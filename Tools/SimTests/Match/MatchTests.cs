using System;
using System.Linq;

namespace ES.SimTests.Match
{
    public class MatchTests
    {
        [Test]
        public void Victory_At600s()
        {
            var w = TestWorlds.Disarm(TestWorlds.CreateOpen());
            w.SpawningEnabled = false;
            while (w.Phase == MatchPhase.Running && w.Elapsed < 700) w.Step(TestWorlds.NoInput());
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Victory));
            Assert.That(w.Elapsed, Is.EqualTo(600f).Within(0.05f));
            Assert.That(w.Events.Rule.Any(r => r.Kind == RuleEventKind.MatchEnded && r.A == (int)MatchPhase.Victory), Is.True);
        }

        [Test]
        public void Defeat_WhenAllDowned()
        {
            var w = TestWorlds.CreateOpen(team: 2, coop: true);
            CombatSystem.DamagePlayer(w, 0, 1000);
            w.Step(TestWorlds.NoInput());
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Running), "ainda há um ativo");
            CombatSystem.DamagePlayer(w, 1, 1000);
            w.Step(TestWorlds.NoInput());
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Defeat));
            Assert.That(w.Events.Rule.Count(r => r.Kind == RuleEventKind.MatchEnded), Is.EqualTo(1));
            int tick = w.Tick;
            w.Step(TestWorlds.NoInput());
            Assert.That(w.Tick, Is.EqualTo(tick), "partida encerrada não avança");
        }

        [Test]
        public void Disconnect_LastActiveWithPendingOffer_NoSoftLock()
        {
            var w = TestWorlds.CreateOpen(team: 2, coop: true);
            w.Players[1].Status = PlayerStatus.Downed;
            ProgressionSystem.AddSharedXp(w, 10); // equipe de 2: ×0,72 → 7,2 ≥ 5
            Assert.That(w.Players[0].Progress.Current, Is.Not.Null);
            MatchRules.SetInactive(w, 0);
            Assert.DoesNotThrow(() => w.Step(TestWorlds.NoInput()));
            Assert.That(w.Players[0].Progress.Current, Is.Null);
            Assert.That(w.Players[0].Progress.PendingLevels, Is.EqualTo(0));
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Defeat));
        }

        [Test]
        public void Disconnect_SoloLike_DoesNotPauseForever()
        {
            var w = TestWorlds.CreateOpen(team: 1);
            ProgressionSystem.AddSharedXp(w, 5);
            Assert.That(w.PausedForChoice, Is.True);
            MatchRules.SetInactive(w, 0);
            Assert.That(w.PausedForChoice, Is.False);
        }

        [Test]
        public void Disconnect_DoesNotChangeBalance()
        {
            var w = TestWorlds.CreateOpen(team: 3, coop: true);
            var before = w.Balance;
            MatchRules.SetInactive(w, 2);
            w.Step(TestWorlds.NoInput());
            Assert.That(w.Balance.TeamSize, Is.EqualTo(3));
            Assert.That(w.Balance.EnemyHp, Is.EqualTo(before.EnemyHp));
            Assert.That(w.Players[2].Status, Is.EqualTo(PlayerStatus.Inactive));
        }

        [Test]
        public void TickClock_LongFrame_CapsAt5Ticks()
        {
            var c = new TickClock(30, 5);
            Assert.That(c.Advance(120.0), Is.EqualTo(5));
            Assert.That(c.Advance(1.0 / 30.0), Is.EqualTo(1));
        }

        [Test]
        public void TickClock_NormalFrames_30PerSecond()
        {
            var c = new TickClock(30, 5);
            int total = 0;
            for (int i = 0; i < 60; i++) total += c.Advance(1.0 / 60.0);
            Assert.That(total, Is.InRange(29, 30));
            Assert.That(c.Alpha, Is.InRange(0.0, 1.0));
        }

        [Test]
        public void World_Step_NoAllocations_AfterWarmup()
        {
            var def = ContentLoader.LoadSp1();
            def.rules.xpA = 1e9f; // sem subidas de nível: mede o caminho quente do tick
            var w = TestWorlds.Create(team: 1, coop: true, db: GameDatabase.Build(def).Db);
            w.Players[0].InvulnUntil = float.MaxValue;
            var rng = new Rng(11);
            for (int i = 0; i < 200; i++)
                SpawnDirector.SpawnEnemy(w, i % 3, new Vec2(rng.Range(4f, 60f), rng.Range(4f, 60f)), false);
            var inputs = TestWorlds.NoInput();
            for (int t = 0; t < 300; t++) w.Step(inputs);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int t = 0; t < 300; t++) w.Step(inputs);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [Test]
        public void World_SameSeedSameInputs_SameState()
        {
            World Run()
            {
                var w = TestWorlds.Create(ES.Sim.Difficulty.Hard, team: 2, coop: true, seed: 77);
                var inputs = TestWorlds.NoInput();
                for (int t = 0; t < 900; t++)
                {
                    inputs[0] = new PlayerInput { Seq = (ushort)t, MoveX = Quant.Axis((float)Math.Cos(t * 0.02)), MoveY = Quant.Axis((float)Math.Sin(t * 0.02)), ChoiceIndex = -1 };
                    inputs[1] = new PlayerInput { Seq = (ushort)t, MoveX = -60, ChoiceIndex = -1 };
                    w.Step(inputs);
                }
                return w;
            }
            var a = Run();
            var b = Run();
            Assert.That(a.Enemies.Count, Is.EqualTo(b.Enemies.Count));
            for (int i = 0; i < a.Enemies.Count; i++) Assert.That(a.Enemies.Pos[i], Is.EqualTo(b.Enemies.Pos[i]));
            Assert.That(a.Players[0].Hp, Is.EqualTo(b.Players[0].Hp));
            Assert.That(a.Players[1].Progress.Level, Is.EqualTo(b.Players[1].Progress.Level));
        }
    }
}
