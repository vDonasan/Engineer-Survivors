namespace ES.Sim
{
    /// <summary>
    /// Miyuki: rajadas ultrarrápidas de dígitos; a cada 4ª, uma expressão que atravessa a fila;
    /// periodicamente uma equação explode em área (com aviso). Nível 3/5: dígitos ricocheteiam 1/2 vezes.
    /// Evolução: dígitos marcam; sushi em curva e sashimi ricocheteante explodem em alvos marcados.
    /// Avançada: esteira de pratos que explode em cadeia ao longo de uma linha.
    /// </summary>
    public sealed class MiyukiBehavior : CharacterWeapon
    {
        const int TVolley = 0, TDigit = 1, TEquation = 2, TSushi = 3, TSashimi = 4, TBelt = 5;
        const int CLeft = 0, CVolley = 1;
        const float DigitGap = 0.06f;

        protected override void Step(ref Ctx c, float dt)
        {
            if (c.Due(TVolley) && c.S.Counters[CLeft] == 0 && c.Nearest(c.Range) >= 0)
            {
                c.S.Counters[CLeft] = System.Math.Max(1, (int)c.Stat(StatId.Count));
                c.Reset(TVolley, c.Stat(StatId.Cooldown));
                if (c.Every(CVolley, 4)) Expression(ref c);
            }
            if (c.S.Counters[CLeft] > 0 && c.Due(TDigit))
            {
                int target = c.Nearest(c.Range + 1f);
                if (target < 0) c.S.Counters[CLeft] = 0;
                else
                {
                    Digit(ref c, target);
                    c.S.Counters[CLeft]--;
                    c.Reset(TDigit, DigitGap);
                }
            }
            if (c.Due(TEquation))
            {
                int t = Area.Densest(c.W, c.Pos, c.Range, 2f);
                if (t >= 0)
                {
                    float r = c.Radius(c.Base >= 5 ? 2.5f : 2.0f);
                    c.Strike(c.W.Enemies.Pos[t], 0.7f, r, c.Dmg(3.5f), FxStyle.Math);
                    c.Sound(2);
                    c.Reset(TEquation, c.Period(c.Base >= 5 ? 6f : 8f));
                }
            }
            if (!c.Evolved) return;

            if (c.Due(TSushi))
            {
                int t = c.Nearest(c.Range);
                if (t >= 0) { Sushi(ref c, t); c.Reset(TSushi, c.Period(1.6f)); }
            }
            if (c.Due(TSashimi))
            {
                int t = c.Nearest(c.Range);
                if (t >= 0)
                {
                    var s = c.NewShot(AllySprite.Sashimi, c.DirTo(t), c.Speed(11f), c.Dmg(1.5f), 0.25f, c.Range);
                    s.Ricochets = 3;
                    s.RicochetRange = 4f;
                    MarkBurst(ref c, ref s);
                    c.Fire(s);
                    c.Reset(TSashimi, c.Period(2.4f));
                }
            }
            if (c.Evo >= 3 && c.Due(TBelt))
            {
                int t = c.Nearest(c.Range);
                if (t >= 0) { Belt(ref c, t); c.Reset(TBelt, c.Period(5f)); }
            }
        }

        void Digit(ref Ctx c, int target)
        {
            var dir = Rotate(c.DirTo(target), ((c.S.Counters[CLeft] % 3) - 1) * 4f * Deg);
            var s = c.NewShot(AllySprite.Digit, dir, c.Stat(StatId.ProjectileSpeed), c.Dmg(), 0.22f, c.Range + 1f);
            if (c.Base >= 3) { s.Ricochets = c.Base >= 5 ? 2 : 1; s.RicochetRange = 4f; }
            if (c.Evolved) { s.Status = ShotStatus.Mark; s.StatusTime = 3f; }
            c.Fire(s);
            c.Sound(0);
        }

        void Expression(ref Ctx c)
        {
            int t = c.Nearest(c.Range + 2f);
            if (t < 0) return;
            var s = c.NewShot(AllySprite.Expression, c.DirTo(t), c.Speed(13f), c.Dmg(2.2f), 0.3f, c.Range + 2f, 5);
            c.Fire(s);
            c.Sound(1);
        }

        void Sushi(ref Ctx c, int target)
        {
            var aim = c.W.Enemies.Pos[target];
            var dir = c.DirTo(target);
            int n = c.Evo >= 2 ? 2 : 1;
            for (int k = 0; k < n; k++)
            {
                float side = (k % 2 == 0 ? 1 : -1) * 55f * Deg;
                var s = c.NewShot(AllySprite.Sushi, Rotate(dir, side), c.Speed(9f), c.Dmg(2f), 0.3f, c.Range * 1.4f, 2);
                s.Motion = ShotMotion.Curve;
                s.Curve = 4f;
                s.Aim = aim;
                s.Steering = true;
                MarkBurst(ref c, ref s);
                c.Fire(s);
            }
            c.Sound(3);
        }

        static void MarkBurst(ref Ctx c, ref Shot s)
        {
            s.Status = ShotStatus.ConsumeMark;
            s.BurstRadius = c.Radius(1.5f);
            s.BurstDamage = c.Dmg(2.5f);
        }

        void Belt(ref Ctx c, int target)
        {
            var dir = c.DirTo(target);
            for (int k = 0; k < 6; k++)
                c.Strike(c.Pos + dir * (1.5f + k * 1.3f), 0.2f + k * 0.09f, c.Radius(1.0f), c.Dmg(2f), FxStyle.Food);
            c.Sound(4);
        }
    }
}
