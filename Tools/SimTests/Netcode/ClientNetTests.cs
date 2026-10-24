using System;

namespace ES.SimTests.Netcode
{
    public class ClientNetTests
    {
        const double Dt = 1.0 / 30.0;
        static readonly ArenaDef Open = new ArenaDef { id = "o", width = 64, height = 64, obstacles = new RectDef[0] };
        static RulesDef Rules => TestWorlds.Db.Def.rules;

        [Test]
        public void Interp_SteadyArrivals_Delay100ms()
        {
            var b = new InterpBuffer(0.1, 0.2);
            for (int tick = 0; tick < 300; tick += 2) b.Push(tick, tick * Dt + 0.05);
            Assert.That(b.Delay, Is.EqualTo(0.1).Within(0.005));
        }

        [Test]
        public void Interp_Jitter_IncreasesDelayUpTo200ms()
        {
            var rng = new Rng(4);
            var b = new InterpBuffer(0.1, 0.2);
            for (int tick = 0; tick < 300; tick += 2) b.Push(tick, tick * Dt + 0.05 + rng.NextFloat() * 0.06);
            Assert.That(b.Delay, Is.GreaterThan(0.12).And.LessThanOrEqualTo(0.2));

            var big = new InterpBuffer(0.1, 0.2);
            for (int tick = 0; tick < 300; tick += 2) big.Push(tick, tick * Dt + 0.05 + rng.NextFloat() * 0.5);
            Assert.That(big.Delay, Is.EqualTo(0.2).Within(1e-9));
        }

        [Test]
        public void Interp_Sample_BracketsRenderTime()
        {
            var b = new InterpBuffer(0.1, 0.2);
            for (int tick = 0; tick <= 60; tick += 2) b.Push(tick, tick * Dt + 0.05);
            // agora = chegada do tick 60; render = 60·Dt − 0,1 s → tick 57
            Assert.That(b.TrySample(60 * Dt + 0.05, out int from, out int to, out float t), Is.True);
            Assert.That(from, Is.EqualTo(56));
            Assert.That(to, Is.EqualTo(58));
            Assert.That(t, Is.EqualTo(0.5f).Within(0.02f));
        }

        [Test]
        public void Interp_BeyondLatest_HoldsLatest()
        {
            var b = new InterpBuffer(0.1, 0.2);
            for (int tick = 0; tick <= 20; tick += 2) b.Push(tick, tick * Dt + 0.05);
            Assert.That(b.TrySample(10.0, out int from, out int to, out float t), Is.True);
            Assert.That(from, Is.EqualTo(20));
            Assert.That(to, Is.EqualTo(20));
            Assert.That(t, Is.EqualTo(0f));
        }

        static PlayerInput Move(ushort seq, sbyte x) => new PlayerInput { Seq = seq, MoveX = x, ChoiceIndex = -1 };

        [Test]
        public void Prediction_NoLatency_MatchesServer()
        {
            var cp = new ClientPrediction(Rules, Open);
            cp.Reset(new Vec2(32, 32));
            var server = new Vec2(32, 32);
            for (ushort s = 1; s <= 30; s++)
            {
                var input = Move(s, (sbyte)(s % 2 == 0 ? 127 : -40));
                cp.Record(input, 4.5f);
                server = MovementSystem.Integrate(server, input.MoveX, input.MoveY, 4.5f, (float)Dt, Open, Rules.playerRadius);
                cp.Reconcile(server, s, 4.5f);
                Assert.That((cp.Predicted - server).Length(), Is.LessThan(1e-4f));
            }
        }

        [Test]
        public void Prediction_ReplaysUnackedInputs()
        {
            var cp = new ClientPrediction(Rules, Open);
            cp.Reset(new Vec2(32, 32));
            for (ushort s = 1; s <= 15; s++) cp.Record(Move(s, 127), 4.5f);
            // O servidor processou até a 10 mas foi empurrado 0,5 u em Y.
            var server = new Vec2(32 + 10 * 4.5f * (float)Dt, 32.5f);
            cp.Reconcile(server, 10, 4.5f);
            Assert.That(cp.Predicted.X, Is.EqualTo(32 + 15 * 4.5f * (float)Dt).Within(1e-3));
            Assert.That(cp.Predicted.Y, Is.EqualTo(32.5f).Within(1e-4));
        }

        [Test]
        public void Prediction_SmallError_Ignored()
        {
            var cp = new ClientPrediction(Rules, Open);
            cp.Reset(new Vec2(32, 32));
            cp.Record(Move(1, 127), 4.5f);
            var before = cp.Predicted;
            cp.Reconcile(before + new Vec2(0.03f, 0), 1, 4.5f);
            Assert.That(cp.Predicted, Is.EqualTo(before));
            Assert.That(cp.RenderOffset.Length(), Is.EqualTo(0));
        }

        [Test]
        public void Prediction_LargeError_SmoothedOver100ms()
        {
            var cp = new ClientPrediction(Rules, Open);
            cp.Reset(new Vec2(32, 32));
            cp.Record(Move(1, 0), 4.5f);
            var before = cp.Predicted;
            cp.Reconcile(before + new Vec2(1f, 0), 1, 4.5f);
            Assert.That(cp.Predicted.X, Is.EqualTo(before.X + 1f).Within(1e-4));
            Assert.That((cp.Predicted + cp.RenderOffset - before).Length(), Is.LessThan(1e-4f), "na hora, o desenho não salta");
            cp.Tick(0.05f);
            Assert.That(cp.RenderOffset.Length(), Is.InRange(0.3f, 0.7f));
            cp.Tick(0.06f);
            Assert.That(cp.RenderOffset.Length(), Is.LessThan(0.01f));
        }
    }
}
