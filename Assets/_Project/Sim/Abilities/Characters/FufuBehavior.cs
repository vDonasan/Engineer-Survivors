using System;

namespace ES.Sim
{
    /// <summary>
    /// Fufu: socos em arco largo que empurram e abrem espaço. Socos que acertam acumulam fúria
    /// (−6% de recarga por acúmulo, some depois de 3 s sem acertar); a cada 5º soco um golpe carregado
    /// solta uma onda de impacto. Evolução: seringas de insulina perfurantes e frascos que estouram.
    /// Avançada: com a fúria no máximo, cada soco dispara uma rajada de seringas.
    /// </summary>
    public sealed class FufuBehavior : CharacterWeapon
    {
        const int TPunch = 0, TFury = 1, TSyringe = 2, TVial = 3;
        const int CFury = 0, CPunch = 1;

        protected override void Step(ref Ctx c, float dt)
        {
            int maxFury = c.Base >= 4 ? 8 : 5;
            if (c.Due(TFury)) c.S.Counters[CFury] = 0;

            if (c.Due(TPunch))
            {
                float reach = c.Range;
                int t = c.Nearest(reach + 0.8f);
                if (t >= 0)
                {
                    var dir = c.DirTo(t);
                    Punch(ref c, dir, reach, maxFury);
                    float fury = Math.Min(c.S.Counters[CFury], maxFury);
                    c.Reset(TPunch, c.Stat(StatId.Cooldown) * (1f - 0.06f * fury));
                }
            }
            if (!c.Evolved) return;

            if (c.Due(TSyringe))
            {
                int t = c.Nearest(10f);
                if (t >= 0)
                {
                    Syringes(ref c, c.DirTo(t), 2, 8f);
                    c.Sound(2);
                    c.Reset(TSyringe, c.Period(1.4f));
                }
            }
            if (c.Due(TVial))
            {
                int t = Area.Densest(c.W, c.Pos, 8f, 1.6f);
                if (t >= 0)
                {
                    var s = c.NewShot(AllySprite.Vial, c.DirTo(t), c.Speed(8f), c.Dmg(1.0f), 0.3f, 8f);
                    s.BurstWhen = ShotBurst.OnEnd;
                    s.BurstRadius = c.Radius(1.6f);
                    s.BurstDamage = c.Dmg(1.3f);
                    s.BurstStyle = FxStyle.Insulin;
                    c.Fire(s);
                    c.Sound(3);
                    c.Reset(TVial, c.Period(2.2f));
                }
            }
        }

        void Punch(ref Ctx c, Vec2 dir, float reach, int maxFury)
        {
            float arc = (c.Base >= 3 ? 140f : 110f) * Deg;
            float knock = c.Stat(StatId.Knockback) + (c.Base >= 3 ? 1f : 0f);
            bool charged = c.Every(CPunch, 5);
            int hit = Area.Cone(c.W, c.Slot, c.Ability, c.Pos, dir, reach, arc, c.Dmg(charged ? 1.5f : 1f), knock, 0f, FxStyle.Impact);
            if (charged)
            {
                Area.Burst(c.W, c.Slot, c.Ability, c.Pos, c.Radius(c.Base >= 5 ? 3.2f : 2.6f), c.Dmg(1.6f), knock + 1f, c.Base >= 5 ? 0.4f : 0f, FxStyle.Impact);
                c.Sound(1);
            }
            else c.Sound(0);
            if (hit <= 0) return;
            if (c.S.Counters[CFury] < maxFury) c.S.Counters[CFury]++;
            c.Reset(TFury, 3f);
            if (c.Evo >= 3 && c.S.Counters[CFury] >= maxFury) Syringes(ref c, dir, 3, 14f);
        }

        void Syringes(ref Ctx c, Vec2 dir, int n, float spreadDeg)
        {
            for (int k = 0; k < n; k++)
                c.Fire(c.NewShot(AllySprite.Insulin, Rotate(dir, (k - (n - 1) * 0.5f) * spreadDeg * Deg), c.Speed(15f), c.Dmg(0.9f), 0.2f, 10f, 5));
        }
    }
}
