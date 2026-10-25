using System;

namespace ES.Sim
{
    /// <summary>Dano em área, cone, cadeia e estados de inimigo, com o efeito visual correspondente.</summary>
    public static class Area
    {
        const int MaxChainJumps = 12;
        static readonly int[] Hits = new int[512];
        static readonly int[] Next = new int[1];
        static readonly ushort[] ChainIds = new ushort[64];
        static readonly int[] Near = new int[64];

        /// <summary>Explosão circular: fere todos no raio e desenha o anel (alcance visível no impacto).</summary>
        public static int Burst(World w, int slot, int ability, Vec2 pos, float radius, float damage, float knock, float stun, FxStyle style)
        {
            if (radius <= 0) return 0;
            w.Events.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.Burst, A = slot, B = (int)style, X = pos.X, Y = pos.Y, Value = radius });
            if (damage <= 0) return 0;
            var e = w.Enemies;
            int n = AbilitySystem.Overlapping(w, pos, radius, Hits);
            for (int h = 0; h < n; h++)
            {
                int i = Hits[h];
                var dir = (e.Pos[i] - pos).Normalized();
                CombatSystem.DamageEnemy(w, i, damage, dir, knock, slot, ability);
                if (stun > 0) e.StunUntil[i] = Math.Max(e.StunUntil[i], w.Elapsed + stun);
            }
            return n;
        }

        /// <summary>Golpe em cone à frente (soco, onda direcional). Retorna quantos foram atingidos.</summary>
        public static int Cone(World w, int slot, int ability, Vec2 origin, Vec2 dir, float radius, float arcRadians, float damage, float knock, float stun, FxStyle style)
        {
            dir = dir.Normalized();
            if (dir.LengthSq() < 1e-6f) dir = new Vec2(0, -1);
            w.Events.AddCosmetic(new CosmeticEvent
            {
                Kind = CosmeticKind.Swing, A = slot, B = (int)style, X = origin.X, Y = origin.Y,
                X2 = dir.X * radius, Y2 = dir.Y * radius, Value = arcRadians * 180f / (float)Math.PI,
            });
            var e = w.Enemies;
            float cosHalf = (float)Math.Cos(arcRadians * 0.5f);
            int n = AbilitySystem.Overlapping(w, origin, radius, Hits);
            int hit = 0;
            for (int h = 0; h < n; h++)
            {
                int i = Hits[h];
                var to = e.Pos[i] - origin;
                float len = to.Length();
                // inimigos colados no jogador sempre contam
                if (len > e.Radius[i] + 0.3f && Vec2.Dot(to / len, dir) < cosHalf) continue;
                CombatSystem.DamageEnemy(w, i, damage, len > 1e-4f ? to / len : dir, knock, slot, ability);
                if (stun > 0) e.StunUntil[i] = Math.Max(e.StunUntil[i], w.Elapsed + stun);
                hit++;
            }
            return hit;
        }

        /// <summary>
        /// Arco elétrico que começa em <paramref name="first"/> e salta para o vizinho mais próximo
        /// ainda não atingido, até <paramref name="jumps"/> saltos (limitado a 12).
        /// </summary>
        public static int Chain(World w, int slot, int ability, Vec2 from, int first, int jumps, float range, float damage, float falloff, FxStyle style)
        {
            var e = w.Enemies;
            if (first < 0 || first >= e.Count) return 0;
            jumps = Math.Max(0, Math.Min(MaxChainJumps, jumps));
            int count = 0, cur = first;
            var prev = from;
            for (int jump = 0; jump <= jumps; jump++)
            {
                var pos = e.Pos[cur];
                if (count < ChainIds.Length) ChainIds[count] = e.Id[cur];
                count++;
                w.Events.AddCosmetic(new CosmeticEvent
                {
                    Kind = CosmeticKind.ChainArc, A = slot, B = jump, X = prev.X, Y = prev.Y, X2 = pos.X, Y2 = pos.Y, Value = (float)style,
                });
                float dmg = damage * (float)Math.Pow(1f - falloff, jump);
                CombatSystem.DamageEnemy(w, cur, dmg, (pos - prev).Normalized(), 0f, slot, ability);
                prev = pos;
                if (AbilitySystem.Nearest(w, pos, range, 1, Next, ChainIds, Math.Min(count, ChainIds.Length)) == 0) break;
                cur = Next[0];
            }
            return count;
        }

        public static void ApplySlow(World w, int i, float amount, float seconds)
        {
            var e = w.Enemies;
            amount = Math.Min(0.9f, amount);
            bool slowed = e.SlowUntil[i] > w.Elapsed;
            e.SlowAmount[i] = slowed ? Math.Max(e.SlowAmount[i], amount) : amount;
            e.SlowUntil[i] = Math.Max(e.SlowUntil[i], w.Elapsed + seconds);
        }

        /// <summary>Dano contínuo; um novo só substitui o atual se for mais forte.</summary>
        public static void ApplyDot(World w, int i, float dps, float seconds, int slot, int ability)
        {
            var e = w.Enemies;
            if (e.DotUntil[i] > w.Elapsed && e.DotDps[i] > dps) return;
            e.DotDps[i] = dps;
            e.DotUntil[i] = w.Elapsed + seconds;
            if (e.DotNext[i] <= w.Elapsed) e.DotNext[i] = w.Elapsed + StatusSystem.DotInterval;
            e.DotOwner[i] = (byte)slot;
            e.DotAbility[i] = (short)ability;
        }

        /// <summary>Inimigo vivo com mais vizinhos num raio (alvo de golpes em área). -1 se nenhum.</summary>
        public static int Densest(World w, Vec2 around, float range, float clusterRadius)
        {
            var e = w.Enemies;
            int n = AbilitySystem.Overlapping(w, around, range, Hits);
            int best = -1, bestCount = -1;
            // amostra no máximo 24 candidatos para manter o custo baixo
            int step = Math.Max(1, n / 24);
            for (int h = 0; h < n; h += step)
            {
                int i = Hits[h];
                int c = AbilitySystem.Overlapping(w, e.Pos[i], clusterRadius, Near);
                if (c > bestCount) { bestCount = c; best = i; }
            }
            return best;
        }
    }

    /// <summary>Dano contínuo e dano atrasado (cola) nos inimigos.</summary>
    public static class StatusSystem
    {
        public const float DotInterval = 0.25f;

        public static void Step(World w)
        {
            var e = w.Enemies;
            float now = w.Elapsed;
            for (int i = 0; i < e.Count; i++)
            {
                if ((e.Flags[i] & EnemyFlags.Dead) != 0) continue;
                if (e.DotUntil[i] > 0 && now >= e.DotNext[i])
                {
                    if (now <= e.DotUntil[i] + 1e-4f)
                    {
                        e.DotNext[i] = now + DotInterval;
                        CombatSystem.DamageEnemy(w, i, e.DotDps[i] * DotInterval, Vec2.Zero, 0f, e.DotOwner[i], e.DotAbility[i]);
                    }
                    else e.DotUntil[i] = 0;
                }
                if ((e.Flags[i] & EnemyFlags.Dead) == 0 && e.DelayAt[i] > 0 && now >= e.DelayAt[i])
                {
                    e.DelayAt[i] = 0;
                    var pos = e.Pos[i];
                    Area.Burst(w, e.DelayOwner[i], e.DelayAbility[i], pos, 0.9f, e.DelayDamage[i], 0.5f, 0f, FxStyle.Paper);
                }
            }
        }
    }
}
