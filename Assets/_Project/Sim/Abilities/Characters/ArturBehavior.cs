using System;

namespace ES.Sim
{
    /// <summary>
    /// Artur: socos em cone no inimigo mais próximo; a cada 4º soco (3º no nível 5) um golpe carregado
    /// com onda de choque. Nível 3: socos interrompem por um instante.
    /// Evolução: pílulas em rajada e seringas perfurantes (a cada 3ª, uma especial que explode).
    /// Avançada: o golpe carregado dispara um anel de seringas.
    /// </summary>
    public sealed class ArturBehavior : CharacterWeapon
    {
        const int TPunch = 0, TPills = 1, TSyringe = 2;
        const int CCombo = 0, CSyringe = 1;

        protected override void Step(ref Ctx c, float dt)
        {
            if (c.Due(TPunch))
            {
                float reach = c.Range;
                int t = c.Nearest(reach + 0.8f);
                if (t >= 0)
                {
                    Punch(ref c, c.DirTo(t), reach);
                    c.Reset(TPunch, c.Stat(StatId.Cooldown));
                }
            }
            if (!c.Evolved) return;

            if (c.Due(TPills))
            {
                int t = c.Nearest(9f);
                if (t >= 0)
                {
                    var dir = c.DirTo(t);
                    for (int k = 0; k < 4; k++)
                        c.Fire(c.NewShot(AllySprite.Pill, Rotate(dir, (k - 1.5f) * 11f * Deg), c.Speed(13f), c.Dmg(0.6f), 0.18f, 8f));
                    c.Sound(2);
                    c.Reset(TPills, c.Period(1.4f));
                }
            }
            if (c.Due(TSyringe))
            {
                int t = c.Nearest(10f);
                if (t >= 0)
                {
                    var dir = c.DirTo(t);
                    int n = c.Evo >= 2 ? 2 : 1;
                    for (int k = 0; k < n; k++) Syringe(ref c, Rotate(dir, (k - (n - 1) * 0.5f) * 8f * Deg), c.Every(CSyringe, 3));
                    c.Sound(3);
                    c.Reset(TSyringe, c.Period(2f));
                }
            }
        }

        void Punch(ref Ctx c, Vec2 dir, float reach)
        {
            float knock = c.Stat(StatId.Knockback);
            int every = c.Base >= 5 ? 3 : 4;
            if (c.Every(CCombo, every))
            {
                Area.Cone(c.W, c.Slot, c.Ability, c.Pos, dir, reach * 1.3f, 110f * Deg, c.Dmg(2.2f), knock + 2f, 0.5f, FxStyle.Impact);
                Area.Burst(c.W, c.Slot, c.Ability, c.Pos, c.Radius(c.Base >= 5 ? 3.0f : 2.4f), c.Dmg(1.2f), knock + 1f, 0.3f, FxStyle.Impact);
                c.Sound(1);
                if (c.Evo >= 3)
                    for (int k = 0; k < 8; k++) Syringe(ref c, Rotate(dir, k * 45f * Deg), false);
            }
            else
            {
                Area.Cone(c.W, c.Slot, c.Ability, c.Pos, dir, reach, 75f * Deg, c.Dmg(), knock, c.Base >= 3 ? 0.25f : 0f, FxStyle.Impact);
                c.Sound(0);
            }
        }

        void Syringe(ref Ctx c, Vec2 dir, bool special)
        {
            var s = c.NewShot(special ? AllySprite.SyringeSpecial : AllySprite.Syringe, dir, c.Speed(16f), c.Dmg(special ? 1.6f : 1.3f), 0.2f, 10f, special ? 1 : 6);
            if (special)
            {
                s.BurstWhen = ShotBurst.OnEnd;
                s.BurstRadius = c.Radius(1.4f);
                s.BurstDamage = c.Dmg(1.0f);
                s.BurstStyle = FxStyle.Energy;
            }
            c.Fire(s);
        }
    }
}
