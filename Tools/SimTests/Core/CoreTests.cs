using System;

namespace ES.SimTests.Core
{
    public class CoreTests
    {
        [Test]
        public void Rng_SameSeed_SameSequence()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 1000; i++) Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));
        }

        [Test]
        public void Rng_DifferentSeeds_DifferentSequences()
        {
            var a = new Rng(1);
            var b = new Rng(2);
            int equal = 0;
            for (int i = 0; i < 100; i++) if (a.NextUInt() == b.NextUInt()) equal++;
            Assert.That(equal, Is.LessThan(5));
        }

        [Test]
        public void Rng_Range_StaysInBounds()
        {
            var r = new Rng(1);
            for (int i = 0; i < 10000; i++)
            {
                int v = r.Range(3, 7);
                Assert.That(v, Is.InRange(3, 6));
                float f = r.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        [Test]
        public void Quant_Pos_RoundTripErrorWithin1Over1024()
        {
            for (float v = 0; v <= 64; v += 0.0137f)
                Assert.That(Math.Abs(Quant.UnPos(Quant.Pos(v, 0, 64), 0, 64) - v), Is.LessThanOrEqualTo(1f / 1024));
        }

        [Test]
        public void Quant_Pos_ClampsOutsideRange()
        {
            Assert.That(Quant.Pos(-5f, 0, 64), Is.EqualTo(0));
            Assert.That(Quant.Pos(99f, 0, 64), Is.EqualTo(ushort.MaxValue));
        }

        [Test]
        public void Quant_Axis_ClampsAndRoundTrips()
        {
            Assert.That(Quant.Axis(2f), Is.EqualTo(127));
            Assert.That(Quant.Axis(-2f), Is.EqualTo(-127));
            Assert.That(Math.Abs(Quant.UnAxis(Quant.Axis(0.5f)) - 0.5f), Is.LessThan(0.01f));
        }

        [Test]
        public void Vec2_ClampLength_LimitsToMax()
        {
            Assert.That(Vec2.ClampLength(new Vec2(3, 4), 1).Length(), Is.EqualTo(1).Within(1e-5));
            Assert.That(Vec2.ClampLength(new Vec2(0.3f, 0.4f), 1).Length(), Is.EqualTo(0.5f).Within(1e-5));
        }

        [Test]
        public void Vec2_Normalized_ZeroStaysZero()
        {
            var n = new Vec2(0, 0).Normalized();
            Assert.That(n.X, Is.EqualTo(0));
            Assert.That(n.Y, Is.EqualTo(0));
        }
    }
}
