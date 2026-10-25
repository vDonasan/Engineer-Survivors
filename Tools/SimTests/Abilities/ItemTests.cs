using System;
using System.Collections.Generic;
using System.Linq;

namespace ES.SimTests.Abilities
{
    /// <summary>Os nove itens: efeitos reais, níveis, evoluções e o texto de mudança por nível.</summary>
    public class ItemTests
    {
        static World Open(string spec = "eletrica")
        {
            var w = TestWorlds.CreateOpen(spec: spec);
            w.SpawningEnabled = false;
            w.Players[0].InvulnUntil = float.MaxValue;
            return w;
        }

        static void Ring(World w, int count, float radius, float hp = 5000)
        {
            for (int k = 0; k < count; k++)
            {
                double a = Math.PI * 2 * k / count;
                var pos = w.Players[0].Pos + new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * (radius + (k % 3) * 0.7f);
                int e = SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex("prensa_ambulante"), pos, false);
                w.Enemies.Hp[e] = w.Enemies.MaxHp[e] = hp;
                w.Enemies.StunUntil[e] = float.MaxValue;
            }
            EnemyAISystem.RebuildGrid(w);
        }

        static AbilityInstance Give(World w, string id, int level)
        {
            var a = w.Players[0].Abilities.FirstOrDefault(x => x.AbilityIndex == w.Db.AbilityIndex(id)) ?? AbilitySystem.Grant(w, 0, w.Db.AbilityIndex(id));
            while (a.Level < level && AbilitySystem.LevelUp(w, 0, a)) { }
            return a;
        }

        static void Run(World w, float seconds, Action<World> each = null)
        {
            var inputs = TestWorlds.NoInput();
            for (int t = 0; t < (int)(seconds * 30); t++) { w.Step(inputs); each?.Invoke(w); }
        }

        static float DamageBy(World w, string id) => w.Stats.DamageByAbility[0][w.Db.AbilityIndex(id)];

        static void Evolve(World w, string resultId)
        {
            int evo = Enumerable.Range(0, w.Db.EvolutionCount).First(i => w.Db.Ability(w.Db.Evolution(i).Result).Id == resultId);
            Assert.That(RequirementEvaluator.Met(w, 0, w.Db.Evolution(evo).Requires), Is.True, resultId + ": requisitos");
            ProgressionSystem.ApplyEvolution(w, 0, evo);
        }

        [Test]
        public void AllNineItems_ExistWithEvolution_AndAreOffered()
        {
            var ids = new[] { "protoboard", "cenoura_podre", "iogurte_80", "motor_astra", "cachorro_quente", "camiseta_contrario", "mamao", "tequila", "chapeu_japones" };
            var db = TestWorlds.Db;
            foreach (var id in ids)
            {
                int i = db.AbilityIndex(id);
                Assert.That(i, Is.GreaterThanOrEqualTo(0), id);
                Assert.That(db.Ability(i).Def.exclusive || db.Ability(i).Def.evolutionOnly, Is.False, id + " aparece nas cartas");
                Assert.That(Enumerable.Range(0, db.EvolutionCount).Any(e => db.Evolution(e).From == i), Is.True, id + ": evolução");
            }
            var w = Open();
            var offered = new HashSet<int>();
            for (short o = 1; o <= 400; o++) foreach (var opt in OfferGenerator.Generate(w, 0, o).Options) offered.Add(opt.Ref);
            foreach (var id in ids) Assert.That(offered.Contains(db.AbilityIndex(id)), Is.True, id + " nunca oferecido");
        }

        [Test]
        public void Protoboard_PerLevelBonuses_AndElectricDischarges()
        {
            var w = Open("eletrica");
            var pb = Give(w, "protoboard", 1);
            var descarga = w.Players[0].Abilities[0];
            float cd1 = descarga.Get(w.Players[0], StatId.Cooldown);
            AbilitySystem.LevelUp(w, 0, pb);
            float cd2 = descarga.Get(w.Players[0], StatId.Cooldown);
            Assert.That(cd2 / cd1, Is.EqualTo(0.87f / 0.92f).Within(1e-3), "nível 2: −13% em vez de −8%");
            Give(w, "protoboard", 4);
            Ring(w, 10, 2.5f);
            Run(w, 20f);
            Assert.That(DamageBy(w, "protoboard"), Is.GreaterThan(0), "descargas da Protoboard");
        }

        [Test]
        public void Protoboard_Experimental_RequiresAnotherElectricAbility()
        {
            var w = Open("mecanica");
            Give(w, "protoboard", 4);
            int evo = Enumerable.Range(0, w.Db.EvolutionCount).First(i => w.Db.Ability(w.Db.Evolution(i).Result).Id == "protoboard_experimental");
            Assert.That(RequirementEvaluator.Met(w, 0, w.Db.Evolution(evo).Requires), Is.False, "Mecânica não tem arma eletrônica");
            Give(w, "descarga_direcionada", 1);
            Evolve(w, "protoboard_experimental");
            Ring(w, 10, 2.5f);
            Run(w, 10f);
            Assert.That(DamageBy(w, "protoboard_experimental"), Is.GreaterThan(0));
        }

        [Test]
        public void Cenoura_LeavesStinkCloud_Level5Spreads_EvolutionSpreadsFurther()
        {
            var w = Open("mecanica");
            w.Players[0].Abilities.Clear();
            Give(w, "cenoura_podre", 5);
            Ring(w, 12, 3f);
            bool cloud = false;
            Run(w, 8f, x => cloud |= Enumerable.Range(0, x.Zones.Count).Any(i => x.Zones.Items[i].Sprite == AllySprite.ZoneStink));
            Assert.That(cloud, Is.True);
            Assert.That(DamageBy(w, "cenoura_podre"), Is.GreaterThan(0));
            Give(w, "campo_eletromagnetico", 1);
            Evolve(w, "hortalica_desgraca");
            Run(w, 6f);
            Assert.That(DamageBy(w, "hortalica_desgraca"), Is.GreaterThan(0));
        }

        [Test]
        public void Iogurte_SpeedXpAndHealOnLevelUp_DietaBoostsOnXp()
        {
            var w = Open();
            var p = w.Players[0];
            float speed0 = p.Stats.Get(StatId.MoveSpeed);
            var io = Give(w, "iogurte_80", 2);
            Assert.That(p.Stats.Get(StatId.MoveSpeed) / speed0, Is.EqualTo(1.11f).Within(1e-3));
            Give(w, "iogurte_80", 5);
            Assert.That(p.Stats.Get(StatId.XpGain), Is.EqualTo(0.10f).Within(1e-4));
            p.Hp = 50;
            ProgressionSystem.AddSharedXp(w, 5);
            Assert.That(p.Hp, Is.GreaterThan(50), "cura ao subir de nível");
            Give(w, "turbina", 1);
            Evolve(w, "dieta_milagrosa");
            float before = p.Stats.Get(StatId.MoveSpeed);
            ProgressionSystem.AddSharedXp(w, 1);
            Assert.That(p.Stats.Get(StatId.MoveSpeed), Is.GreaterThan(before), "impulso ao coletar experiência");
            p.Progress.Current = null; // carta aberta pausa o solo
            p.Progress.PendingLevels = 0;
            Run(w, 2f);
            Assert.That(p.Stats.Get(StatId.MoveSpeed), Is.EqualTo(before).Within(1e-3), "o impulso acaba");
        }

        [Test]
        public void Motor_WavesHit_Level4ThrowsParts_AnyCharacter()
        {
            var w = Open("staut");
            Give(w, "motor_astra", 4);
            Ring(w, 8, 2f);
            bool parts = false;
            Run(w, 10f, x => parts |= Enumerable.Range(0, x.Shots.Count).Any(i => x.Shots.Items[i].Sprite == AllySprite.ScrapPart));
            Assert.That(DamageBy(w, "motor_astra"), Is.GreaterThan(0));
            Assert.That(parts, Is.True);
        }

        [Test]
        public void Astra_WithJeanCar_LeavesHeatTrail()
        {
            var w = Open("jean");
            ES.Sim.DebugEvolve.Slot(w, 0, 1);
            Give(w, "motor_astra", 5);
            Give(w, "turbina", 2);
            Evolve(w, "astra_alta_rotacao");
            Ring(w, 8, 3f);
            bool trail = false;
            Run(w, 4f, x => trail |= Enumerable.Range(0, x.Zones.Count).Any(i => x.Zones.Items[i].Sprite == AllySprite.ZoneHeat && x.Zones.Items[i].Ability == x.Db.AbilityIndex("carro_drift")));
            Assert.That(trail, Is.True, "drift acelerado com rastro");
        }

        [Test]
        public void Cachorro_ChasesBitesDashes_TurboWithMotor()
        {
            var w = Open();
            Give(w, "cachorro_quente", 5);
            Ring(w, 6, 3f);
            bool heat = false;
            Run(w, 10f, x => heat |= Enumerable.Range(0, x.Zones.Count).Any(i => x.Zones.Items[i].Sprite == AllySprite.ZoneHeat));
            Assert.That(w.Companions.Items.Count(c => c.Active && c.Sprite == AllySprite.HotDog), Is.EqualTo(1));
            Assert.That(DamageBy(w, "cachorro_quente"), Is.GreaterThan(0));
            Assert.That(heat, Is.True, "rastro de calor da investida");
            Give(w, "motor_astra", 1);
            Evolve(w, "cao_turbo");
            Run(w, 6f);
            Assert.That(w.Companions.Items.Count(c => c.Active && c.Sprite == AllySprite.HotDogTurbo), Is.EqualTo(1), "um só cachorro, agora turbo");
            Assert.That(w.Companions.Items.Count(c => c.Active && c.Sprite == AllySprite.HotDog), Is.EqualTo(0));
            Assert.That(DamageBy(w, "cao_turbo"), Is.GreaterThan(0));
        }

        [Test]
        public void Camiseta_DodgeAvoidsDamage_AndTriggersEffects()
        {
            var w = Open();
            var p = w.Players[0];
            p.InvulnUntil = 0;
            Give(w, "camiseta_contrario", 4);
            Assert.That(p.Stats.Get(StatId.Dodge), Is.EqualTo(0.11f).Within(1e-4));
            p.Stats.SetBase(StatId.Dodge, 1f); // força a esquiva
            float hp = p.Hp;
            CombatSystem.DamagePlayer(w, 0, 30);
            Assert.That(p.Hp, Is.EqualTo(hp), "esquivou");
            Assert.That(Enumerable.Range(0, w.Zones.Count).Any(i => w.Zones.Items[i].Sprite == AllySprite.Afterimage), Is.True, "imagem residual");
        }

        [Test]
        public void Camiseta_DodgeChanceIsCapped()
        {
            var w = Open();
            var p = w.Players[0];
            p.Stats.SetBase(StatId.Dodge, 5f);
            int dodged = 0;
            for (int i = 0; i < 400; i++) { p.InvulnUntil = 0; float hp = p.Hp = 100; CombatSystem.DamagePlayer(w, 0, 1); if (p.Hp == hp) dodged++; }
            Assert.That(dodged, Is.LessThan(320), "esquiva limitada a 60%");
        }

        [Test]
        public void Mamao_Heals_Level4ShieldCapped_SupremoField()
        {
            var w = Open();
            var p = w.Players[0];
            Give(w, "mamao", 4);
            p.Hp = 40;
            Run(w, 7f);
            Assert.That(p.Hp, Is.GreaterThan(40));
            p.Hp = p.Stats.Get(StatId.MaxHp);
            Run(w, 30f);
            Assert.That(p.Shield, Is.GreaterThan(0));
            Assert.That(p.Shield, Is.LessThanOrEqualTo(p.Stats.Get(StatId.MaxHp) * 0.15f + 1e-3), "escudo limitado");
            Give(w, "mamao", 5);
            Give(w, "marmita", 1);
            Evolve(w, "mamao_supremo");
            Run(w, 1f);
            Assert.That(Enumerable.Range(0, w.Zones.Count).Count(i => w.Zones.Items[i].Sprite == AllySprite.ZoneHeal), Is.EqualTo(1));
        }

        [Test]
        public void Tequila_EuphoriaThenPenalty_FiestaAddsProjectiles()
        {
            var w = Open("staut");
            var p = w.Players[0];
            Give(w, "tequila", 5);
            Ring(w, 6, 3f);
            var bolhas = p.Abilities[0];
            float cd0 = bolhas.Get(p, StatId.Cooldown), speed0 = bolhas.Get(p, StatId.ProjectileSpeed);
            bool euphoria = false, hangover = false;
            Run(w, 20f, x => { euphoria |= bolhas.Get(p, StatId.Cooldown) < cd0 - 1e-4f; hangover |= bolhas.Get(p, StatId.ProjectileSpeed) < speed0 - 1e-4f; });
            Assert.That(euphoria && hangover, Is.True);
            Assert.That(DamageBy(w, "tequila"), Is.GreaterThan(0), "pulso no fim (nível 5)");
            Give(w, "attr_vel_ataque", 2);
            Evolve(w, "fiesta_caotica");
            int count0 = (int)bolhas.Get(p, StatId.Count);
            bool more = false;
            Run(w, 20f, x => more |= (int)bolhas.Get(p, StatId.Count) > count0);
            Assert.That(more, Is.True, "+1 projétil na euforia");
        }

        [Test]
        public void Chapeu_FasterShots_Level5Focus_MestreRicochets()
        {
            var w = Open("staut");
            var p = w.Players[0];
            var bolhas = p.Abilities[0];
            float s0 = bolhas.Get(p, StatId.ProjectileSpeed);
            Give(w, "chapeu_japones", 1);
            Assert.That(bolhas.Get(p, StatId.ProjectileSpeed) / s0, Is.EqualTo(1.10f).Within(1e-3));
            Give(w, "chapeu_japones", 5);
            Ring(w, 6, 3f);
            int focused = 0;
            Run(w, 6f, x => { for (int i = 0; i < x.Shots.Count; i++) if (x.Shots.Items[i].Pierce >= 3 && x.Shots.Items[i].Sprite == AllySprite.Bubble) focused++; });
            Assert.That(focused, Is.GreaterThan(0), "tiro concentrado com +2 perfuração");
            Evolve(w, "mestre_projeteis");
            int maxRicochets = 0, ricochet = 0;
            Run(w, 10f, x => { for (int i = 0; i < x.Shots.Count; i++) { if (x.Shots.Items[i].Ricochets > 0) ricochet++; maxRicochets = Math.Max(maxRicochets, x.Shots.Items[i].Ricochets); } });
            Assert.That(ricochet, Is.GreaterThan(0));
            Assert.That(maxRicochets, Is.LessThanOrEqualTo(3));
        }

        // ---------- texto das mudanças por nível ----------

        [Test]
        public void Preview_WeaponLevel_ShowsBeforeAndAfter()
        {
            var db = TestWorlds.Db;
            var bolhas = db.Ability(db.AbilityIndex("bolhas"));
            var ch = AbilityPreview.Changes(bolhas, 1, 2);
            var count = ch.Single(c => c.Stat == StatId.Count);
            Assert.That(count.From, Is.EqualTo(2)); Assert.That(count.To, Is.EqualTo(3));
            var calc = db.Ability(db.AbilityIndex("calculos"));
            var cd = AbilityPreview.Changes(calc, 3, 4).Single(c => c.Stat == StatId.Cooldown);
            Assert.That(cd.To / cd.From, Is.EqualTo(0.9f).Within(1e-4));
        }

        [Test]
        public void Preview_ItemGrant_UsesPerLevelValues()
        {
            var db = TestWorlds.Db;
            var io = db.Ability(db.AbilityIndex("iogurte_80"));
            var speed = AbilityPreview.Changes(io, 1, 2).Single(c => c.Stat == StatId.MoveSpeed);
            Assert.That(speed.From, Is.EqualTo(0.08f).Within(1e-5)); Assert.That(speed.To, Is.EqualTo(0.11f).Within(1e-5));
            Assert.That(AbilityPreview.Changes(io, 0, 1).Count, Is.GreaterThanOrEqualTo(2), "carta nova mostra os bônus do nível 1");
            var attr = db.Ability(db.AbilityIndex("attr_dano"));
            var dmg = AbilityPreview.Changes(attr, 2, 3).Single();
            Assert.That(dmg.From, Is.EqualTo(0.16f).Within(1e-5)); Assert.That(dmg.To, Is.EqualTo(0.24f).Within(1e-5));
        }

        // ---------- partida com todos os itens ----------

        [Test]
        public void FullMatch_FourPlayers_AllItemsEvolved_NoExceptions()
        {
            var roster = new[] { "giulia", "jean", "staut", "fufu" }.Select((id, i) => new PlayerSlot { Name = "P" + i, SpecializationId = id }).ToArray();
            var cfg = new MatchConfig { Seed = 9, Difficulty = ES.Sim.Difficulty.Hard, TeamSize = 4, ArenaId = "laboratorio", Roster = roster, Coop = true };
            var w = new World(TestWorlds.Db, cfg);
            var items = new[] { "protoboard", "cenoura_podre", "iogurte_80", "motor_astra", "cachorro_quente", "camiseta_contrario", "mamao", "tequila", "chapeu_japones" };
            for (int s = 0; s < 4; s++)
            {
                w.Players[s].InvulnUntil = float.MaxValue;
                ES.Sim.DebugEvolve.Slot(w, s, 3);
                foreach (var id in items)
                {
                    var a = AbilitySystem.Grant(w, s, w.Db.AbilityIndex(id));
                    while (AbilitySystem.LevelUp(w, s, a)) { }
                }
                foreach (var extra in new[] { "turbina", "marmita", "attr_vel_ataque", "campo_eletromagnetico", "cabo_hdmi" })
                {
                    var a = AbilitySystem.Grant(w, s, w.Db.AbilityIndex(extra));
                    if (a != null) while (a.Level < 2 && AbilitySystem.LevelUp(w, s, a)) { }
                }
                for (int e = 0; e < w.Db.EvolutionCount; e++)
                    if (RequirementEvaluator.Met(w, s, w.Db.Evolution(e).Requires) && w.Players[s].Abilities.Any(a => a.AbilityIndex == w.Db.Evolution(e).From))
                        ProgressionSystem.ApplyEvolution(w, s, e);
            }
            var inputs = new PlayerInput[4];
            ushort seq = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            double worst = 0;
            while (w.Phase == MatchPhase.Running && w.Elapsed < 180f)
            {
                seq++;
                for (int s = 0; s < 4; s++) inputs[s] = BotBrain.Decide(w, s, seq);
                var t0 = sw.Elapsed.TotalMilliseconds;
                w.Step(inputs);
                worst = Math.Max(worst, sw.Elapsed.TotalMilliseconds - t0);
            }
            TestContext.Out.WriteLine($"{w.Stats.Kills} abates, pior tick {worst:0.0} ms, total {sw.ElapsedMilliseconds} ms");
            Assert.That(w.Stats.Kills, Is.GreaterThan(500));
            Assert.That(w.Zones.Count, Is.LessThanOrEqualTo(32));
            Assert.That(w.Companions.ActiveCount, Is.LessThanOrEqualTo(24));
        }
    }
}
