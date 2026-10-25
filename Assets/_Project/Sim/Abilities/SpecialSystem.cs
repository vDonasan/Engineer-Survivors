using System;
using System.Collections.Generic;

namespace ES.Sim
{
    /// <summary>Estado do especial de um jogador (disponível desde o início).</summary>
    public sealed class SpecialState
    {
        public int SpecialIndex;
        public float CooldownLeft;
        public byte LastCounter;
        public int SourceId;

        // Apagão
        public bool WaveActive;
        public float WaveElapsed;
        public Vec2 WaveOrigin;
        public readonly HashSet<ushort> WaveHits = new HashSet<ushort>();

        // Turbina
        public readonly Dictionary<ushort, float> RamLast = new Dictionary<ushort, float>(64);
    }

    public static class SpecialSystem
    {
        const float RamInterval = 0.5f;
        static readonly int[] Hits = new int[512];
        static readonly StatBlock Scratch = new StatBlock();

        /// <summary>Aplica o contador de pedidos vindo da entrada do jogador.</summary>
        public static void Request(World w, int slot, byte counter)
        {
            var p = w.Players[slot];
            var s = p?.Special;
            if (s == null) return;
            byte delta = (byte)(counter - s.LastCounter);
            if (delta == 0 || delta > 127) return;
            s.LastCounter = counter;
            if (!p.IsActive || s.CooldownLeft > 0) return;
            Activate(w, p, s);
        }

        static void Activate(World w, PlayerState p, SpecialState s)
        {
            var sp = w.Db.Special(s.SpecialIndex);
            var d = sp.Def;
            s.CooldownLeft = d.cooldown;
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.SpecialActivated, A = p.Slot, B = s.SpecialIndex, X = p.Pos.X, Y = p.Pos.Y });
            w.Events.AddCosmetic(new CosmeticEvent
            {
                Kind = CosmeticKind.WaveStarted, A = p.Slot, B = s.SpecialIndex, X = p.Pos.X, Y = p.Pos.Y,
                Value = sp.Kind == SpecialKind.Blackout ? d.radius : d.duration,
            });

            if (sp.Kind == SpecialKind.Blackout)
            {
                s.WaveActive = true;
                s.WaveElapsed = 0;
                s.WaveOrigin = p.Pos;
                s.WaveHits.Clear();
                Wave(w, p, s, 0f);
            }
            else
            {
                p.SpecialActiveLeft = d.duration;
                s.RamLast.Clear();
                p.Stats.RemoveSource(s.SourceId);
                p.Stats.Add(new Modifier { Stat = StatId.MoveSpeed, Op = ModOp.Mult, Value = d.moveBonus, SourceId = s.SourceId });
                if (d.damageBonus != 0)
                    p.Stats.Add(new Modifier { Stat = StatId.Damage, Op = ModOp.Mult, Value = d.damageBonus, SourceId = s.SourceId, Scope = ModScope.ForTag(sp.BoostTag) });
                if (d.cooldownBonus != 0)
                    p.Stats.Add(new Modifier { Stat = StatId.Cooldown, Op = ModOp.Mult, Value = d.cooldownBonus, SourceId = s.SourceId, Scope = ModScope.AllAbilities });
                if (d.damageTakenMult != 1f)
                    p.Stats.Add(new Modifier { Stat = StatId.DamageTaken, Op = ModOp.Mult, Value = d.damageTakenMult - 1f, SourceId = s.SourceId });
                Ram(w, p, s);
            }
        }

        public static void ApplyInputs(World w, PlayerInput[] inputs)
        {
            if (inputs == null) return;
            for (int slot = 0; slot < PlayerStore.MaxSlots && slot < inputs.Length; slot++)
                if (w.Players.Slots[slot] != null) Request(w, slot, inputs[slot].SpecialCounter);
        }

        public static void Step(World w, float dt)
        {
            foreach (var p in w.Players.Slots)
            {
                var s = p?.Special;
                if (s == null) continue;
                if (s.CooldownLeft > 0) s.CooldownLeft = Math.Max(0, s.CooldownLeft - dt);

                if (s.WaveActive)
                {
                    s.WaveElapsed += dt;
                    Wave(w, p, s, s.WaveElapsed);
                }

                if (p.SpecialActiveLeft > 0)
                {
                    p.SpecialActiveLeft -= dt;
                    if (p.SpecialActiveLeft <= 0 || !p.IsActive)
                    {
                        p.SpecialActiveLeft = 0;
                        p.Stats.RemoveSource(s.SourceId);
                    }
                    else Ram(w, p, s);
                }
            }
        }

        static void Wave(World w, PlayerState p, SpecialState s, float elapsed)
        {
            var sp = w.Db.Special(s.SpecialIndex);
            var d = sp.Def;
            float t = d.expandSeconds > 0 ? Math.Min(1f, elapsed / d.expandSeconds) : 1f;
            float radius = d.radius * t;
            var e = w.Enemies;
            float damage = Scaled(w, p, d.damage);

            int n = AbilitySystem.Overlapping(w, s.WaveOrigin, radius, Hits);
            for (int k = 0; k < n; k++)
            {
                int i = Hits[k];
                if (!s.WaveHits.Add(e.Id[i])) continue;
                if (w.Db.Enemy(e.Type[i]).Tags.Overlaps(sp.StunTags))
                    e.StunUntil[i] = Math.Max(e.StunUntil[i], w.Elapsed + d.stunSeconds);
                CombatSystem.DamageEnemy(w, i, damage, (e.Pos[i] - s.WaveOrigin).Normalized(), 0f, p.Slot, -1);
            }

            var h = w.Hostile;
            float r2 = radius * radius;
            for (int i = h.Count - 1; i >= 0; i--)
                if (Vec2.DistanceSq(h.Pos[i], s.WaveOrigin) <= r2) h.RemoveAt(i);

            if (t >= 1f) s.WaveActive = false;
        }

        static void Ram(World w, PlayerState p, SpecialState s)
        {
            var d = w.Db.Special(s.SpecialIndex).Def;
            if (d.ramDamage <= 0) return;
            var e = w.Enemies;
            float damage = Scaled(w, p, d.ramDamage);
            int n = AbilitySystem.Overlapping(w, p.Pos, p.Radius, Hits);
            for (int k = 0; k < n; k++)
            {
                int i = Hits[k];
                ushort id = e.Id[i];
                if (s.RamLast.TryGetValue(id, out float last) && w.Elapsed - last < RamInterval) continue;
                s.RamLast[id] = w.Elapsed;
                CombatSystem.DamageEnemy(w, i, damage, (e.Pos[i] - p.Pos).Normalized(), d.ramKnockback, p.Slot, -1);
            }
        }

        /// <summary>Dano-base escalado pelos modificadores de Dano do jogador que valem para as tags da especialização.</summary>
        static float Scaled(World w, PlayerState p, float baseDamage)
        {
            Scratch.SetBase(StatId.Damage, baseDamage);
            var tags = w.Db.Specialization(p.Specialization).AffinityTags;
            return StatQuery.Ability(p.Stats, Scratch, -1, tags, StatId.Damage);
        }
    }
}
