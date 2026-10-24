using System;
using System.Collections.Generic;

namespace ES.SimTests.Bots
{
    public class BotSmokeTests
    {
        [Test]
        public void Smoke_12Combos_2Specs_3Seeds_2Minutes_NoExceptions_CapsRespected()
        {
            var failures = new List<string>();
            foreach (ES.Sim.Difficulty d in Enum.GetValues(typeof(ES.Sim.Difficulty)))
                for (int team = 1; team <= 4; team++)
                    foreach (var spec in new[] { "eletrica", "mecanica" })
                        for (ulong seed = 1; seed <= 3; seed++)
                        {
                            var w = TestWorlds.Create(d, team, spec, seed, coop: team > 1);
                            var inputs = new PlayerInput[4];
                            ushort seq = 0;
                            int peakEnemies = 0;
                            while (w.Phase == MatchPhase.Running && w.Elapsed < 120f)
                            {
                                seq++;
                                for (int s = 0; s < 4; s++) inputs[s] = w.Players[s] != null ? BotBrain.Decide(w, s, seq) : PlayerInput.None;
                                w.Step(inputs);
                                peakEnemies = Math.Max(peakEnemies, w.Enemies.Count);
                                if (w.Gems.Count > 150) { failures.Add($"{d}/{team}/{spec}/{seed}: {w.Gems.Count} gemas"); break; }
                            }
                            if (peakEnemies > 400) failures.Add($"{d}/{team}/{spec}/{seed}: pico {peakEnemies}");
                            Assert.That(w.Stats.Kills, Is.GreaterThan(0), $"{d}/{team}/{spec}/{seed}: bots não mataram nada");
                        }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void Bot_ChoosesPendingOffer()
        {
            var w = TestWorlds.CreateOpen();
            ProgressionSystem.AddSharedXp(w, 5);
            var input = BotBrain.Decide(w, 0, 1);
            Assert.That(input.ChoiceOfferId, Is.EqualTo(w.Players[0].Progress.Current.OfferId));
            Assert.That(input.ChoiceIndex, Is.InRange(0, w.Players[0].Progress.Current.Options.Length - 1));
        }

        [Test]
        public void Bot_FleesFromNearbyEnemy()
        {
            var w = TestWorlds.CreateOpen();
            w.SpawningEnabled = false;
            SpawnDirector.SpawnEnemy(w, 0, w.Players[0].Pos + new Vec2(2, 0), false);
            EnemyAISystem.RebuildGrid(w);
            var input = BotBrain.Decide(w, 0, 1);
            Assert.That(input.MoveX, Is.LessThan(0));
        }

        [Test]
        public void Stats_CountKillsAndDamage()
        {
            var w = TestWorlds.CreateOpen();
            w.SpawningEnabled = false;
            int e = SpawnDirector.SpawnEnemy(w, 0, new Vec2(5, 5), false);
            CombatSystem.DamageEnemy(w, e, 999, Vec2.Zero, 0, 0, w.Players[0].Abilities[0].AbilityIndex);
            CombatSystem.DamagePlayer(w, 0, 7);
            Assert.That(w.Stats.Kills, Is.EqualTo(1));
            Assert.That(w.Stats.DamageTaken[0], Is.EqualTo(7));
            Assert.That(w.Stats.DamageByAbility[0][w.Players[0].Abilities[0].AbilityIndex], Is.EqualTo(10), "conta só o dano efetivo (vida do microbô)");
        }
    }
}
