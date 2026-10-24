using System;
using System.Collections.Generic;
using System.Linq;
using D = ES.Sim.Difficulty;

namespace ES.SimTests.Systems
{
    public class SpawnTests
    {
        static SpawnDef Spawn => ContentLoader.LoadSp1().spawn;
        static MatchBalance B(D d, int n) => BalanceResolver.Resolve(ContentLoader.LoadSp1().balance, d, n);

        static void MakeImmortal(World w)
        {
            foreach (var p in w.Players.Slots) if (p != null) p.InvulnUntil = float.MaxValue;
        }

        [Test]
        public void Budget_Formula_NormalSolo_At5Min()
        {
            Assert.That(SpawnDirector.Budget(Spawn, B(D.Normal, 1), 300f), Is.EqualTo(1.2f + 0.55f * 5).Within(1e-4));
        }

        [Test]
        public void Budget_Hard4Players()
        {
            float expected = (1.2f + 0.55f * 1.2f * 5f) * 1.25f * 2.2f;
            Assert.That(SpawnDirector.Budget(Spawn, B(D.Hard, 4), 300f), Is.EqualTo(expected).Within(1e-3));
        }

        [Test]
        public void Bracket_ShiftHardAnticipates()
        {
            var br = TestWorlds.Db.Brackets;
            Assert.That(br[SpawnDirector.BracketIndexAt(br, 3.0f, 1.5f)].FromMinute, Is.EqualTo(4));
            Assert.That(br[SpawnDirector.BracketIndexAt(br, 3.0f, 0f)].FromMinute, Is.EqualTo(2));
            Assert.That(br[SpawnDirector.BracketIndexAt(br, 2.5f, -1f)].FromMinute, Is.EqualTo(0));
            Assert.That(br[SpawnDirector.BracketIndexAt(br, 99f, 0f)].FromMinute, Is.EqualTo(7));
        }

        [Test]
        public void Spawn_NeverExceedsCap_Hard4_600s()
        {
            var w = TestWorlds.Create(D.Hard, 4);
            MakeImmortal(w);
            var inputs = TestWorlds.NoInput();
            int peak = 0;
            int ticks = (int)(600 * w.Rules.tickRate);
            for (int t = 0; t < ticks; t++)
            {
                w.Step(inputs);
                Assert.That(w.Enemies.Count, Is.LessThanOrEqualTo(400));
                peak = Math.Max(peak, w.Enemies.Count);
            }
            Assert.That(peak, Is.GreaterThanOrEqualTo(300), "o teste precisa estressar o teto");
        }

        [Test]
        public void Spawn_OutsideCameraRadius()
        {
            var w = TestWorlds.Create(D.Hard, 1, seed: 9);
            MakeImmortal(w);
            var seen = new HashSet<ushort>();
            var inputs = TestWorlds.NoInput();
            for (int t = 0; t < 30 * 120; t++)
            {
                w.Step(inputs);
                for (int i = 0; i < w.Enemies.Count; i++)
                {
                    if (!seen.Add(w.Enemies.Id[i])) continue;
                    float d = (w.Enemies.Pos[i] - w.Players[0].Pos).Length();
                    Assert.That(d, Is.GreaterThan(12f), $"tick {t}");
                }
            }
            Assert.That(seen.Count, Is.GreaterThan(50));
        }

        [Test]
        public void Spawn_EliteHasTripleHp()
        {
            var def = ContentLoader.LoadSp1();
            def.balance.overrides = new[] { new CellOverrideDef { difficulty = "normal", teamSize = 1, param = "eliteChance", value = 1f } };
            var w = TestWorlds.Create(db: GameDatabase.Build(def).Db);
            MakeImmortal(w);
            var inputs = TestWorlds.NoInput();
            for (int t = 0; t < 60 && w.Enemies.Count == 0; t++) w.Step(inputs);
            Assert.That(w.Enemies.Count, Is.GreaterThan(0));
            Assert.That(w.Enemies.Flags[0] & EnemyFlags.Elite, Is.Not.Zero);
            Assert.That(w.Enemies.MaxHp[0], Is.EqualTo(10f * 3f).Within(1e-3));
        }

        [Test]
        public void Composition_FirstTwoMinutesOnlySwarm()
        {
            var w = TestWorlds.Create(D.Normal, 1, seed: 4);
            MakeImmortal(w);
            int swarm = w.Db.EnemyIndex("microbo_enxame");
            var inputs = TestWorlds.NoInput();
            for (int t = 0; t < 30 * 110; t++)
            {
                w.Step(inputs);
                for (int i = 0; i < w.Enemies.Count; i++) Assert.That(w.Enemies.Type[i], Is.EqualTo(swarm));
            }
        }

        [Test]
        public void Surge_SpawnsSwarmRingAt60s()
        {
            var w = TestWorlds.Create(D.Normal, 1, seed: 2);
            MakeImmortal(w);
            var inputs = TestWorlds.NoInput();
            while (w.Elapsed < 59.9f) w.Step(inputs);
            int before = w.Enemies.Count;
            w.Step(inputs); w.Step(inputs); w.Step(inputs);
            Assert.That(w.Enemies.Count - before, Is.GreaterThanOrEqualTo(20));
        }
    }

    public class EnemyAITests
    {
        static World OpenWorld()
        {
            var w = TestWorlds.CreateOpen();
            w.SpawningEnabled = false;
            w.Players[0].InvulnUntil = float.MaxValue;
            return w;
        }

        [Test]
        public void Chase_MovesTowardPlayer()
        {
            var w = OpenWorld();
            int e = SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex("microbo_enxame"), w.Players[0].Pos + new Vec2(10, 0), false);
            float before = (w.Enemies.Pos[e] - w.Players[0].Pos).Length();
            for (int t = 0; t < 30; t++) w.Step(TestWorlds.NoInput());
            float after = (w.Enemies.Pos[0] - w.Players[0].Pos).Length();
            Assert.That(before - after, Is.EqualTo(3.0f).Within(0.1f));
        }

        [Test]
        public void Kite_KeepsDistanceWindow()
        {
            var w = OpenWorld();
            SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex("torreta_inspecao"), w.Players[0].Pos + new Vec2(15, 0), false);
            for (int t = 0; t < 30 * 8; t++) w.Step(TestWorlds.NoInput());
            float d = (w.Enemies.Pos[0] - w.Players[0].Pos).Length();
            Assert.That(d, Is.InRange(5.4f, 7.1f));
        }

        [Test]
        public void Kite_TelegraphsThenFires()
        {
            var w = OpenWorld();
            SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex("torreta_inspecao"), w.Players[0].Pos + new Vec2(6, 0), false);
            bool telegraphed = false;
            for (int t = 0; t < 30 * 4 && w.Hostile.Count == 0; t++)
            {
                w.Step(TestWorlds.NoInput());
                telegraphed |= w.Events.Cosmetic.Any(c => c.Kind == CosmeticKind.EnemyTelegraph);
            }
            Assert.That(telegraphed, Is.True);
            Assert.That(w.Hostile.Count, Is.EqualTo(1));
        }

        [Test]
        public void Stunned_DoesNotMove()
        {
            var w = OpenWorld();
            int e = SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex("microbo_enxame"), w.Players[0].Pos + new Vec2(8, 0), false);
            w.Enemies.StunUntil[e] = w.Elapsed + 1f;
            var p0 = w.Enemies.Pos[e];
            for (int t = 0; t < 10; t++) w.Step(TestWorlds.NoInput());
            Assert.That(w.Enemies.Pos[0], Is.EqualTo(p0));
            Assert.That(w.Enemies.Flags[0] & EnemyFlags.Stunned, Is.Not.Zero);
        }

        [Test]
        public void FarEnemies_UpdateEvery3Ticks()
        {
            var w = OpenWorld();
            w.Players[0].Pos = new Vec2(2, 2);
            SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex("microbo_enxame"), new Vec2(60, 60), false);
            int moves = 0;
            var last = w.Enemies.Pos[0];
            for (int t = 0; t < 9; t++)
            {
                w.Step(TestWorlds.NoInput());
                if (w.Enemies.Pos[0] != last) moves++;
                last = w.Enemies.Pos[0];
            }
            Assert.That(moves, Is.EqualTo(3));
        }
    }
}
