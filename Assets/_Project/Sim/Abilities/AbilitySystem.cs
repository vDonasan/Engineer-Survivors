using System;
using System.Collections.Generic;

namespace ES.Sim
{
    public static class AbilitySystem
    {
        /// <summary>Maior raio de inimigo (prensa elite ≈ 0,91) para margens de consulta.</summary>
        public const float MaxEnemyRadius = 1.0f;

        static readonly Dictionary<string, IAbilityBehavior> Behaviors = new Dictionary<string, IAbilityBehavior>
        {
            { "chain", new ChainBehavior() },
            { "orbit", new OrbitBehavior() },
            { "projectile", new ProjectileBehavior() },
            { "aura", new AuraBehavior() },
            { "passive", new PassiveBehavior() },
            { "attribute", new PassiveBehavior() },
        };

        static readonly int[] QueryBuf = new int[512];

        public static IAbilityBehavior For(string behavior) =>
            Behaviors.TryGetValue(behavior ?? "", out var b) ? b : throw new ArgumentException("comportamento sem implementação: " + behavior);

        /// <summary>
        /// Concede a habilidade no nível 1. Sobrevivência (Kit) é aplicada na hora e
        /// não ocupa espaço: retorna null.
        /// </summary>
        public static AbilityInstance Grant(World w, int slot, int abilityIndex)
        {
            var p = w.Players[slot];
            var def = w.Db.Ability(abilityIndex);
            if (def.Kind == AbilityKind.Survival)
            {
                float max = p.Stats.Get(StatId.MaxHp);
                p.Hp = Math.Min(max, p.Hp + max * w.Rules.healAmountFraction);
                return null;
            }

            var a = new AbilityInstance { AbilityIndex = abilityIndex, Def = def, Level = 1, SourceId = w.NextSourceId++ };
            a.Stats.CopyBaseFrom(def.BaseStats);
            p.Abilities.Add(a);
            ApplyGrants(p, a);
            For(def.Def.behavior).OnAcquire(w, slot, a);
            AffinitySystem.OnAbilityChanged(w, slot, a);
            return a;
        }

        /// <summary>Sobe um nível; false se já está no máximo.</summary>
        public static bool LevelUp(World w, int slot, AbilityInstance a)
        {
            if (a.Level >= a.Def.MaxLevel) return false;
            a.Level++;
            int li = a.Level - 2;
            if (li >= 0 && li < a.Def.Levels.Length)
            {
                var lv = a.Def.Levels[li];
                foreach (var sv in lv.Add) a.Stats.Add(new Modifier { Stat = sv.Stat, Op = ModOp.Add, Value = sv.Value, SourceId = -a.Level });
                foreach (var sv in lv.Mult) a.Stats.Add(new Modifier { Stat = sv.Stat, Op = ModOp.Mult, Value = sv.Value, SourceId = -a.Level });
            }
            ApplyGrants(w.Players[slot], a);
            AffinitySystem.OnAbilityChanged(w, slot, a);
            return true;
        }

        /// <summary>Um modificador por concessão, com valor × nível (substitui o anterior).</summary>
        static void ApplyGrants(PlayerState p, AbilityInstance a)
        {
            if (a.Def.Grants.Length == 0) return;
            float maxBefore = p.Stats.Get(StatId.MaxHp);
            p.Stats.RemoveSource(a.SourceId);
            foreach (var g in a.Def.Grants) p.Stats.Add(g.ToModifier(a.Level, a.SourceId, a.AbilityIndex));
            float maxAfter = p.Stats.Get(StatId.MaxHp);
            if (maxAfter > maxBefore) p.Hp += maxAfter - maxBefore;
            if (p.Hp > maxAfter) p.Hp = maxAfter;
        }

        public static void Step(World w, float dt)
        {
            foreach (var p in w.Players.Slots)
            {
                if (p == null || !p.IsActive) continue;
                for (int i = 0; i < p.Abilities.Count; i++)
                {
                    var a = p.Abilities[i];
                    For(a.Def.Def.behavior).Tick(w, p.Slot, a, dt);
                }
            }
            PlayerProjectiles.Step(w, dt);
        }

        /// <summary>
        /// Até <paramref name="count"/> inimigos vivos mais próximos dentro do alcance,
        /// em ordem de distância, ignorando os IDs excluídos.
        /// </summary>
        public static int Nearest(World w, Vec2 pos, float range, int count, int[] result, ushort[] exclude, int excludeCount)
        {
            var e = w.Enemies;
            int n = w.EnemyGrid.Query(pos, range, QueryBuf);
            int found = 0;
            for (int k = 0; k < n; k++)
            {
                int i = QueryBuf[k];
                if (i >= e.Count || (e.Flags[i] & EnemyFlags.Dead) != 0 || Contains(exclude, excludeCount, e.Id[i])) continue;
                float d = Vec2.DistanceSq(pos, e.Pos[i]);
                // Inserção ordenada nos primeiros "count".
                int at = found < count ? found : count;
                while (at > 0 && Vec2.DistanceSq(pos, e.Pos[result[at - 1]]) > d) at--;
                if (at >= count) continue;
                int last = Math.Min(found, count - 1);
                for (int s = last; s > at; s--) result[s] = result[s - 1];
                result[at] = i;
                if (found < count) found++;
            }
            return found;
        }

        static bool Contains(ushort[] ids, int count, ushort id)
        {
            for (int i = 0; i < count; i++) if (ids[i] == id) return true;
            return false;
        }

        /// <summary>Inimigos vivos (índices) a até raio + raio do inimigo do ponto.</summary>
        public static int Overlapping(World w, Vec2 pos, float radius, int[] result)
        {
            var e = w.Enemies;
            int n = w.EnemyGrid.Query(pos, radius + MaxEnemyRadius, QueryBuf);
            int found = 0;
            for (int k = 0; k < n && found < result.Length; k++)
            {
                int i = QueryBuf[k];
                if (i >= e.Count || (e.Flags[i] & EnemyFlags.Dead) != 0) continue;
                float r = radius + e.Radius[i];
                if (Vec2.DistanceSq(pos, e.Pos[i]) >= r * r) continue;
                result[found++] = i;
            }
            return found;
        }
    }
}
