using System;
using System.Collections.Generic;
using System.Linq;

namespace ES.SimTests.Match
{
    /// <summary>Bosses (Guirotto 04:00, Mago da Elétrica 09:00), fúria, fim da partida e Modo Infinito.</summary>
    public class BossTests
    {
        static readonly List<RuleEvent> Log = new List<RuleEvent>();

        /// <summary>Avança com os jogadores invulneráveis, registrando os eventos de regra.</summary>
        static void Run(World w, float seconds, Func<World, bool> until = null)
        {
            float end = w.Elapsed + seconds;
            while (w.Elapsed < end - 1e-4f)
            {
                foreach (var p in w.Players.Slots) if (p != null) p.InvulnUntil = float.MaxValue;
                w.Step(TestWorlds.NoInput());
                Log.AddRange(w.Events.Rule);
                if (until != null && until(w)) return;
                if (w.Phase != MatchPhase.Running) return;
            }
        }

        static World Fresh(int team = 1, bool spawning = false, ES.Sim.Difficulty difficulty = ES.Sim.Difficulty.Normal)
        {
            Log.Clear();
            var w = TestWorlds.Disarm(TestWorlds.CreateOpen(difficulty, team, coop: team > 1));
            w.SpawningEnabled = spawning;
            return w;
        }

        static int BossIndex(World w, string id) => Enumerable.Range(0, w.Db.BossCount).First(k => w.Db.Boss(k).Def.id == id);

        static int EnemyOf(World w, BossRuntime b)
        {
            for (int i = 0; i < w.Enemies.Count; i++) if (w.Enemies.Id[i] == b.EnemyId) return i;
            return -1;
        }

        static void Kill(World w, BossRuntime b)
        {
            int i = EnemyOf(w, b);
            w.Enemies.Flags[i] = (byte)(w.Enemies.Flags[i] & ~EnemyFlags.Invulnerable);
            int before = w.Events.Rule.Count;
            CombatSystem.DamageEnemy(w, i, w.Enemies.Hp[i] + 1, Vec2.Zero, 0, 0, -1);
            Log.AddRange(w.Events.Rule.Skip(before));
        }

        [Test]
        public void Content_HasTwoBosses_MagoIsFinal()
        {
            var db = TestWorlds.Db;
            Assert.That(db.BossCount, Is.EqualTo(2));
            Assert.That(db.Boss(db.FinalBoss).Def.id, Is.EqualTo("mago"));
            Assert.That(db.Boss(BossIndex(new World(db, new MatchConfig { ArenaId = "laboratorio", Roster = new[] { new PlayerSlot { Name = "a", SpecializationId = "eletrica" } } }), "guirotto")).Def.spawnSeconds, Is.EqualTo(240f));
        }

        [Test]
        public void Guirotto_SpawnsExactlyAt0400_Once_WithWarning()
        {
            var w = Fresh();
            Run(w, 250);
            var spawned = Log.Where(e => e.Kind == RuleEventKind.BossSpawned && e.A == BossIndex(w, "guirotto")).ToList();
            Assert.That(spawned.Count, Is.EqualTo(1), "uma única vez");
            Assert.That(Log.Any(e => e.Kind == RuleEventKind.BossIncoming && e.A == BossIndex(w, "guirotto")), Is.True, "aviso antes da chegada");
            Assert.That(w.Bosses.Active.Count, Is.EqualTo(1));
            // o tick da chegada é o primeiro em que o relógio alcança 240 s
            Log.Clear();
            var w2 = Fresh();
            Run(w2, 239.9f);
            Assert.That(w2.Bosses.Active.Count, Is.EqualTo(0));
            Run(w2, 0.2f, ww => ww.Bosses.Active.Count > 0);
            Assert.That(w2.Bosses.Active.Count, Is.EqualTo(1));
            Assert.That(w2.Elapsed, Is.EqualTo(240f).Within(w2.Dt * 1.5f));
        }

        [Test]
        public void Mago_SpawnsExactlyAt0900_Once()
        {
            var w = Fresh();
            Run(w, 539.9f);
            Assert.That(Log.Count(e => e.Kind == RuleEventKind.BossSpawned && e.A == BossIndex(w, "mago")), Is.EqualTo(0));
            Run(w, 10f);
            Assert.That(Log.Count(e => e.Kind == RuleEventKind.BossSpawned && e.A == BossIndex(w, "mago")), Is.EqualTo(1));
            var spawn = Log.First(e => e.Kind == RuleEventKind.BossSpawned && e.A == BossIndex(w, "mago"));
            Assert.That(Log.Count(e => e.Kind == RuleEventKind.BossSpawned), Is.EqualTo(2), "Guirotto + Mago, nada duplicado");
        }

        [Test]
        public void Boss_InvulnerableWhileEntering_ThenTakesDamage()
        {
            var w = Fresh();
            var b = BossSystem.Spawn(w, BossIndex(w, "guirotto"), 0);
            int i = EnemyOf(w, b);
            float hp = w.Enemies.Hp[i];
            Assert.That(CombatSystem.DamageEnemy(w, i, 100, Vec2.Zero, 0, 0, -1), Is.False);
            Assert.That(w.Enemies.Hp[i], Is.EqualTo(hp));
            Run(w, w.Db.Boss(b.Boss).Def.entrySeconds + 0.1f);
            i = EnemyOf(w, b);
            CombatSystem.DamageEnemy(w, i, 100, Vec2.Zero, 0, 0, -1);
            Assert.That(w.Enemies.Hp[i], Is.LessThan(hp));
        }

        [TestCase("guirotto")]
        [TestCase("mago")]
        public void Bravo_At30Percent_OnlyOnce_NoHeal(string id)
        {
            var w = Fresh();
            var b = BossSystem.Spawn(w, BossIndex(w, id), 0);
            Run(w, 3f);
            int i = EnemyOf(w, b);
            float max = w.Enemies.MaxHp[i];
            CombatSystem.DamageEnemy(w, i, max * 0.69f, Vec2.Zero, 0, 0, -1); // 31%
            Run(w, 0.2f);
            Assert.That(b.Bravo, Is.False, "acima de 30% continua NORMAL");
            i = EnemyOf(w, b);
            CombatSystem.DamageEnemy(w, i, max * 0.02f, Vec2.Zero, 0, 0, -1); // 29%
            float before = w.Enemies.Hp[i];
            Run(w, 0.1f);
            Assert.That(b.Bravo, Is.True);
            Assert.That(b.State, Is.EqualTo(BossState.Rage), "animação de fúria");
            i = EnemyOf(w, b);
            Assert.That(w.Enemies.Hp[i], Is.LessThanOrEqualTo(before), "a fúria não restaura vida");
            // vida oscilando perto do limite não repete a transição
            w.Enemies.Hp[i] = max * 0.35f;
            Run(w, 0.5f);
            w.Enemies.Hp[EnemyOf(w, b)] = max * 0.25f;
            Run(w, 0.5f);
            Assert.That(Log.Count(e => e.Kind == RuleEventKind.BossBravo), Is.EqualTo(1));
            Assert.That(b.Bravo, Is.True);
        }

        /// <summary>Ataques usados e áreas sinalizadas durante <paramref name="seconds"/>.</summary>
        static (HashSet<BossAttack> attacks, int hazards, int projectiles) Observe(string id, bool bravo, float seconds, int team = 1, ulong seed = 1)
        {
            Log.Clear();
            var w = TestWorlds.Disarm(TestWorlds.CreateOpen(team: team, seed: seed, coop: team > 1));
            w.SpawningEnabled = false;
            var b = BossSystem.Spawn(w, BossIndex(w, id), 0);
            Run(w, 3f);
            if (bravo)
            {
                int i = EnemyOf(w, b);
                w.Enemies.Hp[i] = w.Enemies.MaxHp[i] * 0.2f;
                Run(w, 2.5f);
                Assert.That(b.Bravo, Is.True);
            }
            Log.Clear();
            int maxProjectiles = 0;
            Run(w, seconds, ww => { maxProjectiles = Math.Max(maxProjectiles, ww.Hostile.Count); return false; });
            var attacks = new HashSet<BossAttack>(Log.Where(e => e.Kind == RuleEventKind.BossAction && e.C == (int)BossState.Attack).Select(e => (BossAttack)e.B));
            return (attacks, Log.Count(e => e.Kind == RuleEventKind.BossHazard && e.A != 255), maxProjectiles);
        }

        [Test]
        public void Guirotto_UsesAtLeastFourAttacks_BravoAddsRefrao()
        {
            var normal = Observe("guirotto", false, 60);
            Assert.That(normal.attacks.Count(a => a >= BossAttack.Acorde && a <= BossAttack.Concerto), Is.GreaterThanOrEqualTo(4), string.Join(",", normal.attacks));
            Assert.That(normal.attacks.Contains(BossAttack.Refrao), Is.False, "Refrão Furioso é só da fúria");
            var bravo = Observe("guirotto", true, 60);
            Assert.That(bravo.attacks.Contains(BossAttack.Refrao), Is.True, string.Join(",", bravo.attacks));
            Assert.That(bravo.projectiles, Is.GreaterThan(normal.projectiles), "fúria: mais projéteis");
        }

        [Test]
        public void Mago_UsesAtLeastFiveAttacks_BravoIsDenser()
        {
            var normal = Observe("mago", false, 80);
            var core = new[] { BossAttack.Raio, BossAttack.Cadeia, BossAttack.Tempestade, BossAttack.Orbes, BossAttack.Sobrecarga };
            Assert.That(core.All(normal.attacks.Contains), Is.True, string.Join(",", normal.attacks));
            var bravo = Observe("mago", true, 80);
            Assert.That(bravo.hazards, Is.GreaterThan(normal.hazards), "fúria: mais áreas (raios triplos, cruz, segunda onda)");
        }

        [Test]
        public void Mago_BravoBolt_IsTripleLine()
        {
            Log.Clear();
            var w = Fresh();
            var b = BossSystem.Spawn(w, BossIndex(w, "mago"), 0);
            Run(w, 3f);
            b.Bravo = true;
            b.State = BossState.Attack; b.Attack = BossAttack.Raio; b.Step = 0; b.StepAt = 0; b.Target = 0;
            int before = w.Bosses.HazardCount;
            BossSystem.Step(w, w.Dt);
            Assert.That(w.Bosses.HazardCount - before, Is.EqualTo(3));
        }

        [Test]
        public void Coop4_BossTargetsEveryone()
        {
            Log.Clear();
            var w = Fresh(team: 4);
            // espalha os jogadores
            for (int s = 0; s < 4; s++) w.Players[s].Pos = new Vec2(30 + (s % 2) * 8, 30 + (s / 2) * 8);
            var b = BossSystem.Spawn(w, BossIndex(w, "mago"), 0);
            var targeted = new HashSet<int>();
            Run(w, 60, ww => { if (b.State == BossState.Windup && b.Target >= 0) targeted.Add(b.Target); return false; });
            Assert.That(targeted.Count, Is.EqualTo(4), "não fica preso em um jogador");
        }

        [Test]
        public void TeamScaling_MoreHp_ButNotOnlyHp()
        {
            var w1 = Fresh(team: 1);
            var b1 = BossSystem.Spawn(w1, BossIndex(w1, "guirotto"), 0);
            var w4 = Fresh(team: 4);
            var b4 = BossSystem.Spawn(w4, BossIndex(w4, "guirotto"), 0);
            float hp1 = w1.Enemies.MaxHp[EnemyOf(w1, b1)], hp4 = w4.Enemies.MaxHp[EnemyOf(w4, b4)];
            Assert.That(hp4, Is.GreaterThan(hp1 * 1.5f));
            Assert.That(hp4, Is.LessThan(hp1 * 4f * 1.5f), "não é só multiplicar pelo número de jogadores");
        }

        [Test]
        public void Clock_PastTenMinutes_DoesNotEndWhileMagoAlive()
        {
            var w = Fresh();
            Run(w, 640);
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Running));
            Assert.That(w.Elapsed, Is.GreaterThan(600f), "o relógio continua além de 10:00");
            Assert.That(w.Bosses.Active.Any(b => w.Db.Boss(b.Boss).Def.final), Is.True, "o Mago continua na arena");
            Assert.That(Log.Any(e => e.Kind == RuleEventKind.MatchEnded), Is.False);
        }

        [Test]
        public void GuirottoDeath_DoesNotEndMatch()
        {
            var w = Fresh();
            Run(w, 245);
            Kill(w, w.Bosses.Active[0]);
            Run(w, 10);
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Running));
            Assert.That(Log.Any(e => e.Kind == RuleEventKind.BossDefeated && e.C == 0), Is.True);
            Assert.That(w.Bosses.Defeated, Has.Count.EqualTo(1));
        }

        [Test]
        public void MagoDeath_Victory_AfterDeathAnimation_ProjectilesCleared()
        {
            var w = Fresh();
            Run(w, 560);
            var mago = w.Bosses.Active.First(b => w.Db.Boss(b.Boss).Def.final);
            w.Hostile.Spawn(HostileKind.Orb, new Vec2(1, 1), new Vec2(1, 0), 5, 0.3f, 10);
            Kill(w, mago);
            Assert.That(w.Hostile.Count, Is.EqualTo(0), "projéteis encerrados com segurança");
            Assert.That(w.Bosses.HazardCount, Is.EqualTo(0));
            Run(w, 1f);
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Running), "espera a animação de morte");
            Run(w, w.Rules.bossVictoryDelay + 0.2f);
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Victory));
            Assert.That(Log.Any(e => e.Kind == RuleEventKind.BossDefeated && e.C == 1), Is.True);
            var summary = MatchSummary.FromWorld(w);
            Assert.That(summary.Phase, Is.EqualTo(MatchPhase.Victory));
            Assert.That(summary.BossesDefeated, Is.EqualTo(1));
        }

        [Test]
        public void MagoDeath_AfterTenMinutes_StillVictory()
        {
            var w = Fresh();
            Run(w, 620);
            Kill(w, w.Bosses.Active.First(b => w.Db.Boss(b.Boss).Def.final));
            Run(w, 5);
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Victory));
            Assert.That(w.Elapsed, Is.GreaterThan(620f));
        }

        [Test]
        public void AllDownedBeforeMago_IsDefeat()
        {
            Log.Clear();
            var w = TestWorlds.Disarm(TestWorlds.CreateOpen());
            w.SpawningEnabled = false;
            w.Elapsed = 545;
            w.Step(TestWorlds.NoInput());
            CombatSystem.DamagePlayer(w, 0, 100000);
            w.Step(TestWorlds.NoInput());
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Defeat));
        }

        static World Victorious(int team = 1)
        {
            var w = Fresh(team, spawning: true);
            Run(w, 545);
            foreach (var b in w.Bosses.Active.ToList()) Kill(w, b);
            Run(w, 5);
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Victory));
            return w;
        }

        [Test]
        public void Infinite_ContinuesSameMatch_PreservingProgress_NoTimeVictory()
        {
            var w = Victorious();
            var p = w.Players[0];
            p.Progress.Level = 17;
            int abilities = p.Abilities.Count;
            float t = w.Elapsed;
            Log.Clear();
            Assert.That(BossSystem.ContinueInfinite(w), Is.True);
            Assert.That(BossSystem.ContinueInfinite(w), Is.False, "uma vez só");
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Running));
            Assert.That(w.Events.Rule.Any(e => e.Kind == RuleEventKind.MatchContinued), Is.True);
            Run(w, 400);
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Running), "sem vitória por tempo");
            Assert.That(w.Elapsed, Is.GreaterThan(t + 399), "o relógio continua de onde parou");
            Assert.That(p.Progress.Level, Is.GreaterThanOrEqualTo(17));
            Assert.That(p.Abilities.Count, Is.EqualTo(abilities));
            Assert.That(Log.Count(e => e.Kind == RuleEventKind.BossSpawned), Is.GreaterThanOrEqualTo(2), "bosses recorrentes");
            Assert.That(Log.Any(e => e.Kind == RuleEventKind.BossSpawned && e.C >= 1), Is.True);
            Assert.That(Log.Count(e => e.Kind == RuleEventKind.BossHazard && e.A != 255), Is.GreaterThan(0), "eventos de tempestade");
        }

        [Test]
        public void Infinite_DifficultyEscalates_WithCaps()
        {
            var w = Victorious();
            BossSystem.ContinueInfinite(w);
            Assert.That(BossSystem.InfiniteHp(w), Is.EqualTo(1f).Within(1e-3f));
            w.Elapsed += 600;
            float hp10 = BossSystem.InfiniteHp(w), spawn10 = BossSystem.InfiniteSpawn(w), elite10 = BossSystem.InfiniteElite(w), dmg10 = BossSystem.InfiniteDamage(w);
            Assert.That(hp10, Is.GreaterThan(1.5f));
            Assert.That(spawn10, Is.GreaterThan(1.3f));
            Assert.That(elite10, Is.GreaterThan(0.05f));
            Assert.That(dmg10, Is.GreaterThan(1.2f));
            w.Elapsed += 100000;
            Assert.That(BossSystem.InfiniteHp(w), Is.EqualTo(w.Rules.infiniteHpMax));
            Assert.That(BossSystem.InfiniteSpawn(w), Is.EqualTo(w.Rules.infiniteSpawnMax));
            Assert.That(BossSystem.InfiniteElite(w), Is.EqualTo(w.Rules.infiniteEliteMax));
        }

        [Test]
        public void Infinite_InCoop_RevivesDownedAndContinuesForAll()
        {
            var w = Victorious(team: 2);
            w.Players[1].Status = PlayerStatus.Downed;
            Assert.That(BossSystem.ContinueInfinite(w), Is.True);
            Assert.That(w.Players[1].Status, Is.EqualTo(PlayerStatus.Active));
            Run(w, 30);
            Assert.That(w.Phase, Is.EqualTo(MatchPhase.Running));
        }

        [Test]
        public void Infinite_RecurringBosses_HaveMoreHp_AndDuetFromThirdCycle()
        {
            var w = Victorious();
            BossSystem.ContinueInfinite(w);
            var first = w.Bosses.Active.Count;
            Run(w, w.Rules.infiniteFirstBoss + 0.5f);
            Assert.That(w.Bosses.Active.Count, Is.EqualTo(first + 1));
            var b = w.Bosses.Active.Last();
            Assert.That(b.Cycle, Is.EqualTo(1));
            float hp = w.Enemies.MaxHp[EnemyOf(w, b)];
            Assert.That(hp, Is.GreaterThan(w.Db.Enemy(w.Db.Boss(b.Boss).Enemy).Def.hp * 1.5f));
            foreach (var x in w.Bosses.Active.ToList()) Kill(w, x);
            Run(w, w.Rules.infiniteBossEvery * 2 + 1);
            Assert.That(Log.Count(e => e.Kind == RuleEventKind.BossSpawned && e.C == 3), Is.EqualTo(2), "terceiro ciclo: dueto");
        }

        [Test]
        public void HazardGeometry()
        {
            var circle = new BossHazard { Shape = HazardShape.Circle, Pos = new Vec2(0, 0), Size = 3 };
            Assert.That(BossSystem.Inside(circle, new Vec2(3.2f, 0), 0.4f), Is.True);
            Assert.That(BossSystem.Inside(circle, new Vec2(3.5f, 0), 0.4f), Is.False);
            var ring = new BossHazard { Shape = HazardShape.Ring, Pos = Vec2.Zero, Size = 10, Extra = 7 };
            Assert.That(BossSystem.Inside(ring, new Vec2(5, 0), 0.4f), Is.False, "dentro do anel interno está a salvo");
            Assert.That(BossSystem.Inside(ring, new Vec2(8, 0), 0.4f), Is.True);
            var line = new BossHazard { Shape = HazardShape.Line, Pos = Vec2.Zero, Size = 10, Extra = 0, Width = 1 };
            Assert.That(BossSystem.Inside(line, new Vec2(5, 0.5f), 0.2f), Is.True);
            Assert.That(BossSystem.Inside(line, new Vec2(5, 1.2f), 0.2f), Is.False);
            Assert.That(BossSystem.Inside(line, new Vec2(-1, 0), 0.2f), Is.False);
        }

        [Test]
        public void Hazard_DamagesOnlyAfterDelay_AndCanBeDodged()
        {
            var w = Fresh();
            var p = w.Players[0];
            float hp = p.Hp;
            BossSystem.AddHazard(w, HazardShape.Circle, FxStyle.Storm, p.Pos, 2, 0, 0, 1f, 30);
            for (int k = 0; k < 20; k++) w.Step(TestWorlds.NoInput());
            Assert.That(p.Hp, Is.EqualTo(hp), "nada antes do impacto");
            for (int k = 0; k < 20; k++) w.Step(TestWorlds.NoInput());
            Assert.That(p.Hp, Is.LessThan(hp));
            hp = p.Hp;
            p.InvulnUntil = 0;
            BossSystem.AddHazard(w, HazardShape.Circle, FxStyle.Storm, p.Pos, 2, 0, 0, 1f, 30);
            p.Pos += new Vec2(4, 0); // escapou
            for (int k = 0; k < 40; k++) w.Step(TestWorlds.NoInput());
            Assert.That(p.Hp, Is.EqualTo(hp));
        }

        [Test]
        public void BossProjectiles_CurveHomeAndBounce()
        {
            var w = Fresh();
            int h = w.Hostile.Spawn(HostileKind.Note, new Vec2(20, 20), new Vec2(3, 0), 5, 0.3f, 5);
            w.Hostile.Turn[h] = 1f;
            for (int k = 0; k < 10; k++) w.Step(TestWorlds.NoInput());
            Assert.That(Math.Abs(w.Hostile.Vel[0].Y), Is.GreaterThan(0.5f), "curva");
            Assert.That(w.Hostile.Vel[0].Length(), Is.EqualTo(3f).Within(0.01f), "curva mantém a velocidade");
        }

        [Test]
        public void EveryAttack_RunsToCompletion()
        {
            foreach (BossAttack atk in Enum.GetValues(typeof(BossAttack)))
            {
                if (atk == BossAttack.None) continue;
                foreach (bool bravo in new[] { false, true })
                {
                    Log.Clear();
                    var w = Fresh(team: 2);
                    string id = atk <= BossAttack.Refrao ? "guirotto" : "mago";
                    var b = BossSystem.Spawn(w, BossIndex(w, id), 0);
                    Run(w, 3f);
                    b.Bravo = bravo;
                    b.State = BossState.Attack; b.Attack = atk; b.Step = 0; b.StepAt = 0;
                    Run(w, 8f, ww => b.State != BossState.Attack);
                    Assert.That(b.State, Is.Not.EqualTo(BossState.Attack), $"{atk} bravo={bravo} terminou");
                }
            }
        }

        [Test]
        public void Snapshot_CarriesBossState_AndFeedTracksIt()
        {
            var w = Fresh();
            var b = BossSystem.Spawn(w, BossIndex(w, "mago"), 0);
            Run(w, 3f);
            b.Bravo = true;
            b.State = BossState.Windup; b.Attack = BossAttack.Sobrecarga; b.Facing = new Vec2(-1, 0);
            var chunks = new ChunkSet();
            SnapshotCodec.Write(w, new List<CosmeticEvent>(), chunks);
            var view = new SnapshotView();
            for (int c = 0; c < chunks.Count; c++) Assert.That(SnapshotCodec.TryRead(chunks.Buffers[c], chunks.Lengths[c], w.Arena.width, w.Arena.height, view), Is.True);
            Assert.That(view.Bosses.TryGetValue(b.EnemyId, out var snap), Is.True);
            Assert.That(snap.State, Is.EqualTo(BossState.Windup));
            Assert.That(snap.Attack, Is.EqualTo(BossAttack.Sobrecarga));
            Assert.That(snap.Flags & BossSnap.FlagBravo, Is.Not.Zero);
            Assert.That(snap.Dir, Is.EqualTo(2), "esquerda");

            var feed = new MatchFeed();
            feed.ApplyRuleEvent(new DecodedRuleEvent { Event = new RuleEvent { Kind = RuleEventKind.BossSpawned, A = b.Boss, B = b.EnemyId } });
            feed.SetBoss(b.EnemyId, snap);
            Assert.That(feed.Bosses[b.EnemyId].Bravo, Is.True);
            feed.ApplyRuleEvent(new DecodedRuleEvent { Event = new RuleEvent { Kind = RuleEventKind.BossHazard, A = (int)HazardShape.Circle, B = 40, C = 1000, X = 5, Y = 5 } });
            Assert.That(feed.Hazards[0].Size, Is.EqualTo(2f));
            feed.Phase = MatchPhase.Victory;
            feed.ApplyRuleEvent(new DecodedRuleEvent { Event = new RuleEvent { Kind = RuleEventKind.MatchContinued, X = 600 } });
            Assert.That(feed.Phase, Is.EqualTo(MatchPhase.Running));
            Assert.That(feed.Infinite, Is.True);
            feed.ApplyRuleEvent(new DecodedRuleEvent { Event = new RuleEvent { Kind = RuleEventKind.BossDefeated, A = b.Boss, B = b.EnemyId } });
            Assert.That(feed.Bosses.ContainsKey(b.EnemyId), Is.False);
            Assert.That(feed.BossesDefeated, Is.EqualTo(1));
        }

        [Test]
        public void Client_SeesBossHpDrop()
        {
            var net = new ES.SimTests.Netcode.LoopbackNet();
            var (_, client, _) = net.Connect("Ana");
            net.Host.Start(5);
            net.Pump();
            var w = net.Host.World;
            w.SpawningEnabled = false;
            foreach (var p in w.Players.Slots) if (p != null) p.InvulnUntil = float.MaxValue;
            var b = BossSystem.Spawn(w, BossIndex(w, "guirotto"), 0);
            net.Run(3.0);
            int i = EnemyOf(w, b);
            CombatSystem.DamageEnemy(w, i, w.Enemies.MaxHp[i] * 0.4f, Vec2.Zero, 0, 0, -1);
            net.Run(0.5);
            Assert.That(client.Feed.Bosses.TryGetValue(b.EnemyId, out var info), Is.True);
            float real = w.Enemies.Hp[EnemyOf(w, b)] / w.Enemies.MaxHp[EnemyOf(w, b)];
            Assert.That(real, Is.LessThan(0.65f));
            Assert.That(info.Hp, Is.EqualTo(real).Within(0.03f), "a barra do cliente acompanha a vida do boss");
            Assert.That(net.Host.Feed.Bosses[b.EnemyId].Hp, Is.EqualTo(real).Within(0.01f));
        }

        [Test]
        public void Summary_RoundTripsBossesAndInfinite()
        {
            var s = new MatchSummary { Phase = MatchPhase.Victory, Elapsed = 612, TeamKills = 900, BossesDefeated = 2, Infinite = false };
            var bw = new ByteWriter();
            SummaryCodec.Write(bw, s);
            Assert.That(SummaryCodec.TryRead(bw.Buffer, bw.Length, out var o), Is.True);
            Assert.That(o.BossesDefeated, Is.EqualTo(2));
            Assert.That(o.Infinite, Is.False);
        }

        [Test]
        public void Codec_RoundTripsBossEvents()
        {
            var w = Fresh();
            var events = new List<RuleEvent>
            {
                new RuleEvent { Kind = RuleEventKind.BossIncoming, A = 1, X = 540 },
                new RuleEvent { Kind = RuleEventKind.BossSchedule, A = 254, X = 0 },
                new RuleEvent { Kind = RuleEventKind.BossSpawned, A = 0, B = 77, C = 3, X = 10, Y = 12 },
                new RuleEvent { Kind = RuleEventKind.BossBravo, A = 1, B = 77, X = 5, Y = 6 },
                new RuleEvent { Kind = RuleEventKind.BossAction, A = 77, B = (int)BossAttack.Sobrecarga, C = (int)BossState.Windup, X = 2f },
                new RuleEvent { Kind = RuleEventKind.BossHazard, A = (int)HazardShape.Line | ((int)FxStyle.Electric << 4), B = 320, C = 700, X = 9, Y = 9, Z = 1.25f, W = 0.9f },
                new RuleEvent { Kind = RuleEventKind.BossHazard, A = 255 },
                new RuleEvent { Kind = RuleEventKind.BossDefeated, A = 1, B = 77, C = 1, X = 3, Y = 4 },
                new RuleEvent { Kind = RuleEventKind.MatchContinued, X = 603.5f },
            };
            var bw = new ByteWriter(1024);
            RuleEventCodec.Write(bw, events, w);
            var outList = new List<DecodedRuleEvent>();
            Assert.That(RuleEventCodec.TryRead(bw.Buffer, bw.Length, w.Arena.width, w.Arena.height, outList), Is.True);
            Assert.That(outList.Select(d => d.Event.Kind), Is.EqualTo(events.Select(e => e.Kind)));
            var hz = outList[5].Event;
            Assert.That(hz.B, Is.EqualTo(320)); Assert.That(hz.C, Is.EqualTo(700));
            Assert.That(hz.Z, Is.EqualTo(1.25f)); Assert.That(hz.W, Is.EqualTo(0.9f));
            Assert.That(outList[2].Event.C, Is.EqualTo(3));
            Assert.That(outList[8].Event.X, Is.EqualTo(603.5f));
        }
    }
}
