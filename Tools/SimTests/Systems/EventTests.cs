using System;
using System.Collections.Generic;
using System.Linq;

namespace ES.SimTests.Systems
{
    /// <summary>Eventos aleatórios: agenda, os oito comportamentos, rede e aparência.</summary>
    public class EventTests
    {
        static World Fresh(int team = 1)
        {
            var w = TestWorlds.Disarm(TestWorlds.CreateOpen(team: team, coop: team > 1));
            w.SpawningEnabled = false;
            return w;
        }

        static int Ev(World w, string id) => w.Db.EventIndex(id);

        static void Step(World w, float seconds, List<RuleEvent> log = null)
        {
            float end = w.Elapsed + seconds;
            while (w.Elapsed < end - 1e-4f)
            {
                foreach (var p in w.Players.Slots)
                    if (p != null) { p.Progress.Current = null; p.Progress.PendingLevels = 0; }
                w.Step(TestWorlds.NoInput());
                log?.AddRange(w.Events.Rule);
            }
        }

        static float ModSum(PlayerState p, StatId stat, ScopeKind scope)
        {
            float s = 0;
            for (int i = 0; i < p.Stats.ModifierCount; i++)
            {
                var m = p.Stats.ModifierAt(i);
                if (m.Stat == stat && m.Scope.Kind == scope) s += m.Value;
            }
            return s;
        }

        static int Enemy(World w, Vec2 pos, string id = "prensa_ambulante") => SpawnDirector.SpawnEnemy(w, w.Db.EnemyIndex(id), pos, false);

        static void KillNear(World w, int slot, int count)
        {
            var p = w.Players[slot];
            for (int k = 0; k < count; k++)
            {
                int e = Enemy(w, p.Pos + new Vec2(6, 0), "microbo_enxame");
                CombatSystem.DamageEnemy(w, e, 99999, Vec2.Zero, 0, slot, 0);
                CombatSystem.Cleanup(w);
            }
        }

        [Test]
        public void Content_EightEvents_AllBehaviorsKnown()
        {
            var db = TestWorlds.Db;
            Assert.That(db.EventCount, Is.EqualTo(8));
            foreach (var id in new[] { "bolha_social", "dez_na_prova", "zero_na_prova", "chamado_de_burro", "laboratorio_incompleto", "motoqueiro", "coxinha", "jalapeno" })
                Assert.That(db.EventIndex(id), Is.GreaterThanOrEqualTo(0), id);
            for (int i = 0; i < db.EventCount; i++)
            {
                var e = db.Event(i);
                Assert.That(EventSystem.KnownBehaviors.Contains(e.Def.behavior), Is.True);
                Assert.That(e.Def.banner, Is.Not.Empty, e.Def.id);
                Assert.That(e.Def.duration, Is.GreaterThan(0));
            }
        }

        [Test]
        public void NewEvent_ByConfigurationOnly_ReusesBehavior()
        {
            var def = ContentLoader.LoadSp1();
            var extra = def.events.First(e => e.id == "coxinha");
            def.events = def.events.Append(new EventDef
            {
                id = "pastel", name = "Ganhou um Pastel", description = "x", banner = "x", kind = "positivo", scope = "player", behavior = extra.behavior,
                weight = 5, rarity = "comum", duration = 20, parameters = new[] { new ParamDef { key = "heal", value = 0.5f } },
            }).ToArray();
            var r = GameDatabase.Build(def);
            Assert.That(r.Errors, Is.Empty);
            Assert.That(r.Db.Event(r.Db.EventIndex("pastel")).Param("heal"), Is.EqualTo(0.5f));
            def.events[0].behavior = "inexistente";
            Assert.That(GameDatabase.Build(def).Errors.Any(e => e.Contains("behavior desconhecido")), Is.True);
        }

        [Test]
        public void Schedule_FirstAt75s_IntervalsInRange_NoImmediateRepeat_MaxActive()
        {
            var w = Fresh(team: 2);
            w.EventDir.Enabled = true;
            var log = new List<RuleEvent>();
            var times = new List<float>();
            float end = 1500;
            while (w.Elapsed < end)
            {
                foreach (var p in w.Players.Slots) if (p != null) { p.InvulnUntil = float.MaxValue; p.Progress.Current = null; p.Progress.PendingLevels = 0; }
                w.Step(TestWorlds.NoInput());
                foreach (var e in w.Events.Rule) if (e.Kind == RuleEventKind.EventStarted) { log.Add(e); times.Add(w.Elapsed); }
                Assert.That(w.EventDir.Active.Count(a => !a.Child), Is.LessThanOrEqualTo(w.Rules.eventMaxActive));
            }
            Assert.That(times[0], Is.EqualTo(75f).Within(0.1f));
            Assert.That(log.Count, Is.GreaterThanOrEqualTo(10));
            for (int i = 1; i < times.Count; i++) Assert.That(times[i] - times[i - 1], Is.InRange(59.9f, 120.1f));
            for (int i = 1; i < log.Count; i++) Assert.That(log[i].A, Is.Not.EqualTo(log[i - 1].A), "não repete em sequência");
            Assert.That(log.Select(e => e.A).Distinct().Count(), Is.GreaterThanOrEqualTo(6), "variedade");
            // minMinute: o laboratório nunca antes de 2 min
            var lab = Ev(w, "laboratorio_incompleto");
            for (int i = 0; i < log.Count; i++) if (log[i].A == lab) Assert.That(times[i], Is.GreaterThanOrEqualTo(120f));
        }

        [Test]
        public void Schedule_ContinuesInInfiniteMode_Faster()
        {
            var w = Fresh();
            w.EventDir.Enabled = true;
            w.Phase = MatchPhase.Victory;
            BossSystem.ContinueInfinite(w);
            var times = new List<float>();
            float end = w.Elapsed + 900;
            while (w.Elapsed < end)
            {
                foreach (var p in w.Players.Slots) if (p != null) { p.InvulnUntil = float.MaxValue; p.Progress.Current = null; p.Progress.PendingLevels = 0; }
                w.Step(TestWorlds.NoInput());
                if (w.Events.Rule.Any(e => e.Kind == RuleEventKind.EventStarted)) times.Add(w.Elapsed);
            }
            Assert.That(times.Count, Is.GreaterThanOrEqualTo(9));
            for (int i = 1; i < times.Count; i++) Assert.That(times[i] - times[i - 1], Is.LessThanOrEqualTo(120f * w.Rules.eventInfiniteInterval + 0.1f));
        }

        [Test]
        public void NoBossEvents_WaitForBossToLeave()
        {
            var w = Fresh();
            BossSystem.Spawn(w, 0, 0);
            w.Elapsed = 300;
            for (int k = 0; k < 200; k++)
            {
                w.EventDir.Recent.Clear();
                w.EventDir.Active.Clear();
                var picked = typeof(EventSystem).GetMethod("Pick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { w });
                Assert.That((int)picked, Is.Not.EqualTo(Ev(w, "laboratorio_incompleto")));
            }
        }

        [Test]
        public void Bubble_XpGain_Lure_GoalReward_Pop_AndCleanup()
        {
            var w = Fresh();
            var p = w.Players[0];
            var log = new List<RuleEvent>();
            var a = EventSystem.Begin(w, Ev(w, "bolha_social"), 0);
            Step(w, 0.1f, log);
            Assert.That(p.Stats.Get(StatId.XpGain), Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(EventSystem.Lure(w, p.Pos + new Vec2(6, 0)), Is.SameAs(p), "atrai inimigos próximos");
            Assert.That(EventSystem.Lure(w, p.Pos + new Vec2(20, 0)), Is.Null);
            Assert.That(EventSystem.Look(w, p) & PlayerLook.Bubble, Is.Not.Zero);
            float xp = EventSystem.TotalXp(w, p);
            KillNear(w, 0, 26);
            Step(w, 0.1f, log);
            Assert.That(a.Success, Is.True);
            Assert.That(EventSystem.TotalXp(w, p), Is.GreaterThan(xp));
            Assert.That(log.Any(e => e.Kind == RuleEventKind.EventBonus && e.B == EventOutcome.BonusBubbleGoal), Is.True);
            ProgressionSystem.AddXp(w, p, XpCurve.ToNext(w.Rules, p.Progress.Level) * 2, false);
            Step(w, 0.1f, log);
            Assert.That(log.Any(e => e.Kind == RuleEventKind.EventBonus && e.B == EventOutcome.BonusBubblePop), Is.True, "cheia, estoura");
            Step(w, 21f, log);
            Assert.That(p.Stats.Get(StatId.XpGain), Is.EqualTo(0f).Within(1e-4f), "bônus termina");
            Assert.That(log.Any(e => e.Kind == RuleEventKind.EventEnded && e.B == EventOutcome.Success), Is.True);
        }

        [Test]
        public void Grade_GivesXpToEveryone_CanCompleteBar_AndLuck()
        {
            var w = Fresh(team: 2);
            var p0 = w.Players[0];
            p0.Progress.Xp = XpCurve.ToNext(w.Rules, p0.Progress.Level) * 0.6f;
            int level0 = p0.Progress.Level;
            EventSystem.Begin(w, Ev(w, "dez_na_prova"), -1);
            w.Step(TestWorlds.NoInput());
            Assert.That(p0.Progress.Level, Is.EqualTo(level0 + 1), "perto de subir: completa a barra");
            Assert.That(w.Players[1].Progress.Xp, Is.GreaterThan(0));
            Assert.That(p0.Progress.LuckUntil, Is.GreaterThan(w.Elapsed + 25));
        }

        [Test]
        public void Zero_LosesUnconvertedXpOnly_PenaltyAndHaste_RecoverOnGoal()
        {
            var w = Fresh();
            var p = w.Players[0];
            p.Progress.Level = 5;
            p.Progress.Xp = 40;
            int e = Enemy(w, p.Pos + new Vec2(3, 0));
            var a = EventSystem.Begin(w, Ev(w, "zero_na_prova"), 0);
            w.Step(TestWorlds.NoInput());
            Assert.That(p.Progress.Level, Is.EqualTo(5), "nunca reduz o nível");
            Assert.That(p.Progress.Xp, Is.EqualTo(30).Within(0.01f));
            Assert.That(ModSum(p, StatId.Cooldown, ScopeKind.AllAbilities), Is.EqualTo(0.15f).Within(1e-4f));
            Assert.That(w.Enemies.SlowAmount[0], Is.LessThan(0), "inimigo próximo acelerado");
            KillNear(w, 0, 20);
            Step(w, 0.1f);
            Assert.That(a.Success, Is.True);
            Assert.That(ModSum(p, StatId.Cooldown, ScopeKind.AllAbilities), Is.EqualTo(0f), "penalidade removida");
            Assert.That(p.Progress.Xp + p.Progress.Level * 1000, Is.GreaterThan(30 + 5 * 1000), "XP devolvido com bônus");
        }

        [Test]
        public void Zero_Expires_RemovesPenalty_WithoutRecovery()
        {
            var w = Fresh();
            var p = w.Players[0];
            p.Progress.Xp = 20;
            EventSystem.Begin(w, Ev(w, "zero_na_prova"), 0);
            Step(w, 19f);
            Assert.That(ModSum(p, StatId.Cooldown, ScopeKind.AllAbilities), Is.EqualTo(0f));
            Assert.That(p.Progress.Xp, Is.EqualTo(15).Within(0.01f));
        }

        [Test]
        public void Insult_SlowerAttacks_XpPerKill_GoalGivesDamage()
        {
            var w = Fresh();
            var p = w.Players[0];
            var a = EventSystem.Begin(w, Ev(w, "chamado_de_burro"), 0);
            w.Step(TestWorlds.NoInput());
            Assert.That(ModSum(p, StatId.Cooldown, ScopeKind.AllAbilities), Is.EqualTo(0.12f).Within(1e-4f));
            float before = EventSystem.TotalXp(w, p);
            KillNear(w, 0, 3);
            Step(w, 0.1f);
            Assert.That(EventSystem.TotalXp(w, p), Is.GreaterThan(before), "XP extra por abate");
            KillNear(w, 0, 13);
            Step(w, 0.1f);
            Assert.That(a.Success, Is.True);
            Assert.That(ModSum(p, StatId.Cooldown, ScopeKind.AllAbilities), Is.EqualTo(0f));
            Assert.That(ModSum(p, StatId.Damage, ScopeKind.AllAbilities), Is.EqualTo(0.2f).Within(1e-4f));
            Step(w, 9f);
            Assert.That(ModSum(p, StatId.Damage, ScopeKind.AllAbilities), Is.EqualTo(0f), "bônus de dano é breve");
        }

        [Test]
        public void Lab_WarningThenWavesAndHazards_GoalRewards_ResetsSpawn()
        {
            var w = Fresh(team: 2);
            var log = new List<RuleEvent>();
            var a = EventSystem.Begin(w, Ev(w, "laboratorio_incompleto"), -1);
            Assert.That(log.Count == 0 && w.Events.Rule.Any(e => e.Kind == RuleEventKind.EventStarted && e.Y > 2.9f), Is.True, "aviso antes");
            int before = w.Enemies.Count;
            Step(w, 2.5f, log);
            Assert.That(w.EventDir.SpawnMult, Is.EqualTo(1f), "nada durante o aviso");
            Step(w, 5f, log);
            Assert.That(w.EventDir.SpawnMult, Is.GreaterThan(1.5f));
            Assert.That(w.Enemies.Count, Is.GreaterThan(before), "onda extra");
            Assert.That(log.Count(e => e.Kind == RuleEventKind.BossHazard && e.A != 255), Is.GreaterThan(0), "vazamentos sinalizados");
            Assert.That(a.Goal, Is.EqualTo(80));
            float xp = EventSystem.TotalXp(w, w.Players[1]);
            KillNear(w, 0, 80);
            Step(w, 0.1f, log);
            Assert.That(a.Success, Is.True);
            Assert.That(w.EventDir.SpawnMult, Is.EqualTo(1f));
            Assert.That(EventSystem.TotalXp(w, w.Players[1]), Is.GreaterThan(xp), "recompensa para todos");
        }

        [Test]
        public void Moto_Warns_HitsStandingPlayerAndEnemies_DodgerUnhurt()
        {
            var w = Fresh();
            var p = w.Players[0];
            p.InvulnUntil = 0;
            float hp = p.Hp;
            var a = EventSystem.Begin(w, Ev(w, "motoqueiro"), -1);
            var moto = w.Events.Rule.First(e => e.Kind == RuleEventKind.EventMoto);
            Assert.That(moto.C, Is.GreaterThan(1000), "aviso de ~1,6 s");
            // inimigos na pista
            var side = new Vec2(-a.Dir.Y, a.Dir.X) * 1.1f;
            // inimigos parados na pista, longe do jogador (atordoados: não andam durante o aviso)
            for (int k = 2; k <= 6; k++) { int en = Enemy(w, p.Pos + side + a.Dir * (k * 1.5f), "microbo_enxame"); w.Enemies.StunUntil[en] = 99; }
            int kills = w.Stats.Kills;
            Step(w, 1.5f);
            Assert.That(p.Hp, Is.EqualTo(hp), "nada durante o aviso");
            Step(w, 2.5f);
            Assert.That(p.Hp, Is.LessThan(hp), "parado na pista: atropelado");
            Assert.That(w.Stats.Kills - kills, Is.GreaterThanOrEqualTo(3));

            var w2 = Fresh();
            var p2 = w2.Players[0];
            float hp2 = p2.Hp;
            var b = EventSystem.Begin(w2, Ev(w2, "motoqueiro"), -1);
            var side2 = new Vec2(-b.Dir.Y, b.Dir.X) * 1.1f;
            for (int k = 2; k <= 6; k++) { int en = Enemy(w2, p2.Pos + side2 + b.Dir * (k * 1.5f), "microbo_enxame"); w2.Enemies.StunUntil[en] = 99; }
            p2.Pos -= side2 * 1.8f; // desviou para o outro lado, perto da pista
            var log = new List<RuleEvent>();
            Step(w2, 4f, log);
            Assert.That(p2.Hp, Is.EqualTo(hp2));
            Assert.That(log.Any(e => e.Kind == RuleEventKind.EventBonus && e.B == EventOutcome.BonusReflex), Is.True, "Reflexo de Piloto");
            Assert.That(EventSystem.Look(w2, p2) & PlayerLook.Reflex, Is.Not.Zero);
        }

        [Test]
        public void Coxinha_IsTotem_DropsXp_BuffsWhoeverIsInRange()
        {
            var w = Fresh(team: 2);
            float speedIn = w.Players[0].Stats.Get(StatId.MoveSpeed), speedOut = w.Players[1].Stats.Get(StatId.MoveSpeed);
            var a = EventSystem.Begin(w, Ev(w, "coxinha"), 1);
            var log = new List<RuleEvent>();
            Step(w, 0.1f, log);
            var placed = log.First(e => e.Kind == RuleEventKind.EventTotem);
            var at = new Vec2(placed.X, placed.Y);
            Assert.That(placed.Z, Is.GreaterThanOrEqualTo(3.5f), "alcance do totem");
            var inside = w.Players[0];
            var outside = w.Players[1];
            inside.Pos = at + new Vec2(1, 0);
            outside.Pos = at + new Vec2(15, 0);
            int gems = w.Gems.Count;
            Step(w, 3f, log);
            Assert.That(inside.Stats.Get(StatId.MoveSpeed), Is.GreaterThan(speedIn), "quem está no alcance ganha velocidade");
            Assert.That(ModSum(inside, StatId.Damage, ScopeKind.AllAbilities), Is.EqualTo(0.15f).Within(1e-3f));
            Assert.That(outside.Stats.Get(StatId.MoveSpeed), Is.EqualTo(speedOut), "fora do alcance, nada");
            Assert.That(w.Gems.Count, Is.GreaterThan(gems), "solta experiência");
            // saiu do alcance: perde o bônus
            inside.Pos = at + new Vec2(15, 5);
            Step(w, 0.2f, log);
            Assert.That(inside.Stats.Get(StatId.MoveSpeed), Is.EqualTo(speedIn).Within(1e-3f));
            Step(w, 20f, log);
            Assert.That(log.Any(e => e.Kind == RuleEventKind.EventEnded && e.A == a.Id), Is.True);
            Assert.That(ModSum(inside, StatId.Damage, ScopeKind.AllAbilities), Is.EqualTo(0f));
        }

        [Test]
        public void CoxinhaSuprema_PulsesDamage_LargerRange()
        {
            var w = Fresh();
            w.Rng = new Rng(1);
            ActiveEvent a = null;
            // força a Suprema (chance configurável): tenta sementes até sair
            for (ulong seed = 1; seed < 200; seed++)
            {
                w = Fresh();
                w.Rng = new Rng(seed);
                a = EventSystem.Begin(w, Ev(w, "coxinha"), 0);
                w.Step(TestWorlds.NoInput());
                if (a.C == 1) break;
            }
            Assert.That(a.C, Is.EqualTo(1), "Suprema sorteada");
            int e = Enemy(w, a.Pos + new Vec2(1.5f, 0));
            w.Enemies.StunUntil[e] = 99;
            float hp = w.Enemies.Hp[e];
            Step(w, 2.5f);
            Assert.That(w.Enemies.Hp[0], Is.LessThan(hp), "pulso de dano");
            Assert.That(a.A, Is.GreaterThanOrEqualTo(4.5f));
        }

        [Test]
        public void Jalapeno_Buffs_Burns_EndExplosionAndSlow_CarrotCombo()
        {
            var w = Fresh();
            var p = w.Players[0];
            int e = Enemy(w, p.Pos + new Vec2(1.5f, 0));
            float ehp = w.Enemies.Hp[e];
            EventSystem.Begin(w, Ev(w, "jalapeno"), 0);
            Step(w, 1f);
            Assert.That(ModSum(p, StatId.Damage, ScopeKind.AllAbilities), Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(ModSum(p, StatId.Cooldown, ScopeKind.AllAbilities), Is.EqualTo(-0.2f).Within(1e-4f));
            Assert.That(w.Enemies.Hp[0], Is.LessThan(ehp), "queimadura ao redor");
            Assert.That(EventSystem.Look(w, p) & PlayerLook.Jalapeno, Is.Not.Zero);
            float speed = p.Stats.Get(StatId.MoveSpeed);
            Step(w, 9.5f);
            Assert.That(ModSum(p, StatId.Damage, ScopeKind.AllAbilities), Is.EqualTo(0f));
            Assert.That(p.Stats.Get(StatId.MoveSpeed), Is.LessThan(speed), "alívio: breve lentidão");
            Step(w, 2f);
            Assert.That(p.Stats.Get(StatId.MoveSpeed), Is.EqualTo(speed).Within(1e-3f));

            var w2 = Fresh();
            var q = w2.Players[0];
            AbilitySystem.Grant(w2, 0, w2.Db.AbilityIndex("cenoura_podre"));
            int e2 = Enemy(w2, q.Pos + new Vec2(2.5f, 0));
            EventSystem.Begin(w2, Ev(w2, "jalapeno"), 0);
            Step(w2, 0.6f);
            Assert.That(w2.Enemies.DotUntil[0], Is.GreaterThan(w2.Elapsed), "nuvem picante com a Cenoura Podre");
        }

        [Test]
        public void Look_ReflectsTequilaPhase()
        {
            var w = Fresh();
            var p = w.Players[0];
            AbilitySystem.Grant(w, 0, w.Db.AbilityIndex("tequila"));
            var a = p.Abilities.First(x => w.Db.Ability(x.AbilityIndex).Def.id == "tequila");
            Step(w, 0.1f);
            ((WeaponState)a.State).Counters[0] = 1;
            Assert.That(EventSystem.Look(w, p) & PlayerLook.Euphoria, Is.Not.Zero);
            ((WeaponState)a.State).Counters[0] = 2;
            Assert.That(EventSystem.Look(w, p) & PlayerLook.Hangover, Is.Not.Zero);
            ((WeaponState)a.State).Counters[0] = 0;
            Assert.That(EventSystem.Look(w, p), Is.EqualTo(0), "volta ao visual original");
        }

        [Test]
        public void Codec_RoundTripsEventMessages_AndSnapshotLook()
        {
            var w = Fresh();
            var events = new List<RuleEvent>
            {
                new RuleEvent { Kind = RuleEventKind.EventStarted, A = 3, B = 255, C = 12, X = 30, Y = 3 },
                new RuleEvent { Kind = RuleEventKind.EventProgress, A = 12, B = 17, C = 40 },
                new RuleEvent { Kind = RuleEventKind.EventEnded, A = 12, B = EventOutcome.Success, C = 3, X = -1 },
                new RuleEvent { Kind = RuleEventKind.EventBonus, A = 0, B = EventOutcome.BonusReflex, X = 5, Y = 6, Z = 2 },
                new RuleEvent { Kind = RuleEventKind.EventMoto, A = 13, B = 360, C = 1600, X = 10, Y = 11, Z = 1.5f, W = 22 },
            };
            var bw = new ByteWriter(512);
            RuleEventCodec.Write(bw, events, w);
            var outList = new List<DecodedRuleEvent>();
            Assert.That(RuleEventCodec.TryRead(bw.Buffer, bw.Length, w.Arena.width, w.Arena.height, outList), Is.True);
            Assert.That(outList.Select(d => d.Event.Kind), Is.EqualTo(events.Select(e => e.Kind)));
            Assert.That(outList[0].Event.B, Is.EqualTo(255));
            Assert.That(outList[2].Event.X, Is.EqualTo(-1));
            Assert.That(outList[4].Event.Z, Is.EqualTo(1.5f));

            EventSystem.Begin(w, Ev(w, "jalapeno"), 0);
            Step(w, 0.1f);
            var chunks = new ChunkSet();
            SnapshotCodec.Write(w, new List<CosmeticEvent>(), chunks);
            var view = new SnapshotView();
            for (int c = 0; c < chunks.Count; c++) Assert.That(SnapshotCodec.TryRead(chunks.Buffers[c], chunks.Lengths[c], w.Arena.width, w.Arena.height, view), Is.True);
            Assert.That(view.Players[0].Look & PlayerLook.Jalapeno, Is.Not.Zero);
        }

        [Test]
        public void FullMatchWithBotsAndEvents_StaysStableAndFast()
        {
            var roster = Enumerable.Range(0, 4).Select(k => new PlayerSlot { Name = "Bot" + k, SpecializationId = TestWorlds.Db.Specialization(k).Def.id }).ToArray();
            var w = new World(TestWorlds.Db, new MatchConfig { Seed = 7, Difficulty = ES.Sim.Difficulty.Normal, TeamSize = 4, ArenaId = "laboratorio", Roster = roster, Coop = true });
            var inputs = new PlayerInput[4];
            ushort seq = 0;
            int started = 0;
            double worst = 0;
            var sw = new System.Diagnostics.Stopwatch();
            while (w.Phase == MatchPhase.Running && w.Elapsed < 720)
            {
                seq++;
                for (int s = 0; s < 4; s++) inputs[s] = BotBrain.Decide(w, s, seq);
                sw.Restart();
                w.Step(inputs);
                worst = Math.Max(worst, sw.Elapsed.TotalMilliseconds);
                started += w.Events.Rule.Count(e => e.Kind == RuleEventKind.EventStarted);
                Assert.That(w.Enemies.Count, Is.LessThanOrEqualTo(w.Enemies.Capacity));
                Assert.That(w.EventDir.Buffs.Count, Is.LessThan(64));
            }
            Assert.That(started, Is.GreaterThanOrEqualTo(4));
            Assert.That(worst, Is.LessThan(40), "pior tick (ms)");
        }
    }
}
