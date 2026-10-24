using System;
using System.Linq;

namespace ES.SimTests.WorldTests
{
    public class ArenaTests
    {
        static readonly ArenaDef Open64 = new ArenaDef { id = "t", width = 64, height = 64, obstacles = new RectDef[0] };

        static readonly ArenaDef WithBox = new ArenaDef
        {
            id = "box", width = 64, height = 64,
            obstacles = new[] { new RectDef { x = 10, y = 10, w = 4, h = 2 } },
        };

        static bool Overlaps(Vec2 p, float r, RectDef o)
        {
            float cx = Math.Clamp(p.X, o.x, o.x + o.w), cy = Math.Clamp(p.Y, o.y, o.y + o.h);
            float dx = p.X - cx, dy = p.Y - cy;
            return dx * dx + dy * dy < r * r - 1e-4f;
        }

        [Test]
        public void Arena_PushesOutOfObstacle_FromEdge()
        {
            var p = ArenaCollision.Resolve(new Vec2(12, 9.8f), 0.5f, WithBox);
            Assert.That(Overlaps(p, 0.5f, WithBox.obstacles[0]), Is.False, p.ToString());
        }

        [Test]
        public void Arena_PushesOutOfObstacle_FromInside()
        {
            var p = ArenaCollision.Resolve(new Vec2(11, 10.5f), 0.5f, WithBox);
            Assert.That(Overlaps(p, 0.5f, WithBox.obstacles[0]), Is.False, p.ToString());
        }

        [Test]
        public void Arena_KeepsInsideBounds()
        {
            var p = ArenaCollision.Resolve(new Vec2(-5, 70), 0.5f, Open64);
            Assert.That(p.X, Is.GreaterThanOrEqualTo(0.5f));
            Assert.That(p.Y, Is.LessThanOrEqualTo(63.5f));
        }

        [Test]
        public void Arena_FreePointUnchanged()
        {
            var p = ArenaCollision.Resolve(new Vec2(30, 30), 0.5f, WithBox);
            Assert.That(p, Is.EqualTo(new Vec2(30, 30)));
        }

        [Test]
        public void World_New_SpawnsRosterPlayersActive()
        {
            var w = TestWorlds.Create(team: 3);
            var active = w.Players.Slots.Where(s => s != null && s.Status == PlayerStatus.Active).ToArray();
            Assert.That(active.Length, Is.EqualTo(3));
            foreach (var p in active)
            {
                Assert.That(p.Hp, Is.EqualTo(100));
                Assert.That(Vec2.DistanceSq(p.Pos, new Vec2(32, 32)), Is.LessThan(9f));
            }
            Assert.That(w.Balance.TeamSize, Is.EqualTo(3));
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Running));
        }

        [Test]
        public void EnemyStore_SpawnAndRemove_KeepsIdsUnique()
        {
            var s = new EnemyStore(4);
            int a = s.Spawn(0, new Vec2(1, 1));
            int b = s.Spawn(0, new Vec2(2, 2));
            Assert.That(s.Id[a], Is.Not.EqualTo(s.Id[b]));
            s.RemoveAt(a);
            Assert.That(s.Count, Is.EqualTo(1));
            Assert.That(s.Pos[0], Is.EqualTo(new Vec2(2, 2)));
            s.Spawn(0, default); s.Spawn(0, default); s.Spawn(0, default);
            Assert.That(s.Spawn(0, default), Is.EqualTo(-1), "cheio");
        }
    }
}
