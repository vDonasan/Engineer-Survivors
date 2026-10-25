using System.Linq;

namespace ES.SimTests.Progression
{
    public class ProgressionTests
    {
        static World Open(int team = 1, bool coop = false, string spec = "eletrica")
        {
            var w = TestWorlds.CreateOpen(team: team, coop: coop, spec: spec);
            w.SpawningEnabled = false;
            return w;
        }

        static PlayerInput[] Choose(short offer, int index, int slot = 0)
        {
            var inputs = TestWorlds.NoInput();
            inputs[slot] = new PlayerInput { Seq = 1, ChoiceOfferId = offer, ChoiceIndex = (sbyte)index };
            return inputs;
        }

        [Test]
        public void XpCurve_Values()
        {
            var r = TestWorlds.Db.Def.rules;
            Assert.That(XpCurve.ToNext(r, 1), Is.EqualTo(5));
            Assert.That(XpCurve.ToNext(r, 5), Is.EqualTo(35));
            Assert.That(XpCurve.ToNext(r, 10), Is.EqualTo(99));
        }

        [Test]
        public void SharedXp_AllActivePlayersGain()
        {
            var w = Open(team: 2, coop: true);
            w.Players[1].Status = PlayerStatus.Downed;
            ProgressionSystem.AddSharedXp(w, 3);
            Assert.That(w.Players[0].Progress.Xp, Is.EqualTo(3 * 0.72f).Within(1e-4));
            Assert.That(w.Players[1].Progress.Xp, Is.EqualTo(0));
        }

        [Test]
        public void Offer_NoDuplicates_RespectsMax()
        {
            var w = Open();
            var p = w.Players[0];
            var d = p.Abilities[0];
            while (AbilitySystem.LevelUp(w, 0, d)) { }
            for (short k = 1; k < 200; k++)
            {
                var o = OfferGenerator.Generate(w, 0, k);
                Assert.That(o.Options.Length, Is.InRange(1, 3));
                Assert.That(o.Options.Distinct().Count(), Is.EqualTo(o.Options.Length));
                Assert.That(o.Options.Any(x => x.Kind == OptionKind.Upgrade && x.Ref == d.AbilityIndex), Is.False);
            }
        }

        [Test]
        public void Offer_ExhaustedPool_StillReturnsAtLeastOne()
        {
            var w = Open();
            var p = w.Players[0];
            // tudo o que pode ser oferecido (fora armas de personagem, evoluções e o kit)
            for (int i = 0; i < w.Db.AbilityCount; i++)
            {
                var ab = w.Db.Ability(i);
                if (ab.Def.exclusive || ab.Def.evolutionOnly || ab.Kind == AbilityKind.Survival || p.Abilities.Any(x => x.AbilityIndex == i)) continue;
                AbilitySystem.Grant(w, 0, i);
            }
            foreach (var a in p.Abilities.ToArray()) while (AbilitySystem.LevelUp(w, 0, a)) { }
            ProgressionSystem.ApplyEvolution(w, 0, w.Db.EvolutionIndex("evo_pulso_amplificado"));
            ProgressionSystem.ApplyEvolution(w, 0, w.Db.EvolutionIndex("evo_rotor_de_impacto"));

            var o = OfferGenerator.Generate(w, 0, 1);
            Assert.That(o.Options.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(o.Options.All(x => x.Kind == OptionKind.Survival), Is.True);
        }

        [Test]
        public void MultiLevel_QueuesOffersInOrder()
        {
            var w = Open();
            var pr = w.Players[0].Progress;
            ProgressionSystem.AddSharedXp(w, 5 + 11 + 17);
            Assert.That(pr.Level, Is.EqualTo(4));
            Assert.That(pr.Current.OfferId, Is.EqualTo(1));
            Assert.That(ProgressionSystem.Choose(w, 0, 1, 0), Is.True);
            Assert.That(pr.Current.OfferId, Is.EqualTo(2));
            Assert.That(ProgressionSystem.Choose(w, 0, 2, 0), Is.True);
            Assert.That(ProgressionSystem.Choose(w, 0, 3, 0), Is.True);
            Assert.That(pr.Current, Is.Null);
            Assert.That(pr.PendingLevels, Is.EqualTo(0));
        }

        [Test]
        public void Choose_WrongOfferId_Rejected()
        {
            var w = Open();
            ProgressionSystem.AddSharedXp(w, 5);
            Assert.That(ProgressionSystem.Choose(w, 0, 99, 0), Is.False);
            Assert.That(w.Players[0].Progress.Current, Is.Not.Null);
        }

        [Test]
        public void Choose_IndexOutOfRange_Rejected()
        {
            var w = Open();
            ProgressionSystem.AddSharedXp(w, 5);
            short id = w.Players[0].Progress.Current.OfferId;
            Assert.That(ProgressionSystem.Choose(w, 0, id, 7), Is.False);
            Assert.That(ProgressionSystem.Choose(w, 0, id, -1), Is.False);
        }

        [Test]
        public void Choose_AppliesOptionAndEmitsEvent()
        {
            var w = Open();
            ProgressionSystem.AddSharedXp(w, 5);
            var offer = w.Players[0].Progress.Current;
            int before = w.Players[0].Abilities.Sum(a => a.Level);
            w.Step(Choose(offer.OfferId, 0));
            Assert.That(w.Events.Rule.Any(r => r.Kind == RuleEventKind.ChoiceApplied && r.A == 0), Is.True);
            var opt = offer.Options[0];
            if (opt.Kind != OptionKind.Survival)
                Assert.That(w.Players[0].Abilities.Sum(a => a.Level), Is.EqualTo(before + 1));
        }

        [Test]
        public void Coop_Timeout_AppliesFirstOption()
        {
            var w = Open(coop: true);
            ProgressionSystem.AddSharedXp(w, 5);
            Assert.That(w.PausedForChoice, Is.False);
            bool applied = false;
            for (int t = 0; t < 30 * 13 && !applied; t++)
            {
                w.Step(TestWorlds.NoInput());
                applied = w.Events.Rule.Any(r => r.Kind == RuleEventKind.ChoiceApplied);
            }
            Assert.That(applied, Is.True);
            Assert.That(w.Elapsed, Is.InRange(11.9f, 12.2f));
            Assert.That(w.Players[0].Progress.Current, Is.Null);
        }

        [Test]
        public void Coop_OfferGrantsInvulnerability()
        {
            var w = Open(coop: true);
            ProgressionSystem.AddSharedXp(w, 5);
            Assert.That(w.Players[0].InvulnUntil, Is.GreaterThanOrEqualTo(w.Elapsed + 1.99f));
        }

        [Test]
        public void Solo_PausedForChoice_StopsTimeUntilChosen()
        {
            var w = Open();
            ProgressionSystem.AddSharedXp(w, 5);
            Assert.That(w.PausedForChoice, Is.True);
            float t0 = w.Elapsed;
            for (int t = 0; t < 30; t++) w.Step(TestWorlds.NoInput());
            Assert.That(w.Elapsed, Is.EqualTo(t0));
            w.Step(Choose(w.Players[0].Progress.Current.OfferId, 0));
            Assert.That(w.PausedForChoice, Is.False);
            w.Step(TestWorlds.NoInput());
            Assert.That(w.Elapsed, Is.GreaterThan(t0));
        }

        [Test]
        public void Offer_SurvivalWhenLowHp()
        {
            var w = Open();
            w.Players[0].Hp = 30;
            for (short k = 1; k < 20; k++)
                Assert.That(OfferGenerator.Generate(w, 0, k).Options.Any(o => o.Kind == OptionKind.Survival), Is.True);
        }

        [Test]
        public void CollectedGem_LevelsUpThroughStep()
        {
            var w = Open(coop: true);
            var p = w.Players[0];
            w.Gems.Spawn(PickupKind.Xp, p.Pos, 6, 0);
            w.Step(TestWorlds.NoInput());
            Assert.That(p.Progress.Level, Is.EqualTo(2));
            Assert.That(w.Events.Rule.Any(r => r.Kind == RuleEventKind.LevelUpOffer && r.A == 0), Is.True);
        }
    }
}
