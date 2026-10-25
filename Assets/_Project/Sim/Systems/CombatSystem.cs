using System;

namespace ES.Sim
{
    public static class CombatSystem
    {
        const float KnockScale = 4f;
        const float FlashSeconds = 0.1f;
        const float MaxEnemyRadius = 1.0f;

        static readonly int[] Hits = new int[64];

        /// <summary>Aplica dano a um inimigo vivo. Retorna true se este golpe o matou.</summary>
        public static bool DamageEnemy(World w, int i, float amount, Vec2 knockDir, float knockback, int sourcePlayer, int abilityIndex)
        {
            var e = w.Enemies;
            if (i < 0 || i >= e.Count || (e.Flags[i] & (EnemyFlags.Dead | EnemyFlags.Invulnerable)) != 0 || amount <= 0) return false;
            if (w.Balance.AbilityDamage > 0) amount *= w.Balance.AbilityDamage;

            w.Stats.AddDamage(sourcePlayer, abilityIndex, Math.Min(amount, e.Hp[i]));
            if (sourcePlayer >= 0 && sourcePlayer < PlayerStore.MaxSlots && abilityIndex >= 0) ItemHooks.OnEnemyHit(w, sourcePlayer, abilityIndex, i);
            e.Hp[i] -= amount;
            e.FlashUntil[i] = w.Elapsed + FlashSeconds;
            if (knockback > 0)
            {
                float resist = w.Db.Enemy(e.Type[i]).Def.knockbackResist;
                e.Knock[i] += knockDir * (knockback * KnockScale * (1f - resist));
            }
            w.Events.AddCosmetic(new CosmeticEvent
            {
                Kind = CosmeticKind.Damage, A = e.Id[i], B = sourcePlayer, X = e.Pos[i].X, Y = e.Pos[i].Y, Value = amount,
            });

            if (e.Hp[i] > 0) return false;
            w.Stats.Kills++;
            if (sourcePlayer >= 0 && sourcePlayer < PlayerStore.MaxSlots) w.Stats.KillsBySlot[sourcePlayer]++;
            Kill(w, i);
            return true;
        }

        static void Kill(World w, int i)
        {
            var e = w.Enemies;
            e.Hp[i] = 0;
            e.Flags[i] |= EnemyFlags.Dead;
            var pos = e.Pos[i];
            bool elite = (e.Flags[i] & EnemyFlags.Elite) != 0;
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.EnemyDied, A = e.Id[i], B = e.Type[i], C = w.Tick, X = pos.X, Y = pos.Y });
            if (w.Db.Enemy(e.Type[i]).Behavior == EnemyBehavior.Boss)
            {
                BossSystem.OnKilled(w, i);
                // recompensa: chuva de experiência e uma cura garantida
                for (int k = 0; k < 8; k++)
                {
                    double a = Math.PI * 2 * k / 8;
                    SpawnPickup(w, PickupKind.Xp, pos + new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * 1.2f, e.XpValue[i] / 8f);
                }
                SpawnPickup(w, PickupKind.Heal, pos, 0.5f);
                return;
            }

            SpawnPickup(w, PickupKind.Xp, pos, e.XpValue[i]);
            float healChance = elite ? w.Rules.eliteHealChance : w.Balance.HealDropChance;
            if (w.Rng.NextFloat() < healChance)
                SpawnPickup(w, PickupKind.Heal, pos + new Vec2(0.3f, 0.3f), w.Rules.healAmountFraction);
        }

        static void SpawnPickup(World w, PickupKind kind, Vec2 pos, float value)
        {
            var g = w.Gems;
            while (g.Count >= w.Rules.capGems) MergeOldest(w);
            int i = g.Spawn(kind, pos, value, w.Tick);
            if (i < 0) return;
            w.Events.AddRule(new RuleEvent
            {
                Kind = RuleEventKind.GemSpawned, A = g.Id[i], B = (int)kind, C = (int)Math.Round(value * (kind == PickupKind.Heal ? 100 : 1)),
                X = pos.X, Y = pos.Y,
            });
        }

        /// <summary>Funde a gema de XP mais antiga na gema de XP mais próxima dela.</summary>
        static void MergeOldest(World w)
        {
            var g = w.Gems;
            int oldest = -1;
            for (int i = 0; i < g.Count; i++)
                if (g.Kind[i] == PickupKind.Xp && (oldest < 0 || g.SpawnTick[i] < g.SpawnTick[oldest])) oldest = i;

            int target = -1;
            float best = float.MaxValue;
            if (oldest >= 0)
            {
                for (int i = 0; i < g.Count; i++)
                {
                    if (i == oldest || g.Kind[i] != PickupKind.Xp) continue;
                    float d = Vec2.DistanceSq(g.Pos[i], g.Pos[oldest]);
                    if (d < best) { best = d; target = i; }
                }
            }

            int remove;
            if (oldest >= 0 && target >= 0)
            {
                g.Value[target] += g.Value[oldest];
                remove = oldest;
            }
            else
            {
                remove = 0;
                for (int i = 1; i < g.Count; i++) if (g.SpawnTick[i] < g.SpawnTick[remove]) remove = i;
            }
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.GemCollected, A = g.Id[remove], B = -1 });
            g.RemoveAt(remove);
        }

        /// <summary>Dano final (já escalado pelo balanceamento) antes de armadura.</summary>
        public static void DamagePlayer(World w, int slot, float amount)
        {
            var p = w.Players.Slots[slot];
            if (p == null || !p.IsActive || w.Elapsed < p.InvulnUntil || amount <= 0) return;

            float dodge = Math.Min(0.6f, p.Stats.Get(StatId.Dodge));
            if (dodge > 0 && w.Rng.NextFloat() < dodge)
            {
                p.InvulnUntil = w.Elapsed + w.Rules.hitInvuln * 0.5f;
                w.Events.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.Burst, A = slot, B = (int)FxStyle.Dodge, X = p.Pos.X, Y = p.Pos.Y, Value = 0.7f });
                ItemHooks.OnDodge(w, slot);
                return;
            }
            float final = Math.Max(1f, amount - p.Stats.Get(StatId.Armor)) * p.Stats.Get(StatId.DamageTaken);
            if (p.Shield > 0)
            {
                float absorbed = Math.Min(p.Shield, final);
                p.Shield -= absorbed;
                final -= absorbed;
                if (final <= 0) { p.InvulnUntil = w.Elapsed + w.Rules.hitInvuln; return; }
            }
            w.Stats.DamageTaken[slot] += Math.Min(final, p.Hp);
            p.Hp -= final;
            p.InvulnUntil = w.Elapsed + w.Rules.hitInvuln;
            w.Events.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.Hit, A = slot, X = p.Pos.X, Y = p.Pos.Y, Value = final });

            if (p.Hp > 0) return;
            p.Hp = 0;
            p.Status = PlayerStatus.Downed;
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.PlayerDowned, A = slot, X = p.Pos.X, Y = p.Pos.Y });
        }

        public static void Step(World w, float dt)
        {
            Contact(w);
            HostileProjectiles(w, dt);
            Pickups(w);
            Regen(w, dt);
            Cleanup(w);
        }

        static void Contact(World w)
        {
            var e = w.Enemies;
            foreach (var p in w.Players.Slots)
            {
                if (p == null || !p.IsActive) continue;
                int n = w.EnemyGrid.Query(p.Pos, p.Radius + MaxEnemyRadius, Hits);
                for (int k = 0; k < n; k++)
                {
                    int i = Hits[k];
                    if (i >= e.Count || (e.Flags[i] & (EnemyFlags.Dead | EnemyFlags.Stunned)) != 0) continue;
                    float r = p.Radius + e.Radius[i];
                    if (Vec2.DistanceSq(p.Pos, e.Pos[i]) >= r * r || w.Elapsed < e.NextContact[i]) continue;
                    e.NextContact[i] = w.Elapsed + w.Db.Enemy(e.Type[i]).Def.contactInterval;
                    DamagePlayer(w, p.Slot, e.ContactDamage[i]);
                }
            }
        }

        static void HostileProjectiles(World w, float dt)
        {
            var h = w.Hostile;
            for (int i = h.Count - 1; i >= 0; i--)
            {
                Steer(w, i, dt);
                var prev = h.Pos[i];
                h.Pos[i] += h.Vel[i] * dt;
                h.LifeLeft[i] -= dt;
                bool remove = h.LifeLeft[i] <= 0;
                if (!remove && ArenaCollision.Blocked(h.Pos[i], h.Radius[i], w.Arena))
                {
                    if (h.Bounces[i] > 0)
                    {
                        // ricochete: inverte o eixo que bateu
                        h.Bounces[i]--;
                        var v = h.Vel[i];
                        if (ArenaCollision.Blocked(new Vec2(h.Pos[i].X, prev.Y), h.Radius[i], w.Arena)) v = new Vec2(-v.X, v.Y);
                        if (ArenaCollision.Blocked(new Vec2(prev.X, h.Pos[i].Y), h.Radius[i], w.Arena)) v = new Vec2(v.X, -v.Y);
                        h.Vel[i] = v;
                        h.Pos[i] = prev;
                    }
                    else remove = true;
                }
                if (!remove)
                {
                    foreach (var p in w.Players.Slots)
                    {
                        if (p == null || !p.IsActive || (h.HitMask[i] & (1 << p.Slot)) != 0) continue;
                        float r = p.Radius + h.Radius[i];
                        if (Vec2.DistanceSq(p.Pos, h.Pos[i]) >= r * r) continue;
                        DamagePlayer(w, p.Slot, h.Damage[i]);
                        if (h.Pierce[i]) { h.HitMask[i] |= (byte)(1 << p.Slot); continue; }
                        remove = true;
                        break;
                    }
                }
                if (remove) h.RemoveAt(i);
            }
        }

        /// <summary>Curva e perseguição dos projéteis dos bosses.</summary>
        static void Steer(World w, int i, float dt)
        {
            var h = w.Hostile;
            float turn = h.Turn[i] * dt;
            if (h.Homing[i] > 0 && h.HomingLeft[i] > 0)
            {
                h.HomingLeft[i] -= dt;
                var target = EnemyAISystem.Nearest(w, h.Pos[i], out _);
                if (target != null)
                {
                    var v = h.Vel[i];
                    var to = target.Pos - h.Pos[i];
                    float cross = v.X * to.Y - v.Y * to.X;
                    float max = h.Homing[i] * dt;
                    turn += cross > 0 ? max : -max;
                }
            }
            if (turn == 0) return;
            float c = (float)Math.Cos(turn), s = (float)Math.Sin(turn);
            var u = h.Vel[i];
            h.Vel[i] = new Vec2(u.X * c - u.Y * s, u.X * s + u.Y * c);
        }

        static void Pickups(World w)
        {
            var g = w.Gems;
            foreach (var p in w.Players.Slots)
            {
                if (p == null || !p.IsActive) continue;
                float r = p.Stats.Get(StatId.PickupRadius);
                float r2 = r * r;
                for (int i = g.Count - 1; i >= 0; i--)
                {
                    if (Vec2.DistanceSq(p.Pos, g.Pos[i]) > r2) continue;
                    if (g.Kind[i] == PickupKind.Xp) w.PendingXp += g.Value[i];
                    else
                    {
                        float max = p.Stats.Get(StatId.MaxHp);
                        p.Hp = Math.Min(max, p.Hp + max * g.Value[i]);
                    }
                    w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.GemCollected, A = g.Id[i], B = p.Slot });
                    g.RemoveAt(i);
                }
            }
        }

        static void Regen(World w, float dt)
        {
            foreach (var p in w.Players.Slots)
            {
                if (p == null || !p.IsActive) continue;
                float regen = p.Stats.Get(StatId.Regen) * w.Balance.Regen;
                if (regen <= 0) continue;
                p.Hp = Math.Min(p.Stats.Get(StatId.MaxHp), p.Hp + regen * dt);
            }
        }

        /// <summary>Remove inimigos marcados como mortos.</summary>
        public static void Cleanup(World w)
        {
            var e = w.Enemies;
            for (int i = e.Count - 1; i >= 0; i--)
                if ((e.Flags[i] & EnemyFlags.Dead) != 0) e.RemoveAt(i);
        }
    }
}
