using System;
using System.Collections.Generic;
using System.Linq;

namespace ES.SimTests.WorldTests
{
    public class GridTests
    {
        [Test]
        public void Grid_Query_MatchesBruteForce()
        {
            var rng = new Rng(7);
            var grid = new SpatialGrid(64, 64, 2, 1024);
            var pts = new Vec2[500];
            for (int i = 0; i < pts.Length; i++)
            {
                pts[i] = new Vec2(rng.Range(0f, 64f), rng.Range(0f, 64f));
                grid.Insert(i, pts[i]);
            }
            var results = new int[600];
            for (int q = 0; q < 200; q++)
            {
                var c = new Vec2(rng.Range(-2f, 66f), rng.Range(-2f, 66f));
                float r = rng.Range(0.1f, 9f);
                int n = grid.Query(c, r, results);
                var got = new HashSet<int>(results.Take(n));
                var expected = new HashSet<int>(Enumerable.Range(0, pts.Length).Where(i => Vec2.DistanceSq(pts[i], c) <= r * r));
                Assert.That(got.SetEquals(expected), $"consulta {q}: {got.Count} vs {expected.Count}");
            }
        }

        [Test]
        public void Grid_Clear_Empties()
        {
            var grid = new SpatialGrid(64, 64, 2, 16);
            grid.Insert(0, new Vec2(10, 10));
            grid.Clear();
            Assert.That(grid.Query(new Vec2(10, 10), 5, new int[4]), Is.EqualTo(0));
        }

        [Test]
        public void Grid_Query_NoAllocation()
        {
            var grid = new SpatialGrid(64, 64, 2, 1024);
            var rng = new Rng(3);
            for (int i = 0; i < 400; i++) grid.Insert(i, new Vec2(rng.Range(0f, 64f), rng.Range(0f, 64f)));
            var results = new int[512];
            grid.Query(new Vec2(32, 32), 6, results);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) grid.Query(new Vec2(i % 64, (i * 7) % 64), 6, results);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [Test]
        public void EventBuffer_CosmeticCappedAt48()
        {
            var buf = new EventBuffer(48);
            for (int i = 0; i < 100; i++) buf.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.Hit });
            Assert.That(buf.Cosmetic.Count, Is.EqualTo(48));
            buf.Clear();
            Assert.That(buf.Cosmetic.Count, Is.EqualTo(0));
        }
    }
}
