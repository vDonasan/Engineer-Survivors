using System.Linq;

namespace ES.SimTests.Progression
{
    public class BuildSystemsTests
    {
        static World Open(string spec = "eletrica")
        {
            var w = TestWorlds.CreateOpen(spec: spec, coop: true);
            w.SpawningEnabled = false;
            w.Players[0].InvulnUntil = float.MaxValue;
            return w;
        }

        static AbilityInstance Get(World w, string id) => w.Players[0].Abilities.FirstOrDefault(a => a.AbilityIndex == w.Db.AbilityIndex(id));
        static AbilityInstance Grant(World w, string id) => AbilitySystem.Grant(w, 0, w.Db.AbilityIndex(id));
        static void LevelTo(World w, AbilityInstance a, int level) { while (a.Level < level) AbilitySystem.LevelUp(w, 0, a); }
        static float Stat(World w, AbilityInstance a, StatId s) => a.Get(w.Players[0], s);

        static int Dummy(World w, Vec2 offset, bool elite = false)
        {
            int e = SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex("prensa_ambulante"), w.Players[0].Pos + offset, elite);
            w.Enemies.StunUntil[e] = float.MaxValue;
            return e;
        }

        static float HpOf(World w, ushort id) => Enumerable.Range(0, w.Enemies.Count).Where(i => w.Enemies.Id[i] == id).Select(i => w.Enemies.Hp[i]).DefaultIfEmpty(-1).First();

        [Test]
        public void Affinity_ThreeStages_Eletrica_Descarga()
        {
            var w = Open();
            var d = Get(w, "descarga_direcionada");
            Assert.That(Stat(w, d, StatId.Damage), Is.EqualTo(14f * 1.04f).Within(1e-3));
            LevelTo(w, d, 4);
            Assert.That(Stat(w, d, StatId.Damage), Is.EqualTo((14f + 4 + 2 + 3) * 1.04f * 1.04f).Within(1e-3));
            Assert.That(Stat(w, d, StatId.Chains), Is.EqualTo(3f));
            LevelTo(w, d, 5);
            Assert.That(Stat(w, d, StatId.Chains), Is.EqualTo(2f + 1 + 1 + 1), "nível 3, nível 5 e afinidade avançada");
        }

        [Test]
        public void Affinity_NotAppliedToNonMatchingTags()
        {
            var w = Open();
            var arm = Grant(w, "braco_giratorio");
            Assert.That(Stat(w, arm, StatId.Damage), Is.EqualTo(12f).Within(1e-4));
        }

        [Test]
        public void Affinity_Mecanica_AdvancedGivesMoveSpeed()
        {
            var w = Open("mecanica");
            var arm = Get(w, "braco_giratorio");
            LevelTo(w, arm, 5);
            Assert.That(w.Players[0].Stats.Get(StatId.MoveSpeed), Is.EqualTo(4.5f * 1.05f).Within(1e-4));
            Assert.That(Stat(w, arm, StatId.Knockback), Is.EqualTo(2f * 1.15f).Within(1e-4));
        }

        [Test]
        public void Synergy_CargaAcumulada_ActivatesAtBateria2_AndEvery4thIsAmplified()
        {
            var w = Open();
            int syn = w.Db.SynergyIndex("carga_acumulada");
            var bat = Grant(w, "bateria_portatil");
            Assert.That(w.Players[0].Synergies[syn], Is.False);
            AbilitySystem.LevelUp(w, 0, bat);
            Assert.That(w.Players[0].Synergies[syn], Is.True);

            ushort id = w.Enemies.Id[Dummy(w, new Vec2(3, 0), elite: true)];
            float start = HpOf(w, id);
            for (int t = 0; t < 100; t++) w.Step(TestWorlds.NoInput());
            Assert.That(Get(w, "descarga_direcionada").FireCount, Is.EqualTo(4));
            float single = 14f * 1.04f;
            Assert.That(start - HpOf(w, id), Is.EqualTo(3 * single + single * 2f).Within(1e-2));
        }

        [Test]
        public void Synergy_DeactivatesWhenRequirementLost()
        {
            var w = Open();
            int syn = w.Db.SynergyIndex("carga_acumulada");
            var bat = Grant(w, "bateria_portatil");
            LevelTo(w, bat, 3);
            LevelTo(w, Get(w, "descarga_direcionada"), 5);
            Assert.That(w.Players[0].Synergies[syn], Is.True);
            ProgressionSystem.ApplyEvolution(w, 0, w.Db.EvolutionIndex("evo_pulso_amplificado"));
            Assert.That(w.Players[0].Synergies[syn], Is.False);
            Assert.That(w.Events.Rule.Any(r => r.Kind == RuleEventKind.SynergyChanged && r.B == syn && r.C == 0), Is.True);
        }

        [Test]
        public void Evolution_NotOfferedBeforeRequirements()
        {
            var w = Open();
            LevelTo(w, Get(w, "descarga_direcionada"), 5);
            var bat = Grant(w, "bateria_portatil");
            LevelTo(w, bat, 2);
            for (short k = 1; k < 50; k++)
                Assert.That(OfferGenerator.Generate(w, 0, k).Options.Any(o => o.Kind == OptionKind.Evolution), Is.False);
            AbilitySystem.LevelUp(w, 0, bat);
            for (short k = 50; k < 60; k++)
                Assert.That(OfferGenerator.Generate(w, 0, k).Options.Any(o => o.Kind == OptionKind.Evolution && o.Ref == w.Db.EvolutionIndex("evo_pulso_amplificado")), Is.True);
        }

        [Test]
        public void Evolution_PulsoAmplificado_ReplacesDescarga_ThreeInitialTargets()
        {
            var w = Open();
            ProgressionSystem.ApplyEvolution(w, 0, w.Db.EvolutionIndex("evo_pulso_amplificado"));
            var abilities = w.Players[0].Abilities.Select(a => w.Db.Ability(a.AbilityIndex).Id).ToArray();
            Assert.That(abilities, Is.EqualTo(new[] { "pulso_amplificado" }));

            var ids = new[] { new Vec2(5, 0), new Vec2(-5, 0), new Vec2(0, 5) }.Select(o => w.Enemies.Id[Dummy(w, o)]).ToArray();
            w.Step(TestWorlds.NoInput());
            foreach (var id in ids) Assert.That(70 - HpOf(w, id), Is.EqualTo(22f * 1.04f).Within(1e-2));
        }

        [Test]
        public void Evolution_RotorDeImpacto_IsDiscoverable_AndRecordedOnApply()
        {
            var w = Open("mecanica");
            int evo = w.Db.EvolutionIndex("evo_rotor_de_impacto");
            Assert.That(w.Db.Evolution(evo).Discoverable, Is.True);
            Assert.That(w.Discovered, Is.Empty);
            ProgressionSystem.ApplyEvolution(w, 0, evo);
            Assert.That(w.Discovered, Does.Contain(evo));
            Assert.That(Get(w, "rotor_de_impacto"), Is.Not.Null);
            Assert.That(Get(w, "braco_giratorio"), Is.Null);
            Assert.That(w.Events.Rule.Any(r => r.Kind == RuleEventKind.EvolutionApplied && r.B == evo), Is.True);
        }

        [Test]
        public void HybridBuild_EletricaWithBracoGiratorio_Works()
        {
            var w = Open();
            TestWorlds.Disarm(w);
            Grant(w, "braco_giratorio");
            ushort id = w.Enemies.Id[Dummy(w, new Vec2(0, 1.8f))];
            for (int t = 0; t < 45; t++) w.Step(TestWorlds.NoInput());
            Assert.That(HpOf(w, id), Is.LessThan(70));
        }
    }
}
