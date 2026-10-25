using System;

namespace ES.Sim
{
    /// <summary>
    /// Donasa: fala tão alta que vira onda sonora — anel ao redor com empurrão e interrupção, a cada
    /// 3ª fala uma onda direcional que atravessa a fila e, periodicamente, um grito em área.
    /// Nível 3: duas ondas direcionais; nível 5: grito mais frequente e maior.
    /// Evolução: ondas maiores e garrafas verdes que quebram em área. Avançada: grito gigante com chuva de garrafas.
    /// </summary>
    public sealed class DonasaBehavior : CharacterWeapon
    {
        const int TTalk = 0, TShout = 1, TBottle = 2, TGiant = 3;
        const int CTalk = 0;

        protected override void Step(ref Ctx c, float dt)
        {
            float amp = c.Evolved ? 1.35f : 1f;
            if (c.Due(TTalk))
            {
                float ring = c.Radius(2.3f * amp);
                int t = c.Nearest(Math.Max(ring + 1f, c.Range));
                if (t >= 0)
                {
                    Area.Burst(c.W, c.Slot, c.Ability, c.Pos, ring, c.Dmg(), c.Stat(StatId.Knockback), 0.15f, FxStyle.Sound);
                    if (c.Every(CTalk, 3)) Waves(ref c, c.DirTo(t), amp);
                    c.Sound(0);
                    c.Reset(TTalk, c.Stat(StatId.Cooldown));
                }
            }
            if (c.Due(TShout) && c.Nearest(c.Radius(4f)) >= 0)
            {
                float r = c.Radius((c.Base >= 5 ? 4.6f : 4f) * amp);
                Area.Burst(c.W, c.Slot, c.Ability, c.Pos, r, c.Dmg(2.2f), 3f, 0.4f, FxStyle.Sound);
                c.Sound(1);
                c.Reset(TShout, c.Period(c.Base >= 5 ? 4.5f : 6f));
            }
            if (!c.Evolved) return;

            if (c.Due(TBottle))
            {
                int t = c.Nearest(9f);
                if (t >= 0)
                {
                    var dir = c.DirTo(t);
                    int n = c.Evo >= 2 ? 3 : 2;
                    for (int k = 0; k < n; k++)
                    {
                        var s = c.NewShot(AllySprite.Bottle, Rotate(dir, (k - (n - 1) * 0.5f) * 15f * Deg), c.Speed(9f), c.Dmg(1.1f), 0.25f, 9f);
                        s.BurstWhen = ShotBurst.OnEnd;
                        s.BurstRadius = c.Radius(1.4f);
                        s.BurstDamage = c.Dmg(1.0f);
                        s.BurstStyle = FxStyle.Glass;
                        c.Fire(s);
                    }
                    c.Sound(3);
                    c.Reset(TBottle, c.Period(1.5f));
                }
            }
            if (c.Evo >= 3 && c.Due(TGiant) && c.Nearest(6f) >= 0)
            {
                Area.Burst(c.W, c.Slot, c.Ability, c.Pos, c.Radius(6f), c.Dmg(3f), 4f, 0.6f, FxStyle.Sound);
                int n = AbilitySystem.Nearest(c.W, c.Pos, 7f, 8, Targets, null, 0);
                for (int k = 0; k < n; k++)
                    c.Strike(c.W.Enemies.Pos[Targets[k]], 0.3f + k * 0.1f, c.Radius(1.3f), c.Dmg(1.5f), FxStyle.Glass);
                c.Sound(4);
                c.Reset(TGiant, c.Period(10f));
            }
        }

        void Waves(ref Ctx c, Vec2 dir, float amp)
        {
            int n = c.Base >= 3 ? 2 : 1;
            for (int k = 0; k < n; k++)
            {
                float off = n == 1 ? 0 : (k == 0 ? -15f : 15f) * Deg;
                var s = c.NewShot(AllySprite.SoundWave, Rotate(dir, off), c.Stat(StatId.ProjectileSpeed), c.Dmg(1.2f), 0.55f * amp, c.Range, c.Evolved ? 14 : 8);
                s.Knockback = 1f;
                s.Stun = 0.1f;
                c.Fire(s);
            }
            c.Sound(2);
        }
    }
}
