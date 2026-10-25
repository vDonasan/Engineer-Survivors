namespace ES.Sim
{
    /// <summary>
    /// Staut: bolhas que flutuam ondulando e estouram em área. Nível 3: a cada 3ª rajada uma bolha
    /// grande prende o alvo (atordoa + dano contínuo); nível 5: a bolha grande atravessa um inimigo.
    /// Evolução: maletas de dinheiro perfurantes que abrem em moedas; avançada: moedas ricocheteiam.
    /// </summary>
    public sealed class StautBehavior : CharacterWeapon
    {
        const int TBubble = 0, TCase = 1;
        const int CVolley = 0;
        const byte VBubble = 0, VBig = 1, VCase = 2, VCoin = 3;

        protected override void Step(ref Ctx c, float dt)
        {
            if (c.Due(TBubble))
            {
                int target = c.Nearest(c.Range);
                if (target >= 0)
                {
                    Bubbles(ref c, target);
                    c.Reset(TBubble, c.Stat(StatId.Cooldown));
                }
            }
            if (c.Evolved && c.Due(TCase))
            {
                int target = c.Nearest(c.Range + 2f);
                if (target >= 0)
                {
                    Briefcases(ref c, target);
                    c.Reset(TCase, c.Period(c.Param("caseEvery", 2.2f)));
                }
            }
        }

        void Bubbles(ref Ctx c, int target)
        {
            var dir = c.DirTo(target);
            int count = System.Math.Max(1, (int)c.Stat(StatId.Count));
            float speed = c.Stat(StatId.ProjectileSpeed);
            float life = c.Param("bubbleLife", 3f);
            for (int k = 0; k < count; k++)
            {
                float off = (k - (count - 1) * 0.5f) * 22f * Deg;
                var s = c.NewShot(AllySprite.Bubble, Rotate(dir, off), speed, c.Dmg(), 0.32f, speed * life);
                s.Motion = ShotMotion.Wave;
                s.Curve = 0.35f;
                s.Frequency = 0.8f + 0.15f * k;
                s.BurstWhen = ShotBurst.OnHit;
                s.BurstRadius = c.Radius(0.9f);
                s.BurstDamage = c.Dmg(0.5f);
                s.BurstStyle = FxStyle.Pop;
                s.Variant = VBubble;
                c.Fire(s);
            }
            c.Sound(0);

            if (c.Base >= 3 && c.Every(CVolley, 3))
            {
                var big = c.NewShot(AllySprite.BubbleBig, dir, speed * 0.8f, c.Dmg(0.8f), 0.55f, speed * life, c.Base >= 5 ? 2 : 1);
                big.Motion = ShotMotion.Wave;
                big.Curve = 0.25f;
                big.Frequency = 0.6f;
                big.Status = ShotStatus.Trap;
                big.StatusTime = c.Time(1.6f);
                big.StatusValue = c.Dmg(0.7f);
                big.BurstWhen = ShotBurst.OnEnd;
                big.BurstRadius = c.Radius(1.3f);
                big.BurstDamage = c.Dmg(0.8f);
                big.BurstStyle = FxStyle.Pop;
                big.Variant = VBig;
                c.Fire(big);
                c.Sound(1);
            }
        }

        void Briefcases(ref Ctx c, int target)
        {
            var dir = c.DirTo(target);
            int n = c.Evo >= 2 ? 2 : 1;
            for (int k = 0; k < n; k++)
            {
                float off = (k - (n - 1) * 0.5f) * 14f * Deg;
                var s = c.NewShot(AllySprite.Briefcase, Rotate(dir, off), c.Speed(9f), c.Dmg(2.4f), 0.42f, 9f, 3);
                s.Knockback = 2f;
                s.Variant = VCase;
                c.Fire(s);
            }
            c.Sound(2);
        }

        public override void OnShotEnd(World w, ref Shot s)
        {
            if (s.Variant != VCase) return;
            var a = InstanceOf(w, s.Owner, s.Ability);
            if (a == null) return;
            var c = new Ctx(w, s.Owner, a, (WeaponState)a.State);
            // a maleta abre: estouro de dinheiro e moedas em leque
            Area.Burst(w, s.Owner, s.Ability, s.Pos, c.Radius(1.3f), c.Dmg(0.8f), 1f, 0f, FxStyle.Coins);
            int coins = c.Evo >= 2 ? 7 : 5;
            for (int k = 0; k < coins; k++)
            {
                var dir = Rotate(new Vec2(1, 0), k * 6.2831853f / coins + s.Age);
                var coin = new Shot
                {
                    Sprite = AllySprite.Coin, Pos = s.Pos, Vel = dir * 6f, Damage = c.Dmg(0.6f), Radius = 0.22f,
                    Life = 0.55f, Pierce = 1, Variant = VCoin,
                };
                if (c.Evo >= 3) { coin.Ricochets = 2; coin.RicochetRange = 3.5f; }
                SpawnChild(w, s, coin);
            }
            c.Sound(3, s.Pos);
        }
    }
}
