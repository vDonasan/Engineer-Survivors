namespace ES.SimTests.Systems
{
    public class MovementTests
    {
        static readonly ArenaDef Open = new ArenaDef { id = "o", width = 64, height = 64, obstacles = new RectDef[0] };
        const float Dt = 1f / 30f;

        [Test]
        public void Integrate_FullInput_MovesAtSpeed()
        {
            var p = new Vec2(32, 32);
            for (int i = 0; i < 30; i++) p = MovementSystem.Integrate(p, 127, 0, 4.5f, Dt, Open, 0.4f);
            Assert.That(p.X - 32, Is.EqualTo(4.5f).Within(0.01f));
            Assert.That(p.Y, Is.EqualTo(32f));
        }

        [Test]
        public void Integrate_DiagonalNotFaster()
        {
            var p = new Vec2(32, 32);
            for (int i = 0; i < 30; i++) p = MovementSystem.Integrate(p, 127, 127, 4.5f, Dt, Open, 0.4f);
            Assert.That((p - new Vec2(32, 32)).Length(), Is.LessThanOrEqualTo(4.51f));
        }

        [Test]
        public void Integrate_StopsAtWall()
        {
            var p = new Vec2(1, 32);
            for (int i = 0; i < 30; i++) p = MovementSystem.Integrate(p, -127, 0, 4.5f, Dt, Open, 0.4f);
            Assert.That(p.X, Is.EqualTo(0.4f).Within(1e-4));
        }

        [Test]
        public void World_Step_AppliesInputToActivePlayer()
        {
            var w = TestWorlds.CreateOpen();
            var start = w.Players[0].Pos;
            var inputs = TestWorlds.NoInput();
            for (int i = 0; i < 30; i++)
            {
                inputs[0] = new PlayerInput { Seq = (ushort)(i + 1), MoveY = 127, ChoiceIndex = -1 };
                w.Step(inputs);
            }
            Assert.That(w.Players[0].Pos.Y - start.Y, Is.EqualTo(4.5f).Within(0.02f));
            Assert.That(w.Players[0].Facing.Y, Is.GreaterThan(0.9f));
        }
    }

    public class InputValidatorTests
    {
        static PlayerInput In(ushort seq, sbyte x = 0, sbyte y = 0, byte special = 0) =>
            new PlayerInput { Seq = seq, MoveX = x, MoveY = y, SpecialCounter = special, ChoiceIndex = -1 };

        [Test]
        public void Validator_ClampsOversizedVector()
        {
            var v = new InputValidator();
            Assert.That(v.Accept(0, In(1, 127, 127), 0, out var s), Is.True);
            var len = new Vec2(Quant.UnAxis(s.MoveX), Quant.UnAxis(s.MoveY)).Length();
            Assert.That(len, Is.LessThanOrEqualTo(1.001f));
            Assert.That(len, Is.GreaterThan(0.98f));
        }

        [Test]
        public void Validator_RejectsOldSeq_WithWraparound()
        {
            var v = new InputValidator();
            Assert.That(v.Accept(0, In(65535), 0, out _), Is.True);
            Assert.That(v.Accept(0, In(0), 0.01, out _), Is.True, "0 depois de 65535 é novo");
            Assert.That(v.Accept(1, In(20), 0, out _), Is.True);
            Assert.That(v.Accept(1, In(10), 0.01, out _), Is.False, "10 depois de 20 é velho");
            Assert.That(v.Accept(1, In(20), 0.02, out _), Is.False, "repetido");
        }

        [Test]
        public void Validator_IgnoresRegressingSpecialCounter()
        {
            var v = new InputValidator();
            v.Accept(0, In(1, special: 5), 0, out var a);
            Assert.That(a.SpecialCounter, Is.EqualTo(5));
            v.Accept(0, In(2, special: 3), 0.01, out var b);
            Assert.That(b.SpecialCounter, Is.EqualTo(5));
            v.Accept(0, In(3, special: 6), 0.02, out var c);
            Assert.That(c.SpecialCounter, Is.EqualTo(6));
        }

        [Test]
        public void Validator_RateLimit60PerSecond()
        {
            var v = new InputValidator();
            int accepted = 0;
            for (int i = 0; i < 100; i++) if (v.Accept(0, In((ushort)(i + 1)), 5.0 + i * 0.001, out _)) accepted++;
            Assert.That(accepted, Is.EqualTo(60));
            Assert.That(v.Accept(0, In(500), 6.01, out _), Is.True, "novo segundo libera");
        }

        [Test]
        public void Validator_KickAfter50Malformed()
        {
            var v = new InputValidator();
            for (int i = 0; i < 49; i++) v.CountMalformed(2);
            Assert.That(v.ShouldKick(2), Is.False);
            v.CountMalformed(2);
            Assert.That(v.ShouldKick(2), Is.True);
            Assert.That(v.ShouldKick(1), Is.False);
        }
    }
}
