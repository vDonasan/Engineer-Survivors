using System;

namespace ES.Sim
{
    /// <summary>
    /// Garcia: celulares que ricocheteiam e leques de papéis de cola que grudam e estouram depois.
    /// Evolução: computadores (impacto em área + fragmentos) e TVs que caem numa área marcada.
    /// Avançada: chuva de eletrônicos ao redor.
    /// </summary>
    public sealed class GarciaBehavior : CharacterWeapon
    {
        const int TPhone = 0, TPaper = 1, TComputer = 2, TTv = 3, TRain = 4;
        const byte VComputer = 2, VTv = 1, VFragment = 3;

        protected override void Step(ref Ctx c, float dt)
        {
            if (c.Due(TPhone))
            {
                int t = c.Nearest(c.Range);
                if (t >= 0) { Phones(ref c, t); c.Reset(TPhone, c.Stat(StatId.Cooldown)); }
            }
            if (c.Due(TPaper))
            {
                int t = c.Nearest(6.5f);
                if (t >= 0) { Papers(ref c, t); c.Reset(TPaper, c.Period(1.6f)); }
            }
            if (!c.Evolved) return;

            if (c.Due(TComputer))
            {
                int t = c.Nearest(c.Range);
                if (t >= 0)
                {
                    var s = c.NewShot(AllySprite.Computer, c.DirTo(t), c.Speed(7f), c.Dmg(2.2f), 0.45f, c.Range);
                    s.Knockback = 2f;
                    s.BurstWhen = ShotBurst.OnEnd;
                    s.BurstRadius = c.Radius(1.8f);
                    s.BurstDamage = c.Dmg(1.4f);
                    s.BurstStyle = FxStyle.Tech;
                    s.Variant = VComputer;
                    c.Fire(s);
                    c.Sound(2);
                    c.Reset(TComputer, c.Period(2.6f));
                }
            }
            if (c.Due(TTv))
            {
                int t = Area.Densest(c.W, c.Pos, c.Range, 2f);
                if (t >= 0)
                {
                    var at = c.W.Enemies.Pos[t];
                    c.Strike(at, 0.8f, c.Radius(2.2f), c.Dmg(3.2f), FxStyle.Tech, VTv, 2f);
                    if (c.Evo >= 2)
                    {
                        int t2 = c.Nearest(c.Range);
                        var at2 = t2 >= 0 && Vec2.DistanceSq(c.W.Enemies.Pos[t2], at) > 4f ? c.W.Enemies.Pos[t2] : at + new Vec2(2.5f, 0);
                        c.Strike(at2, 0.95f, c.Radius(2.2f), c.Dmg(3.2f), FxStyle.Tech, VTv, 2f);
                    }
                    c.Sound(3, at);
                    c.Reset(TTv, c.Period(3.5f));
                }
            }
            if (c.Evo >= 3 && c.Due(TRain))
            {
                int n = AbilitySystem.Nearest(c.W, c.Pos, 7f, 10, Targets, null, 0);
                if (n > 0)
                {
                    for (int k = 0; k < 10; k++)
                    {
                        var at = k < n ? c.W.Enemies.Pos[Targets[k]]
                            : c.Pos + Rotate(new Vec2(c.W.Rng.Range(1.5f, 5f), 0), c.W.Rng.Range(0f, 6.2831853f));
                        c.Strike(at, 0.4f + k * 0.12f, c.Radius(1.4f), c.Dmg(2f), FxStyle.Tech, 0, 1.5f);
                    }
                    c.Sound(4);
                    c.Reset(TRain, c.Period(9f));
                }
            }
        }

        void Phones(ref Ctx c, int target)
        {
            var dir = c.DirTo(target);
            int n = Math.Max(1, (int)c.Stat(StatId.Count));
            for (int k = 0; k < n; k++)
            {
                var s = c.NewShot(AllySprite.Phone, Rotate(dir, (k - (n - 1) * 0.5f) * 10f * Deg), c.Stat(StatId.ProjectileSpeed), c.Dmg(), 0.28f, c.Range);
                s.Knockback = 1f;
                s.Ricochets = c.Base >= 3 ? 2 : 1;
                s.RicochetRange = 4f;
                c.Fire(s);
            }
            c.Sound(0);
        }

        void Papers(ref Ctx c, int target)
        {
            var dir = c.DirTo(target);
            int n = 5 + (c.Base >= 2 ? 1 : 0) + (c.Base >= 5 ? 2 : 0);
            float spread = 50f * Deg;
            for (int k = 0; k < n; k++)
            {
                float off = n == 1 ? 0 : -spread / 2 + spread * k / (n - 1);
                var s = c.NewShot(AllySprite.Paper, Rotate(dir, off), c.Speed(9f), c.Dmg(0.45f), 0.22f, 6.5f);
                s.Status = ShotStatus.Sticky;
                s.StatusTime = 1f;
                s.StatusValue = c.Dmg(c.Base >= 5 ? 1.4f : 1.0f);
                c.Fire(s);
            }
            c.Sound(1);
        }

        static void Fragments(World w, in Shot parent, Vec2 pos, int count, float damage, float phase)
        {
            for (int k = 0; k < count; k++)
            {
                var dir = Rotate(new Vec2(1, 0), phase + k * 6.2831853f / count);
                SpawnChild(w, parent, new Shot
                {
                    Sprite = AllySprite.Fragment, Pos = pos, Vel = dir * 7f, Damage = damage, Radius = 0.2f, Life = 0.36f, Pierce = 1, Variant = VFragment,
                });
            }
        }

        public override void OnShotEnd(World w, ref Shot s)
        {
            if (s.Variant != VComputer) return;
            var a = InstanceOf(w, s.Owner, s.Ability);
            if (a == null) return;
            var c = new Ctx(w, s.Owner, a, (WeaponState)a.State);
            Fragments(w, s, s.Pos, 4, c.Dmg(0.5f), s.Age);
        }

        public override void OnStrike(World w, in Strike st)
        {
            if (st.Variant != VTv) return;
            var a = InstanceOf(w, st.Owner, st.Ability);
            if (a == null) return;
            var c = new Ctx(w, st.Owner, a, (WeaponState)a.State);
            var parent = new Shot { Owner = st.Owner, Ability = st.Ability, Depth = st.Depth };
            Fragments(w, parent, st.Pos, 5, c.Dmg(0.5f), 0.3f);
        }
    }
}
