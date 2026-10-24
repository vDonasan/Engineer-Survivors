using System.Linq;

namespace ES.SimTests.Netcode
{
    public class LoadoutTests
    {
        static GameDatabase Db => TestWorlds.Db;
        static int A(string id) => Db.AbilityIndex(id);

        static LoadoutTracker Tracker(params string[] specs)
        {
            var cfg = new MatchConfig { Roster = specs.Select(s => new PlayerSlot { Name = s, SpecializationId = s }).ToArray(), TeamSize = specs.Length };
            var t = new LoadoutTracker(Db);
            t.Reset(cfg);
            return t;
        }

        static RuleEvent Choice(int slot, OptionKind kind, int reference) =>
            new RuleEvent { Kind = RuleEventKind.ChoiceApplied, A = slot, B = (int)kind, C = reference };

        [Test]
        public void Reset_GivesEachSlotItsStartingWeapon()
        {
            var t = Tracker("eletrica", "mecanica");
            Assert.That(t.Slots[0].Abilities.Single().Ability, Is.EqualTo(A("descarga_direcionada")));
            Assert.That(t.Slots[1].Abilities.Single().Ability, Is.EqualTo(A("braco_giratorio")));
            Assert.That(t.Slots[0].LevelOf(A("descarga_direcionada")), Is.EqualTo(1));
            Assert.That(t.Slots[2], Is.Null);
        }

        [Test]
        public void NewAbility_Upgrade_AndSurvival()
        {
            var t = Tracker("eletrica");
            t.Apply(Choice(0, OptionKind.NewAbility, A("laser_industrial")));
            t.Apply(Choice(0, OptionKind.Upgrade, A("laser_industrial")));
            t.Apply(Choice(0, OptionKind.Survival, A("kit_primeiros_socorros")));
            Assert.That(t.Slots[0].LevelOf(A("laser_industrial")), Is.EqualTo(2));
            Assert.That(t.Slots[0].Abilities.Count, Is.EqualTo(2), "o Kit cura na hora e não ocupa espaço");
        }

        [Test]
        public void Evolution_ReplacesSourceInPlace_AndIsDiscovered()
        {
            var t = Tracker("eletrica");
            t.Apply(Choice(0, OptionKind.NewAbility, A("bateria_portatil")));
            int evo = Db.EvolutionIndex("evo_pulso_amplificado");
            t.Apply(new RuleEvent { Kind = RuleEventKind.EvolutionApplied, A = 0, B = evo, C = Db.Evolution(evo).Result });
            t.Apply(Choice(0, OptionKind.Evolution, evo));
            Assert.That(t.Slots[0].Abilities[0].Ability, Is.EqualTo(A("pulso_amplificado")));
            Assert.That(t.Slots[0].LevelOf(A("descarga_direcionada")), Is.EqualTo(0));
            Assert.That(t.Slots[0].Abilities.Count, Is.EqualTo(2));
            Assert.That(t.Discovered, Does.Contain(evo));
            Assert.That(t.Slots[0].Evolved, Does.Contain(evo));
        }

        [Test]
        public void Synergy_TogglesActive()
        {
            var t = Tracker("eletrica");
            t.Apply(new RuleEvent { Kind = RuleEventKind.SynergyChanged, A = 0, B = 0, C = 1 });
            Assert.That(t.Slots[0].ActiveSynergies, Does.Contain(0));
            t.Apply(new RuleEvent { Kind = RuleEventKind.SynergyChanged, A = 0, B = 0, C = 0 });
            Assert.That(t.Slots[0].ActiveSynergies, Is.Empty);
        }

        [Test]
        public void EvolutionProgress_CountsMetRequirements()
        {
            var t = Tracker("eletrica");
            int evo = Db.EvolutionIndex("evo_pulso_amplificado");
            var p0 = t.Progress(0, evo);
            Assert.That(p0.Total, Is.EqualTo(2));
            Assert.That(p0.Met, Is.EqualTo(0));
            for (int i = 0; i < 4; i++) t.Apply(Choice(0, OptionKind.Upgrade, A("descarga_direcionada")));
            Assert.That(t.Progress(0, evo).Met, Is.EqualTo(1), "Descarga no nível 5");
        }

        [Test]
        public void MalformedSlot_Ignored()
        {
            var t = Tracker("eletrica");
            Assert.DoesNotThrow(() => t.Apply(Choice(3, OptionKind.NewAbility, A("laser_industrial"))));
            Assert.DoesNotThrow(() => t.Apply(Choice(0, OptionKind.NewAbility, 9999)));
            Assert.That(t.Slots[0].Abilities.Count, Is.EqualTo(1));
        }

        [Test]
        public void Client_LoadoutAndSummary_MatchHost()
        {
            var net = new LoopbackNet();
            var (_, a, _) = net.Connect("Ana", "mecanica");
            net.Host.Start(5);
            net.Pump();
            var w = net.Host.World;
            w.SpawningEnabled = false;
            ProgressionSystem.AddSharedXp(w, 10);
            net.Run(0.1);
            a.Choose(a.Feed.CurrentOffer.OfferId, 0);
            net.Run(0.2);

            var host = net.Host.Feed.Loadouts.Slots[1].Abilities.Select(x => (x.Ability, x.Level));
            var client = a.Feed.Loadouts.Slots[1].Abilities.Select(x => (x.Ability, x.Level));
            Assert.That(client, Is.EqualTo(host));
            Assert.That(w.Players[1].Abilities.Select(x => (x.AbilityIndex, x.Level)), Is.EqualTo(host));

            w.Stats.AddDamage(1, A("braco_giratorio"), 123);
            w.Players[0].InvulnUntil = w.Players[1].InvulnUntil = 0; // a escolha no coop dá invulnerabilidade
            CombatSystem.DamagePlayer(w, 0, 1000);
            CombatSystem.DamagePlayer(w, 1, 1000);
            net.Run(0.2);
            Assert.That(a.Feed.Summary, Is.Not.Null);
            Assert.That(a.Feed.Summary.Phase, Is.EqualTo(MatchPhase.Defeat));
            var mine = a.Feed.Summary.Players[1];
            Assert.That(mine.Present, Is.True);
            Assert.That(mine.Damage.Single(d => d.Ability == A("braco_giratorio")).Amount, Is.EqualTo(123f).Within(0.01f));
            Assert.That(net.Host.Feed.Summary.Players[1].Damage.Select(d => (d.Ability, d.Amount)),
                Is.EqualTo(mine.Damage.Select(d => (d.Ability, d.Amount))));
        }

        [Test]
        public void SummaryCodec_RejectsGarbage()
        {
            Assert.That(SummaryCodec.TryRead(new byte[] { 1, 2, 3 }, 3, out _), Is.False);
            Assert.That(SummaryCodec.TryRead(new byte[0], 0, out _), Is.False);
        }

        [Test]
        public void TeamKills_CountedFromEnemyDied()
        {
            var f = new MatchFeed();
            f.ApplyRuleEvent(new DecodedRuleEvent { Event = new RuleEvent { Kind = RuleEventKind.EnemyDied, A = 3 } });
            f.ApplyRuleEvent(new DecodedRuleEvent { Event = new RuleEvent { Kind = RuleEventKind.EnemyDied, A = 4 } });
            Assert.That(f.TeamKills, Is.EqualTo(2));
        }
    }
}
