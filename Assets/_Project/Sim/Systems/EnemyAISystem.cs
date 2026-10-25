using System;

namespace ES.Sim
{
    public static class EnemyAISystem
    {
        const float SeparationWeight = 1.2f;
        const float KnockDecayPerSecond = 8f;
        const float HostileProjectileRadius = 0.2f;
        const float HostileProjectileLife = 4f;

        static readonly int[] Neighbors = new int[32];

        public static void Step(World w, float dt)
        {
            var e = w.Enemies;
            RebuildGrid(w);

            int every = Math.Max(1, w.Rules.farEnemyUpdateEvery);
            float far2 = w.Rules.farEnemyDistance * w.Rules.farEnemyDistance;

            for (int i = 0; i < e.Count; i++)
            {
                if ((e.Flags[i] & EnemyFlags.Dead) != 0) continue;
                bool flashing = e.FlashUntil[i] > w.Elapsed;
                e.Flags[i] = (byte)(flashing ? e.Flags[i] | EnemyFlags.HitFlash : e.Flags[i] & ~EnemyFlags.HitFlash);
                // bosses: movimento e ataques em BossSystem (não ficam atordoados nem são empurrados)
                if (w.Db.Enemy(e.Type[i]).Behavior == EnemyBehavior.Boss) { e.Knock[i] = Vec2.Zero; continue; }

                if (e.StunUntil[i] > w.Elapsed)
                {
                    e.Flags[i] |= EnemyFlags.Stunned;
                    e.Knock[i] = Vec2.Zero;
                    continue;
                }
                e.Flags[i] = (byte)(e.Flags[i] & ~EnemyFlags.Stunned);

                var target = Nearest(w, e.Pos[i], out float dist2);
                // Bolha Social: quem está perto vai atrás do jogador da bolha
                var lure = EventSystem.Lure(w, e.Pos[i]);
                if (lure != null) { target = lure; dist2 = Vec2.DistanceSq(lure.Pos, e.Pos[i]); }
                float stepDt = dt;
                if (target != null && dist2 > far2)
                {
                    if ((w.Tick + e.Id[i]) % every != 0) continue;
                    stepDt = dt * every;
                }

                var def = w.Db.Enemy(e.Type[i]);
                Vec2 dir = Vec2.Zero;
                if (target != null)
                {
                    var to = target.Pos - e.Pos[i];
                    float dist = (float)Math.Sqrt(dist2);
                    var toN = dist > 1e-4f ? to / dist : Vec2.Zero;
                    if (def.Behavior == EnemyBehavior.Chase) dir = toN;
                    else
                    {
                        if (dist > def.Def.keepMax) dir = toN;
                        else if (dist < def.Def.keepMin) dir = -toN;
                        Fire(w, i, def.Def, target, dist);
                    }
                }

                var sep = Separation(w, i);
                float speed = e.Speed[i] * (e.SlowUntil[i] > w.Elapsed ? 1f - e.SlowAmount[i] : 1f);
                e.Vel[i] = dir * speed + sep * SeparationWeight;
                var next = e.Pos[i] + (e.Vel[i] + e.Knock[i]) * stepDt;
                e.Knock[i] = e.Knock[i] * Math.Max(0f, 1f - KnockDecayPerSecond * stepDt);
                e.Pos[i] = ArenaCollision.Resolve(next, e.Radius[i], w.Arena);
            }

            RebuildGrid(w);
        }

        public static void RebuildGrid(World w)
        {
            var e = w.Enemies;
            w.EnemyGrid.Clear();
            for (int i = 0; i < e.Count; i++) w.EnemyGrid.Insert(i, e.Pos[i]);
        }

        static void Fire(World w, int i, EnemyDef def, PlayerState target, float dist)
        {
            var e = w.Enemies;
            if (e.TelegraphUntil[i] > 0)
            {
                if (w.Elapsed < e.TelegraphUntil[i]) return;
                e.TelegraphUntil[i] = 0;
                e.Flags[i] = (byte)(e.Flags[i] & ~EnemyFlags.Telegraphing);
                e.NextFire[i] = w.Elapsed + def.fireInterval;
                if (w.Hostile.Count >= w.Hostile.Capacity) return;
                var dir = (target.Pos - e.Pos[i]).Normalized();
                float dmg = def.projectileDamage * w.Balance.EnemyDamage * ((e.Flags[i] & EnemyFlags.Elite) != 0 ? w.Rules.eliteDamage : 1f) * BossSystem.InfiniteDamage(w);
                w.Hostile.Spawn(e.Type[i], e.Pos[i] + dir * e.Radius[i], dir * def.projectileSpeed, dmg, HostileProjectileRadius, HostileProjectileLife);
                return;
            }

            if (w.Elapsed < e.NextFire[i] || dist > def.keepMax + 2f) return;
            e.TelegraphUntil[i] = w.Elapsed + def.telegraph * w.Balance.Telegraph;
            e.Flags[i] |= EnemyFlags.Telegraphing;
            w.Events.AddCosmetic(new CosmeticEvent
            {
                Kind = CosmeticKind.EnemyTelegraph, A = e.Id[i], X = e.Pos[i].X, Y = e.Pos[i].Y,
                X2 = target.Pos.X, Y2 = target.Pos.Y, Value = def.telegraph * w.Balance.Telegraph,
            });
        }

        static Vec2 Separation(World w, int i)
        {
            var e = w.Enemies;
            float r = e.Radius[i];
            int n = w.EnemyGrid.Query(e.Pos[i], r + 0.8f, Neighbors);
            Vec2 push = Vec2.Zero;
            for (int k = 0; k < n; k++)
            {
                int j = Neighbors[k];
                if (j == i || j >= e.Count) continue;
                var d = e.Pos[i] - e.Pos[j];
                float min = r + e.Radius[j];
                float len2 = d.LengthSq();
                if (len2 >= min * min) continue;
                if (len2 < 1e-6f) { d = new Vec2(((e.Id[i] & 1) == 0 ? 1 : -1) * 0.01f, 0.01f); len2 = d.LengthSq(); }
                float len = (float)Math.Sqrt(len2);
                push += d / len * ((min - len) / min);
            }
            return push;
        }

        public static PlayerState Nearest(World w, Vec2 pos, out float dist2)
        {
            PlayerState best = null;
            dist2 = float.MaxValue;
            foreach (var p in w.Players.Slots)
            {
                if (p == null || !p.IsActive) continue;
                float d = Vec2.DistanceSq(p.Pos, pos);
                if (d < dist2) { dist2 = d; best = p; }
            }
            return best;
        }
    }
}
