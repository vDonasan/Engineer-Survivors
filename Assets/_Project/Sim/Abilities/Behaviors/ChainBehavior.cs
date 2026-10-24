using System;

namespace ES.Sim
{
    /// <summary>Arco que salta do inimigo mais próximo para os vizinhos (Descarga, Pulso).</summary>
    public sealed class ChainBehavior : IAbilityBehavior
    {
        static readonly int[] Initial = new int[8];
        static readonly int[] Next = new int[1];
        static readonly ushort[] HitIds = new ushort[64];

        public void OnAcquire(World w, int slot, AbilityInstance a) { }

        public void Tick(World w, int slot, AbilityInstance a, float dt)
        {
            a.CooldownLeft -= dt;
            if (a.CooldownLeft > 0) return;

            var p = w.Players[slot];
            var e = w.Enemies;
            int count = Math.Max(1, Math.Min(Initial.Length, (int)a.Get(p, StatId.Count)));
            int n = AbilitySystem.Nearest(w, p.Pos, a.Get(p, StatId.Range), count, Initial, null, 0);
            if (n == 0) { a.CooldownLeft = 0; return; }

            a.FireCount++;
            SynergySystem.ChainCharge(w, slot, a, out float chargeMult, out int extraChains);

            float damage = a.Get(p, StatId.Damage) * chargeMult;
            int chains = (int)a.Get(p, StatId.Chains) + extraChains;
            float chainRange = a.Get(p, StatId.ChainRange);
            float falloff = a.Get(p, StatId.ChainFalloff);
            int hitCount = 0;

            for (int t = 0; t < n; t++)
            {
                int cur = Initial[t];
                if ((e.Flags[cur] & EnemyFlags.Dead) != 0 && hitCount > 0) continue;
                var prev = p.Pos;
                for (int jump = 0; jump <= chains; jump++)
                {
                    var pos = e.Pos[cur];
                    if (hitCount < HitIds.Length) HitIds[hitCount++] = e.Id[cur];
                    w.Events.AddCosmetic(new CosmeticEvent
                    {
                        Kind = CosmeticKind.ChainArc, A = slot, B = jump, X = prev.X, Y = prev.Y, X2 = pos.X, Y2 = pos.Y,
                        Value = chargeMult > 1f ? 1f : 0f,
                    });
                    float dmg = damage * (float)Math.Pow(1f - falloff, jump);
                    CombatSystem.DamageEnemy(w, cur, dmg, (pos - prev).Normalized(), 0f, slot, a.AbilityIndex);
                    prev = pos;
                    if (AbilitySystem.Nearest(w, pos, chainRange, 1, Next, HitIds, hitCount) == 0) break;
                    cur = Next[0];
                }
            }

            a.CooldownLeft = Math.Max(0.1f, a.Get(p, StatId.Cooldown));
        }
    }
}
