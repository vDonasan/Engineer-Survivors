using System;

namespace ES.Sim
{
    /// <summary>Pulsos periódicos de dano e lentidão ao redor do jogador (Campo Eletromagnético).</summary>
    public sealed class AuraBehavior : IAbilityBehavior
    {
        static readonly int[] Hits = new int[256];

        public void OnAcquire(World w, int slot, AbilityInstance a) { }

        public void Tick(World w, int slot, AbilityInstance a, float dt)
        {
            a.CooldownLeft -= dt;
            if (a.CooldownLeft > 0) return;

            var p = w.Players[slot];
            var e = w.Enemies;
            float interval = Math.Max(0.1f, a.Get(p, StatId.HitInterval));
            float damage = a.Get(p, StatId.Damage);
            float slow = Math.Min(0.9f, a.Get(p, StatId.Slow));
            int n = AbilitySystem.Overlapping(w, p.Pos, a.Get(p, StatId.Area), Hits);
            for (int h = 0; h < n; h++)
            {
                int i = Hits[h];
                CombatSystem.DamageEnemy(w, i, damage, Vec2.Zero, 0f, slot, a.AbilityIndex);
                if (slow <= 0) continue;
                bool slowed = e.SlowUntil[i] > w.Elapsed;
                e.SlowAmount[i] = slowed ? Math.Max(e.SlowAmount[i], slow) : slow;
                e.SlowUntil[i] = w.Elapsed + interval + 0.1f;
            }
            a.FireCount++;
            a.CooldownLeft = interval;
        }
    }
}
