using System;

namespace ES.Sim
{
    /// <summary>Dispara no inimigo mais próximo; projéteis perfuram (Laser Industrial).</summary>
    public sealed class ProjectileBehavior : IAbilityBehavior
    {
        const float ProjectileRadius = 0.3f;
        const float SpreadRadians = 8f * (float)Math.PI / 180f;

        static readonly int[] Target = new int[1];

        public void OnAcquire(World w, int slot, AbilityInstance a) { }

        public void Tick(World w, int slot, AbilityInstance a, float dt)
        {
            a.CooldownLeft -= dt;
            if (a.CooldownLeft > 0) return;

            var p = w.Players[slot];
            float range = a.Get(p, StatId.Range);
            if (AbilitySystem.Nearest(w, p.Pos, range, 1, Target, null, 0) == 0) { a.CooldownLeft = 0; return; }

            a.FireCount++;
            var baseDir = (w.Enemies.Pos[Target[0]] - p.Pos).Normalized();
            float speed = Math.Max(0.1f, a.Get(p, StatId.ProjectileSpeed));
            int count = Math.Max(1, (int)a.Get(p, StatId.Count));
            float damage = a.Get(p, StatId.Damage);
            int pierce = Math.Max(1, (int)a.Get(p, StatId.Pierce));
            float knock = a.Get(p, StatId.Knockback);
            float life = range / speed;

            for (int k = 0; k < count; k++)
            {
                float off = (k - (count - 1) * 0.5f) * SpreadRadians;
                float c = (float)Math.Cos(off), s = (float)Math.Sin(off);
                var dir = new Vec2(baseDir.X * c - baseDir.Y * s, baseDir.X * s + baseDir.Y * c);
                var shot = new Shot
                {
                    Owner = (byte)slot, Ability = (short)a.AbilityIndex, Sprite = AllySprite.Laser,
                    Pos = p.Pos, Vel = dir * speed, Damage = damage, Knockback = knock, Radius = ProjectileRadius, Life = life, Pierce = pierce,
                };
                if (w.Shots.Spawn(shot) < 0) break;
            }
            w.Events.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.ProjectileFired, A = slot, B = a.AbilityIndex, X = p.Pos.X, Y = p.Pos.Y });

            a.CooldownLeft = Math.Max(0.1f, a.Get(p, StatId.Cooldown));
        }
    }
}
