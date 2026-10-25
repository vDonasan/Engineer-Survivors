using System;
using System.Collections.Generic;
using System.Linq;

namespace ES.SimTests.Abilities
{
    /// <summary>Os dez personagens: arma exclusiva, mecânicas por nível, evolução e melhoria avançada.</summary>
    public class CharacterTests
    {
        public static readonly string[] Heroes = { "staut", "miyuki", "giulia", "breno", "artur", "badotti", "garcia", "jean", "fufu", "donasa" };

        static World Open(string spec)
        {
            var w = TestWorlds.CreateOpen(spec: spec);
            w.SpawningEnabled = false;
            w.Players[0].InvulnUntil = float.MaxValue;
            return w;
        }

        /// <summary>Prensa parada (vida alta) em volta do jogador.</summary>
        static void Ring(World w, int count, float radius, float hp = 5000)
        {
            for (int k = 0; k < count; k++)
            {
                double a = Math.PI * 2 * k / count;
                var pos = w.Players[0].Pos + new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * (radius + (k % 3));
                int e = SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex("prensa_ambulante"), pos, false);
                w.Enemies.Hp[e] = w.Enemies.MaxHp[e] = hp;
                w.Enemies.StunUntil[e] = float.MaxValue;
            }
            EnemyAISystem.RebuildGrid(w);
        }

        static AbilityInstance Signature(World w) => w.Players[0].Abilities[0];

        static string ComponentFor(World w, int evolution) =>
            w.Db.Evolution(evolution).Def.requires.First(r => r.target != w.Db.Evolution(evolution).Def.fromAbility).target;

        /// <summary>Arma no nível 5 + componente no nível 2 → evolução aplicada; depois sobe a evoluída até <paramref name="evoLevel"/>.</summary>
        static void Evolve(World w, string spec, int evoLevel = 1)
        {
            var sig = Signature(w);
            while (AbilitySystem.LevelUp(w, 0, sig)) { }
            int evo = Enumerable.Range(0, w.Db.EvolutionCount).First(i => w.Db.Evolution(i).From == sig.AbilityIndex);
            var comp = AbilitySystem.Grant(w, 0, w.Db.AbilityIndex(ComponentFor(w, evo)));
            AbilitySystem.LevelUp(w, 0, comp);
            Assert.That(RequirementEvaluator.Met(w, 0, w.Db.Evolution(evo).Requires), Is.True, spec + ": requisitos da evolução");
            ProgressionSystem.ApplyEvolution(w, 0, evo);
            var evolved = w.Players[0].Abilities.First(a => a.AbilityIndex == w.Db.Evolution(evo).Result);
            for (int l = 1; l < evoLevel; l++) AbilitySystem.LevelUp(w, 0, evolved);
        }

        sealed class Seen
        {
            public readonly HashSet<AllySprite> Sprites = new HashSet<AllySprite>();
            public readonly List<CosmeticEvent> Events = new List<CosmeticEvent>();
            public readonly List<Zone> Zones = new List<Zone>();
            public int MaxShots, MaxCoinRicochet;
            public readonly Dictionary<AllySprite, int> PeakCompanions = new Dictionary<AllySprite, int>();
            public float Damage;
        }

        static Seen Run(World w, float seconds)
        {
            var seen = new Seen();
            float hp0 = HpSum(w);
            var inputs = TestWorlds.NoInput();
            int ticks = (int)(seconds * 30);
            for (int t = 0; t < ticks; t++)
            {
                w.Step(inputs);
                seen.Events.AddRange(w.Events.Cosmetic);
                for (int i = 0; i < w.Shots.Count; i++)
                {
                    var s = w.Shots.Items[i];
                    seen.Sprites.Add(s.Sprite);
                    if (s.Sprite == AllySprite.Coin) seen.MaxCoinRicochet = Math.Max(seen.MaxCoinRicochet, s.Ricochets);
                }
                seen.MaxShots = Math.Max(seen.MaxShots, w.Shots.Count);
                for (int i = 0; i < w.Zones.Count; i++) seen.Zones.Add(w.Zones.Items[i]);
                foreach (var g in w.Companions.Items.Where(c => c.Active).GroupBy(c => c.Sprite))
                {
                    seen.Sprites.Add(g.Key);
                    seen.PeakCompanions[g.Key] = Math.Max(seen.PeakCompanions.TryGetValue(g.Key, out int n) ? n : 0, g.Count());
                }
            }
            seen.Damage = hp0 - HpSum(w);
            return seen;
        }

        static float HpSum(World w)
        {
            float s = 0;
            for (int i = 0; i < w.Enemies.Count; i++) s += Math.Max(0, w.Enemies.Hp[i]);
            return s;
        }

        static bool Has(Seen s, CosmeticKind kind, FxStyle? style = null, float minValue = 0) =>
            s.Events.Any(e => e.Kind == kind && (style == null || (kind == CosmeticKind.ChainArc ? (int)e.Value : e.B) == (int)style.Value) && e.Value >= minValue);

        // ---------- dados ----------

        [Test]
        public void Roster_TenVisibleCharacters_EachWithExclusiveWeaponAndEvolution()
        {
            var db = TestWorlds.Db;
            var visible = Enumerable.Range(0, db.SpecializationCount).Select(db.Specialization).Where(s => !s.Def.hidden).ToArray();
            Assert.That(visible.Select(s => s.Def.id), Is.EquivalentTo(Heroes));
            foreach (var s in visible)
            {
                var weapon = db.Ability(s.StartingAbility);
                Assert.That(weapon.Def.exclusive, Is.True, s.Def.id);
                Assert.That(weapon.MaxLevel, Is.EqualTo(5), s.Def.id);
                var evo = Enumerable.Range(0, db.EvolutionCount).Select(db.Evolution).Single(e => e.From == s.StartingAbility);
                var result = db.Ability(evo.Result);
                Assert.That(result.Def.exclusive && result.Def.evolutionOnly, Is.True, s.Def.id);
                Assert.That(result.Def.behavior, Is.EqualTo(weapon.Def.behavior), s.Def.id + ": a evoluída usa o mesmo comportamento");
                Assert.That(result.MaxLevel, Is.EqualTo(3), s.Def.id + ": evoluída com nível avançado");
                Assert.That(weapon.Def.sfx.Length, Is.GreaterThan(0), s.Def.id + ": sons de disparo");
                Assert.That(string.IsNullOrEmpty(s.Def.engineering), Is.False, s.Def.id);
            }
        }

        [Test]
        public void ExclusiveWeapons_AreNeverOffered()
        {
            var w = Open("staut");
            var exclusive = Enumerable.Range(0, w.Db.AbilityCount).Where(i => w.Db.Ability(i).Def.exclusive).ToHashSet();
            for (short o = 1; o <= 300; o++)
            {
                var offer = OfferGenerator.Generate(w, 0, o);
                foreach (var opt in offer.Options)
                    if (opt.Kind == OptionKind.NewAbility) Assert.That(exclusive.Contains(opt.Ref), Is.False, w.Db.Ability(opt.Ref).Id);
            }
        }

        // ---------- ataque inicial ----------

        static readonly Dictionary<string, Func<Seen, bool>> BaseAttack = new Dictionary<string, Func<Seen, bool>>
        {
            ["staut"] = s => s.Sprites.Contains(AllySprite.Bubble),
            ["miyuki"] = s => s.Sprites.Contains(AllySprite.Digit),
            ["giulia"] = s => s.Sprites.Contains(AllySprite.Resistor) && s.Sprites.Contains(AllySprite.Transistor),
            ["breno"] = s => s.Sprites.Contains(AllySprite.Dorito) && s.Sprites.Contains(AllySprite.Burger),
            ["artur"] = s => Has(s, CosmeticKind.Swing, FxStyle.Impact),
            ["badotti"] = s => s.PeakCompanions.ContainsKey(AllySprite.BotShooter) && s.PeakCompanions.ContainsKey(AllySprite.BotBrawler) && s.PeakCompanions.ContainsKey(AllySprite.BotDasher),
            ["garcia"] = s => s.Sprites.Contains(AllySprite.Phone) && s.Sprites.Contains(AllySprite.Paper),
            ["jean"] = s => s.Sprites.Contains(AllySprite.Jabuti),
            ["fufu"] = s => Has(s, CosmeticKind.Swing, FxStyle.Impact),
            ["donasa"] = s => Has(s, CosmeticKind.Burst, FxStyle.Sound),
        };

        static readonly Dictionary<string, Func<Seen, bool>> EvolvedAttack = new Dictionary<string, Func<Seen, bool>>
        {
            ["staut"] = s => s.Sprites.Contains(AllySprite.Briefcase) && s.Sprites.Contains(AllySprite.Coin) && Has(s, CosmeticKind.Burst, FxStyle.Coins),
            ["miyuki"] = s => s.Sprites.Contains(AllySprite.Sushi) && s.Sprites.Contains(AllySprite.Sashimi) && Has(s, CosmeticKind.Burst, FxStyle.Math),
            ["giulia"] = s => s.Sprites.Contains(AllySprite.PowerSupply) && s.Sprites.Contains(AllySprite.ScopeWave) && Has(s, CosmeticKind.ChainArc, FxStyle.Circuit),
            ["breno"] = s => s.Sprites.Contains(AllySprite.Tire),
            ["artur"] = s => s.Sprites.Contains(AllySprite.Pill) && s.Sprites.Contains(AllySprite.Syringe) && s.Sprites.Contains(AllySprite.SyringeSpecial),
            ["badotti"] = s => s.PeakCompanions.TryGetValue(AllySprite.BotMaster, out int n) && n == 1,
            ["garcia"] = s => s.Sprites.Contains(AllySprite.Computer) && s.Sprites.Contains(AllySprite.Fragment) && Has(s, CosmeticKind.AreaMark, FxStyle.Tech),
            ["jean"] = s => s.PeakCompanions.ContainsKey(AllySprite.Car) && s.Sprites.Contains(AllySprite.JabutiWalk),
            ["fufu"] = s => s.Sprites.Contains(AllySprite.Insulin) && s.Sprites.Contains(AllySprite.Vial) && Has(s, CosmeticKind.Burst, FxStyle.Insulin),
            ["donasa"] = s => s.Sprites.Contains(AllySprite.Bottle) && Has(s, CosmeticKind.Burst, FxStyle.Glass),
        };

        static readonly Dictionary<string, Func<Seen, bool>> Advanced = new Dictionary<string, Func<Seen, bool>>
        {
            ["staut"] = s => s.MaxCoinRicochet > 0,
            ["miyuki"] = s => Has(s, CosmeticKind.AreaMark, FxStyle.Food),
            ["giulia"] = s => s.Zones.Any(z => z.Sprite == AllySprite.ZoneStorm) && Has(s, CosmeticKind.Burst, FxStyle.Storm),
            ["breno"] = s => s.PeakCompanions.TryGetValue(AllySprite.TireOrbit, out int n) && n == 2,
            ["artur"] = s => s.MaxShots >= 8,
            ["badotti"] = s => s.Events.Any(e => e.Kind == CosmeticKind.AreaMark && e.B == (int)FxStyle.Robot && e.X2 >= 5f),
            ["garcia"] = s => s.Events.Count(e => e.Kind == CosmeticKind.AreaMark && e.B == (int)FxStyle.Tech) >= 10,
            ["jean"] = s => s.Events.Any(e => e.Kind == CosmeticKind.Burst && e.B == (int)FxStyle.Drift && e.Value >= 3.5f),
            ["fufu"] = s => s.Sprites.Contains(AllySprite.Insulin),
            ["donasa"] = s => s.Events.Any(e => e.Kind == CosmeticKind.Burst && e.B == (int)FxStyle.Sound && e.Value >= 6f),
        };

        [TestCaseSource(nameof(Heroes))]
        public void BaseAttack_DealsRealDamage(string spec)
        {
            var w = Open(spec);
            Ring(w, 10, 2.5f);
            var s = Run(w, 6f);
            Assert.That(s.Damage, Is.GreaterThan(100f), spec + ": dano nos alvos");
            Assert.That(BaseAttack[spec](s), Is.True, spec + ": ataque inicial reconhecível");
            Assert.That(w.Stats.DamageByAbility[0][Signature(w).AbilityIndex], Is.GreaterThan(0), spec + ": dano atribuído à arma");
        }

        [TestCaseSource(nameof(Heroes))]
        public void Evolution_AddsNewAttack_AndKeepsTheInitial(string spec)
        {
            var w = Open(spec);
            Evolve(w, spec);
            Ring(w, 14, 2.5f);
            var s = Run(w, 12f);
            Assert.That(EvolvedAttack[spec](s), Is.True, spec + ": mecânica da evolução");
            if (spec == "badotti")
                Assert.That(s.PeakCompanions.Keys, NUnit.Framework.Has.None.AnyOf(AllySprite.BotShooter, AllySprite.BotBrawler, AllySprite.BotDasher), "o Master substitui os três");
            else
                Assert.That(BaseAttack[spec](s), Is.True, spec + ": o ataque inicial continua");
        }

        [TestCaseSource(nameof(Heroes))]
        public void AdvancedUpgrade_UnlocksAtEvolutionLevel3(string spec)
        {
            var w3 = Open(spec);
            Evolve(w3, spec, 3);
            Ring(w3, 14, 2.5f);
            Assert.That(Advanced[spec](Run(w3, 14f)), Is.True, spec + ": melhoria avançada");
        }

        [TestCase("staut"), TestCase("giulia"), TestCase("garcia"), TestCase("jean"), TestCase("breno")]
        public void AdvancedUpgrade_IsAbsentBeforeLevel3(string spec)
        {
            var w = Open(spec);
            Evolve(w, spec, 2);
            Ring(w, 14, 2.5f);
            Assert.That(Advanced[spec](Run(w, 14f)), Is.False, spec);
        }

        // ---------- mecânicas por nível ----------

        static void Level(World w, int level)
        {
            var sig = Signature(w);
            while (sig.Level < level) AbilitySystem.LevelUp(w, 0, sig);
        }

        [Test]
        public void Staut_Level3_BigBubbleTrapsEnemy()
        {
            var w = Open("staut");
            Level(w, 3);
            Ring(w, 6, 2f);
            bool trapped = false;
            for (int t = 0; t < 30 * 8 && !trapped; t++)
            {
                w.Step(TestWorlds.NoInput());
                for (int i = 0; i < w.Enemies.Count; i++) trapped |= w.Enemies.TrapUntil[i] > w.Elapsed;
            }
            Assert.That(trapped, Is.True);
        }

        [Test]
        public void Staut_Level1_NoTrap()
        {
            var w = Open("staut");
            Ring(w, 6, 2f);
            for (int t = 0; t < 30 * 8; t++)
            {
                w.Step(TestWorlds.NoInput());
                for (int i = 0; i < w.Enemies.Count; i++) Assert.That(w.Enemies.TrapUntil[i], Is.EqualTo(0));
            }
        }

        [Test]
        public void Giulia_ClusteredHits_CreateElectricField()
        {
            var w = Open("giulia");
            // grupo compacto: os acertos caem na mesma região
            for (int k = 0; k < 6; k++)
            {
                int e = SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex("prensa_ambulante"), w.Players[0].Pos + new Vec2(4f + (k % 2) * 0.6f, (k / 2) * 0.6f), false);
                w.Enemies.Hp[e] = 5000;
                w.Enemies.StunUntil[e] = float.MaxValue;
            }
            EnemyAISystem.RebuildGrid(w);
            Assert.That(Run(w, 8f).Zones.Any(z => z.Sprite == AllySprite.ZoneElectric), Is.True);
        }

        [Test]
        public void Garcia_Paper_SticksThenBursts()
        {
            var w = Open("garcia");
            Ring(w, 6, 2.5f);
            bool stuck = false;
            for (int t = 0; t < 30 * 6; t++)
            {
                w.Step(TestWorlds.NoInput());
                for (int i = 0; i < w.Enemies.Count; i++) stuck |= w.Enemies.DelayAt[i] > w.Elapsed;
                if (stuck && w.Events.Cosmetic.Any(e => e.Kind == CosmeticKind.Burst && e.B == (int)FxStyle.Paper)) return;
            }
            Assert.Fail("a cola não grudou e estourou");
        }

        [Test]
        public void Miyuki_Evolved_DigitsMark()
        {
            var w = Open("miyuki");
            Evolve(w, "miyuki");
            Ring(w, 8, 3f);
            bool marked = false;
            for (int t = 0; t < 30 * 4 && !marked; t++)
            {
                w.Step(TestWorlds.NoInput());
                for (int i = 0; i < w.Enemies.Count; i++) marked |= w.Enemies.MarkUntil[i] > w.Elapsed;
            }
            Assert.That(marked, Is.True);
        }

        [Test]
        public void Badotti_Evolution_RemovesTheThreeBots()
        {
            var w = Open("badotti");
            Ring(w, 6, 3f);
            Run(w, 1f);
            Assert.That(w.Companions.ActiveCount, Is.EqualTo(3));
            Evolve(w, "badotti");
            Assert.That(w.Companions.ActiveCount, Is.EqualTo(0), "robôs removidos na evolução");
            w.Step(TestWorlds.NoInput());
            Assert.That(w.Companions.Items.Where(c => c.Active).Select(c => c.Sprite), Is.EqualTo(new[] { AllySprite.BotMaster }));
        }

        [Test]
        public void Fufu_Fury_ShortensCooldown()
        {
            var w = Open("fufu");
            Ring(w, 8, 1.2f);
            int swings = Run(w, 6f).Events.Count(e => e.Kind == CosmeticKind.Swing);
            // sem fúria: 6 s / 0,7 s ≈ 8 socos; com fúria (até −30%) passa disso
            Assert.That(swings, Is.GreaterThan(9));
        }

        // ---------- kit ----------

        [Test]
        public void Shot_Ricochet_RetargetsAnotherEnemy()
        {
            var w = Open("staut");
            w.Players[0].Abilities.Clear();
            Ring(w, 2, 3f);
            var a = w.Enemies.Pos[0];
            w.Shots.Spawn(new Shot { Owner = 0, Ability = 0, Pos = a - new Vec2(1, 0), Vel = new Vec2(10, 0), Damage = 5, Radius = 0.3f, Life = 2, Pierce = 1, Ricochets = 1, RicochetRange = 8 });
            float hp1 = w.Enemies.Hp[1];
            for (int t = 0; t < 60; t++) w.Step(TestWorlds.NoInput());
            Assert.That(w.Enemies.Hp[0], Is.LessThan(5000));
            Assert.That(w.Enemies.Hp[1], Is.LessThan(hp1), "ricocheteou no segundo");
        }

        [Test]
        public void Shot_Roll_BouncesOffWalls()
        {
            var w = Open("staut");
            w.Players[0].Abilities.Clear();
            var start = new Vec2(w.Arena.width - 2, 10);
            w.Shots.Spawn(new Shot { Owner = 0, Ability = 0, Motion = ShotMotion.Roll, Curve = 0.5f, Pos = start, Vel = new Vec2(8, 0), Radius = 0.4f, Life = 5, Pierce = 99 });
            for (int t = 0; t < 30; t++) w.Step(TestWorlds.NoInput());
            Assert.That(w.Shots.Count, Is.EqualTo(1));
            Assert.That(w.Shots.Items[0].Vel.X, Is.LessThan(0), "voltou da parede");
            Assert.That(w.Shots.Items[0].Pos.X, Is.LessThan(w.Arena.width));
        }

        [Test]
        public void Chain_IsCappedAndNeverRepeatsTargets()
        {
            var w = Open("giulia");
            w.Players[0].Abilities.Clear();
            for (int k = 0; k < 40; k++)
            {
                int e = SpawnDirector.SpawnEnemy(w, 0, w.Players[0].Pos + new Vec2(1 + k * 0.5f, 0), false);
                w.Enemies.Hp[e] = 9999;
            }
            EnemyAISystem.RebuildGrid(w);
            w.Events.Clear();
            int hits = Area.Chain(w, 0, 0, w.Players[0].Pos, 0, 999, 3f, 1f, 0f, FxStyle.Electric);
            Assert.That(hits, Is.EqualTo(13), "1 + 12 saltos no máximo");
            var targets = w.Events.Cosmetic.Where(c => c.Kind == CosmeticKind.ChainArc).Select(c => (c.X2, c.Y2)).ToList();
            Assert.That(targets.Distinct().Count(), Is.EqualTo(targets.Count));
        }

        [Test]
        public void ChildShots_StopAtMaxDepth()
        {
            var w = Open("staut");
            Evolve(w, "staut", 3);
            Ring(w, 20, 2f);
            Run(w, 10f);
            for (int i = 0; i < w.Shots.Count; i++) Assert.That(w.Shots.Items[i].Depth, Is.LessThanOrEqualTo(Kit.MaxDepth));
        }

        [Test]
        public void DamageNumbers_UseAtMostHalfTheCosmeticBudget()
        {
            var buf = new EventBuffer(10);
            for (int i = 0; i < 20; i++) buf.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.Damage });
            Assert.That(buf.Cosmetic.Count, Is.EqualTo(5));
            Assert.That(buf.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.Burst }), Is.True);
        }

        // ---------- partidas ----------

        [Test]
        public void FullMatches_EveryCharacter_EvolvedTeamOf4_NoExceptions_CapsHeld()
        {
            var report = new List<string>();
            for (int g = 0; g < Heroes.Length; g += 4)
            {
                var team = Enumerable.Range(0, 4).Select(k => Heroes[(g + k) % Heroes.Length]).ToArray();
                var roster = team.Select((id, i) => new PlayerSlot { Name = "P" + i, SpecializationId = id }).ToArray();
                var cfg = new MatchConfig { Seed = (ulong)(g + 7), Difficulty = ES.Sim.Difficulty.Hard, TeamSize = 4, ArenaId = "laboratorio", Roster = roster, Coop = true };
                var w = new World(TestWorlds.Db, cfg);
                foreach (var p in w.Players.Slots) p.InvulnUntil = float.MaxValue;
                for (int s = 0; s < 4; s++) EvolveSlot(w, s);
                var inputs = new PlayerInput[4];
                ushort seq = 0;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                double worst = 0;
                while (w.Phase == MatchPhase.Running && w.Elapsed < 240f)
                {
                    seq++;
                    for (int s = 0; s < 4; s++) inputs[s] = BotBrain.Decide(w, s, seq);
                    var t0 = sw.Elapsed.TotalMilliseconds;
                    w.Step(inputs);
                    worst = Math.Max(worst, sw.Elapsed.TotalMilliseconds - t0);
                    Assert.That(w.Enemies.Count, Is.LessThanOrEqualTo(400));
                    Assert.That(w.Shots.Count, Is.LessThanOrEqualTo(w.Shots.Capacity));
                }
                report.Add($"{string.Join(",", team)}: {w.Stats.Kills} abates, {w.Tick} ticks em {sw.ElapsedMilliseconds} ms, pior tick {worst:0.0} ms, tiros pico {w.Shots.Count}");
                Assert.That(w.Stats.Kills, Is.GreaterThan(200), report.Last());
            }
            TestContext.Out.WriteLine(string.Join("\n", report));
        }

        static void EvolveSlot(World w, int slot)
        {
            var sig = w.Players[slot].Abilities[0];
            while (AbilitySystem.LevelUp(w, slot, sig)) { }
            int evo = Enumerable.Range(0, w.Db.EvolutionCount).First(i => w.Db.Evolution(i).From == sig.AbilityIndex);
            var comp = AbilitySystem.Grant(w, slot, w.Db.AbilityIndex(ComponentFor(w, evo)));
            AbilitySystem.LevelUp(w, slot, comp);
            ProgressionSystem.ApplyEvolution(w, slot, evo);
            var evolved = w.Players[slot].Abilities.First(a => a.AbilityIndex == w.Db.Evolution(evo).Result);
            while (AbilitySystem.LevelUp(w, slot, evolved)) { }
        }
    }
}
