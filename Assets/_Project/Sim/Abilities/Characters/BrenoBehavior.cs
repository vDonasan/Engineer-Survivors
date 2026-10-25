using System;

namespace ES.Sim
{
    /// <summary>
    /// Breno: rajadas de Doritos perfurantes e hambúrgueres pesados que empurram e fazem onda ao cair.
    /// Nível 3: um Dorito por rajada ricocheteia; nível 5: todos ricocheteiam e a onda cresce.
    /// Evolução: pneus de crossfit que rolam, quicam nas paredes e geram onda ao parar.
    /// Avançada: dois pneus orbitais.
    /// </summary>
    public sealed class BrenoBehavior : CharacterWeapon
    {
        const int TChips = 0, TBurger = 1, TTire = 2, TOrbitHit = 3;
        const float OrbitRadius = 2.2f, OrbitPeriod = 1.4f, OrbitHitRadius = 0.65f;
        static readonly int[] Hits = new int[64];

        protected override void Step(ref Ctx c, float dt)
        {
            if (c.Due(TChips))
            {
                int t = c.Nearest(c.Range);
                if (t >= 0) { Doritos(ref c, t); c.Reset(TChips, c.Stat(StatId.Cooldown)); }
            }
            if (c.Due(TBurger))
            {
                int t = c.Nearest(c.Range);
                if (t >= 0)
                {
                    var s = c.NewShot(AllySprite.Burger, c.DirTo(t), c.Speed(8f), c.Dmg(2.6f), 0.42f, c.Range);
                    s.Knockback = 3f;
                    s.BurstWhen = ShotBurst.OnEnd;
                    s.BurstRadius = c.Radius(c.Base >= 5 ? 1.7f : 1.3f);
                    s.BurstDamage = c.Dmg(0.9f);
                    s.BurstStyle = FxStyle.Food;
                    c.Fire(s);
                    c.Sound(1);
                    c.Reset(TBurger, c.Period(1.5f));
                }
            }
            if (!c.Evolved) return;

            if (c.Due(TTire))
            {
                int t = c.Nearest(c.Range + 2f);
                if (t >= 0)
                {
                    bool big = c.Evo >= 2;
                    var s = c.NewShot(AllySprite.Tire, c.DirTo(t), c.Speed(9.5f), c.Dmg(big ? 2.2f : 1.8f), big ? 0.75f : 0.55f, 60f, 99);
                    s.Motion = ShotMotion.Roll;
                    s.Curve = 2.2f;
                    s.Life = 6f;
                    s.Knockback = 2f;
                    s.BurstWhen = ShotBurst.OnEnd;
                    s.BurstRadius = c.Radius(big ? 2.4f : 2.0f);
                    s.BurstDamage = c.Dmg(1.5f);
                    s.BurstStyle = FxStyle.Impact;
                    c.Fire(s);
                    c.Sound(2);
                    c.Reset(TTire, c.Period(3f));
                }
            }
            if (c.Evo >= 3) Orbit(ref c, dt);
        }

        void Doritos(ref Ctx c, int target)
        {
            var dir = c.DirTo(target);
            int n = Math.Max(1, (int)c.Stat(StatId.Count));
            int pierce = Math.Max(1, (int)c.Stat(StatId.Pierce));
            for (int k = 0; k < n; k++)
            {
                float off = (k - (n - 1) * 0.5f) * 9f * Deg;
                var s = c.NewShot(AllySprite.Dorito, Rotate(dir, off), c.Stat(StatId.ProjectileSpeed), c.Dmg(), 0.22f, c.Range, pierce);
                if (c.Base >= 5) { s.Ricochets = k == 0 ? 2 : 1; s.RicochetRange = 4f; }
                else if (c.Base >= 3 && k == 0) { s.Ricochets = 1; s.RicochetRange = 4f; }
                c.Fire(s);
            }
            c.Sound(0);
        }

        /// <summary>Dois pneus girando ao redor do Breno; cada um bate a cada 0,5 s em quem toca.</summary>
        void Orbit(ref Ctx c, float dt)
        {
            var comp = c.W.Companions;
            float angle = (float)(c.W.Elapsed * Math.PI * 2 / OrbitPeriod);
            bool hit = c.Due(TOrbitHit);
            for (int k = 0; k < 2; k++)
            {
                int id = c.S.Companions[k];
                if (id < 0 || !comp.Items[id].Active) id = c.S.Companions[k] = comp.Spawn(c.Slot, c.Ability, AllySprite.TireOrbit, c.Pos);
                if (id < 0) continue;
                float ang = angle + k * (float)Math.PI;
                ref var t = ref comp.Items[id];
                t.Pos = c.Pos + new Vec2((float)Math.Cos(ang), (float)Math.Sin(ang)) * c.Radius(OrbitRadius);
                t.Param = AllyList.Angle(new Vec2(-(float)Math.Sin(ang), (float)Math.Cos(ang)));
                if (!hit) continue;
                int n = AbilitySystem.Overlapping(c.W, t.Pos, OrbitHitRadius, Hits);
                for (int h = 0; h < n; h++)
                {
                    int e = Hits[h];
                    CombatSystem.DamageEnemy(c.W, e, c.Dmg(1.2f), (c.W.Enemies.Pos[e] - c.Pos).Normalized(), 2.5f, c.Slot, c.Ability);
                }
            }
            if (hit) c.Reset(TOrbitHit, 0.5f);
        }
    }
}
