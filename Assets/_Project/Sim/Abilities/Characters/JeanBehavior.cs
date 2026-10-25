using System;

namespace ES.Sim
{
    /// <summary>
    /// Jean (montado no porco): jabutis lançados em curva que, ao cair, continuam andando e batendo.
    /// Nível 3: jabutis ricocheteiam; nível 5: perseguem o inimigo mais próximo.
    /// Evolução: carro fazendo drift ao redor do Jean (dano por contato, derrapagem ampla periódica).
    /// Avançada: volta acelerada que fecha uma zona circular de impacto.
    /// </summary>
    public sealed class JeanBehavior : CharacterWeapon
    {
        const int TThrow = 0, TCarHit = 1, TWide = 2, TWideLeft = 3, TLap = 4, TLapLeft = 5;
        const int CSide = 0;
        const byte VThrown = 0, VWalk = 3;
        const float CarPeriod = 1.6f, CarHitRadius = 0.75f;
        static readonly int[] Hits = new int[64];

        sealed class CarState { public float Angle; public bool WideActive, LapActive; }

        protected override void Step(ref Ctx c, float dt)
        {
            if (c.Due(TThrow))
            {
                int t = c.Nearest(c.Range);
                if (t >= 0) { Throw(ref c, t); c.Reset(TThrow, c.Stat(StatId.Cooldown)); }
            }
            if (c.Evolved) Car(ref c, dt);
        }

        void Throw(ref Ctx c, int target)
        {
            var aim = c.W.Enemies.Pos[target];
            var dir = c.DirTo(target);
            int n = Math.Max(1, (int)c.Stat(StatId.Count));
            float speed = c.Stat(StatId.ProjectileSpeed);
            for (int k = 0; k < n; k++)
            {
                float side = (c.S.Counters[CSide]++ % 2 == 0 ? 1 : -1) * (40f + 10f * (k / 2)) * Deg;
                var s = c.NewShot(AllySprite.Jabuti, Rotate(dir, side), speed, c.Dmg(), 0.3f, c.Range * 1.3f, Math.Max(1, (int)c.Stat(StatId.Pierce)));
                s.Motion = c.Base >= 5 ? ShotMotion.Homing : ShotMotion.Curve;
                s.Curve = c.Base >= 5 ? 3f : 3.5f;
                s.Aim = aim;
                s.Steering = true;
                if (c.Base >= 3) { s.Ricochets = 1; s.RicochetRange = 4f; }
                s.Variant = VThrown;
                c.Fire(s);
            }
            c.Sound(0);
        }

        /// <summary>O jabuti que cai continua andando por alguns segundos.</summary>
        public override void OnShotEnd(World w, ref Shot s)
        {
            if (s.Variant != VThrown) return;
            var a = InstanceOf(w, s.Owner, s.Ability);
            if (a == null) return;
            var c = new Ctx(w, s.Owner, a, (WeaponState)a.State);
            var dir = s.Vel.Normalized();
            if (AbilitySystem.Nearest(w, s.Pos, 5f, 1, Targets, null, 0) > 0) dir = (w.Enemies.Pos[Targets[0]] - s.Pos).Normalized();
            if (dir.LengthSq() < 1e-4f) dir = new Vec2(1, 0);
            var walk = new Shot
            {
                Sprite = AllySprite.JabutiWalk, Pos = s.Pos, Vel = dir * 2.2f, Damage = c.Dmg(0.6f), Radius = 0.3f,
                Life = c.Time(2.5f), Pierce = 2, Variant = VWalk,
                Motion = c.Base >= 5 ? ShotMotion.Homing : ShotMotion.Linear, Curve = 2f,
            };
            SpawnChild(w, s, walk);
        }

        void Car(ref Ctx c, float dt)
        {
            var comp = c.W.Companions;
            var st = c.S.Extra as CarState ?? (CarState)(c.S.Extra = new CarState());
            int id = c.S.Companions[0];
            if (id < 0 || !comp.Items[id].Active) id = c.S.Companions[0] = comp.Spawn(c.Slot, c.Ability, AllySprite.Car, c.Pos + new Vec2(2.6f, 0));
            if (id < 0) return;
            ref var car = ref comp.Items[id];

            if (c.Due(TWide) && !st.WideActive && !st.LapActive && c.Nearest(6f) >= 0)
            {
                st.WideActive = true;
                c.Reset(TWideLeft, 1.2f);
                c.Reset(TWide, c.Period(5f));
                c.Sound(1);
            }
            if (c.Evo >= 3 && c.Due(TLap) && !st.LapActive && c.Nearest(6f) >= 0)
            {
                st.LapActive = true;
                st.WideActive = false;
                c.Reset(TLapLeft, 0.45f);
                c.Reset(TLap, c.Period(8f));
                c.Sound(2);
            }

            float speedMult = c.Evo >= 2 ? 1.2f : 1f;
            float omega = (float)(Math.PI * 2 / CarPeriod) * speedMult * (st.LapActive ? 4.5f : 1f);
            float scale = st.LapActive ? 1.35f : st.WideActive ? 1.6f : 1f;
            st.Angle = (st.Angle + omega * dt) % (float)(Math.PI * 2);
            var rel = new Vec2((float)Math.Cos(st.Angle) * 2.6f, (float)Math.Sin(st.Angle) * 1.9f) * (c.Radius(1f) * scale);
            car.Pos = c.Pos + rel;
            // em drift o carro aponta para dentro da curva
            var tangent = new Vec2(-(float)Math.Sin(st.Angle), (float)Math.Cos(st.Angle));
            car.Param = AllyList.Angle(Rotate(tangent, 28f * Deg));

            if (c.Due(TCarHit))
            {
                int n = AbilitySystem.Overlapping(c.W, car.Pos, CarHitRadius, Hits);
                for (int h = 0; h < n; h++)
                {
                    int k = Hits[h];
                    CombatSystem.DamageEnemy(c.W, k, c.Dmg(c.Evo >= 2 ? 1.25f : 1f), (c.W.Enemies.Pos[k] - c.Pos).Normalized(), 2f, c.Slot, c.Ability);
                }
                c.Reset(TCarHit, st.LapActive ? 0.15f : 0.4f);
            }
            if (st.WideActive && c.Due(TWideLeft))
            {
                st.WideActive = false;
                Area.Burst(c.W, c.Slot, c.Ability, car.Pos, c.Radius(1.6f), c.Dmg(1.5f), 2f, 0f, FxStyle.Drift);
            }
            if (st.LapActive && c.Due(TLapLeft))
            {
                st.LapActive = false;
                Area.Burst(c.W, c.Slot, c.Ability, c.Pos, c.Radius(3.6f), c.Dmg(2.5f), 3f, 0.3f, FxStyle.Drift);
            }
        }
    }
}
