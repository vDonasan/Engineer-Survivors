using ES.Sim;
using NUnit.Framework;

namespace ES.Tests
{
    public class SanityTests
    {
        [Test]
        public void Sanity_SimAssemblyLoads()
        {
            var a = new Rng(1);
            var b = new Rng(1);
            Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));
        }
    }
}
