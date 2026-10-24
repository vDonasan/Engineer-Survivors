using System;
using System.Collections.Generic;

namespace ES.Sim
{
    /// <summary>Braços que giram ao redor do jogador (Braço Giratório, Rotor de Impacto).</summary>
    public sealed class OrbitBehavior : IAbilityBehavior
    {
        const float ArmHitRadius = 0.6f;
        const int MaxArms = 12;

        sealed class OrbitState
        {
            public float Angle;
            public readonly Dictionary<ushort, float> LastHit = new Dictionary<ushort, float>(128);
            public float PruneAt;
        }

        static readonly int[] Hits = new int[64];
        static readonly List<ushort> Stale = new List<ushort>(128);

        public void OnAcquire(World w, int slot, AbilityInstance a) => a.State = new OrbitState();

        public void Tick(World w, int slot, AbilityInstance a, float dt)
        {
            var s = a.State as OrbitState ?? (OrbitState)(a.State = new OrbitState());
            var p = w.Players[slot];
            var e = w.Enemies;

            float period = Math.Max(0.1f, a.Get(p, StatId.Duration));
            s.Angle = (s.Angle + (float)(Math.PI * 2) * dt / period) % (float)(Math.PI * 2);

            int arms = Math.Max(1, (int)a.Get(p, StatId.Count));
            float armMult = a.Def.Param("turbineArmMultiplier", 0f);
            if (armMult > 0 && p.SpecialActiveLeft > 0) arms = (int)(arms * armMult);
            arms = Math.Min(arms, MaxArms);

            float radius = a.Get(p, StatId.Range);
            float interval = a.Get(p, StatId.HitInterval);
            float damage = a.Get(p, StatId.Damage);
            float knock = a.Get(p, StatId.Knockback);

            float speedScale = a.Def.Param("speedDamageScale", 0f);
            if (speedScale > 0) damage *= 1f + speedScale * p.Vel.Length() / w.Rules.playerSpeed;

            for (int k = 0; k < arms; k++)
            {
                double ang = s.Angle + Math.PI * 2 * k / arms;
                var armPos = p.Pos + new Vec2((float)Math.Cos(ang), (float)Math.Sin(ang)) * radius;
                int n = AbilitySystem.Overlapping(w, armPos, ArmHitRadius, Hits);
                for (int h = 0; h < n; h++)
                {
                    int i = Hits[h];
                    ushort id = e.Id[i];
                    if (s.LastHit.TryGetValue(id, out float last) && w.Elapsed - last < interval - 1e-4f) continue;
                    s.LastHit[id] = w.Elapsed;
                    CombatSystem.DamageEnemy(w, i, damage, (e.Pos[i] - p.Pos).Normalized(), knock, slot, a.AbilityIndex);
                }
            }

            if (w.Elapsed >= s.PruneAt)
            {
                s.PruneAt = w.Elapsed + 1f;
                Stale.Clear();
                foreach (var kv in s.LastHit) if (w.Elapsed - kv.Value > interval * 2f) Stale.Add(kv.Key);
                foreach (var id in Stale) s.LastHit.Remove(id);
            }
        }
    }
}
