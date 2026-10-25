using System;
using System.Collections.Generic;
using System.Linq;

namespace ES.SimTests.Codec
{
    public class CodecTests
    {
        static World WithEnemies(int count, bool elite = false, int team = 1)
        {
            var w = TestWorlds.CreateOpen(team: team, coop: true);
            w.SpawningEnabled = false;
            var rng = new Rng(3);
            for (int i = 0; i < count; i++)
                SpawnDirector.SpawnEnemy(w, i % 3, new Vec2(rng.Range(1f, 63f), rng.Range(1f, 63f)), elite);
            return w;
        }

        static SnapshotView Decode(World w, ChunkSet set, int skipChunk = -1)
        {
            var view = new SnapshotView();
            for (int c = 0; c < set.Count; c++)
            {
                if (c == skipChunk) continue;
                Assert.That(SnapshotCodec.TryRead(set.Buffers[c], set.Lengths[c], w.Arena.width, w.Arena.height, view), Is.True, $"bloco {c}");
            }
            return view;
        }

        [Test]
        public void Input_RoundTrip()
        {
            var bw = new ByteWriter(16);
            var input = new PlayerInput { Seq = 65000, MoveX = -127, MoveY = 64, SpecialCounter = 200, ChoiceOfferId = -3, ChoiceIndex = 2 };
            InputCodec.Write(bw, input);
            Assert.That(bw.Length, Is.EqualTo(8));
            var br = new ByteReader(bw.Buffer, bw.Length);
            Assert.That(InputCodec.TryRead(ref br, out var back), Is.True);
            Assert.That(back, Is.EqualTo(input));
        }

        [Test]
        public void Input_TruncatedData_ReturnsFalse()
        {
            var bw = new ByteWriter(16);
            InputCodec.Write(bw, PlayerInput.None);
            var br = new ByteReader(bw.Buffer, 5);
            Assert.That(InputCodec.TryRead(ref br, out _), Is.False);
        }

        [Test]
        public void Snapshot_RoundTrip_PositionsWithin1Over1024()
        {
            var w = WithEnemies(50, team: 2);
            w.Players[1].Hp = 37.5f;
            var set = new ChunkSet();
            SnapshotCodec.Write(w, new List<CosmeticEvent>(), set);
            var view = Decode(w, set);
            Assert.That(view.Tick, Is.EqualTo(w.Tick));
            Assert.That(view.Enemies.Count, Is.EqualTo(50));
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var s = view.Enemies[w.Enemies.Id[i]];
                Assert.That(Math.Abs(s.Pos.X - w.Enemies.Pos[i].X), Is.LessThanOrEqualTo(1f / 1024));
                Assert.That(Math.Abs(s.Pos.Y - w.Enemies.Pos[i].Y), Is.LessThanOrEqualTo(1f / 1024));
                Assert.That(s.Type, Is.EqualTo(w.Enemies.Type[i]));
            }
            for (int slot = 0; slot < 2; slot++)
            {
                var p = view.Players[slot];
                Assert.That(p.Present, Is.True);
                Assert.That((p.Pos - w.Players[slot].Pos).Length(), Is.LessThan(2f / 1024));
                Assert.That(p.Hp, Is.EqualTo(w.Players[slot].Hp).Within(0.1f));
            }
            Assert.That(view.Players[2].Present, Is.False);
        }

        [Test]
        public void Snapshot_300Enemies_ChunksUnder1100Bytes_AndAllPresent()
        {
            var w = WithEnemies(300, team: 4);
            for (int i = 0; i < 120; i++) w.Hostile.Spawn(0, new Vec2(10 + i * 0.1f, 20), new Vec2(1, 0), 8, 0.2f, 4);
            var cosm = Enumerable.Range(0, 48).Select(i => new CosmeticEvent { Kind = CosmeticKind.Damage, A = i, X = 3, Y = 4, Value = 12 }).ToList();
            var set = new ChunkSet();
            SnapshotCodec.Write(w, cosm, set);
            Assert.That(set.Count, Is.GreaterThan(1));
            for (int c = 0; c < set.Count; c++) Assert.That(set.Lengths[c], Is.LessThanOrEqualTo(1100));
            var view = Decode(w, set);
            Assert.That(view.Enemies.Count, Is.EqualTo(300));
            Assert.That(view.Hostile.Count, Is.EqualTo(120));
            Assert.That(view.Cosmetic.Count, Is.EqualTo(48));
            Assert.That(Enumerable.Range(0, 4).All(s => view.Players[s].Present), Is.True);
        }

        [Test]
        public void Snapshot_EnemyRecordIs7Bytes()
        {
            int Size(int n)
            {
                var set = new ChunkSet();
                SnapshotCodec.Write(WithEnemies(n), new List<CosmeticEvent>(), set);
                return Enumerable.Range(0, set.Count).Sum(c => set.Lengths[c]);
            }
            Assert.That(Size(101) - Size(100), Is.EqualTo(7));
        }

        [Test]
        public void Snapshot_ChunkAlone_DecodesItsEnemies()
        {
            var w = WithEnemies(300);
            var set = new ChunkSet();
            SnapshotCodec.Write(w, new List<CosmeticEvent>(), set);
            var full = Decode(w, set);
            var partial = Decode(w, set, skipChunk: 0);
            Assert.That(partial.Enemies.Count, Is.GreaterThan(0));
            Assert.That(partial.Enemies.Count, Is.LessThan(300));
            foreach (var kv in partial.Enemies) Assert.That(kv.Value.Pos, Is.EqualTo(full.Enemies[kv.Key].Pos));
        }

        [Test]
        public void Snapshot_GarbageBytes_NoThrow_MostlyRejected()
        {
            var rng = new Rng(99);
            int accepted = 0;
            for (int k = 0; k < 10000; k++)
            {
                var data = new byte[rng.Range(0, 200)];
                for (int i = 0; i < data.Length; i++) data[i] = (byte)rng.NextUInt();
                var view = new SnapshotView();
                bool ok = false;
                Assert.DoesNotThrow(() => ok = SnapshotCodec.TryRead(data, data.Length, 64, 64, view));
                if (ok) accepted++;
            }
            Assert.That(accepted, Is.LessThan(100));
        }

        [Test]
        public void RuleEvents_RoundTrip_IncludingOfferOptions()
        {
            var w = TestWorlds.CreateOpen(coop: true);
            w.SpawningEnabled = false;
            ProgressionSystem.AddSharedXp(w, 5);
            int e = SpawnDirector.SpawnEnemy(w, 0, new Vec2(10, 12), false);
            CombatSystem.DamageEnemy(w, e, 999, Vec2.Zero, 0, 0, -1);
            var events = w.Events.Rule.ToList();

            var bw = new ByteWriter(64);
            RuleEventCodec.Write(bw, events, w);
            var decoded = new List<DecodedRuleEvent>();
            Assert.That(RuleEventCodec.TryRead(bw.Buffer, bw.Length, w.Arena.width, w.Arena.height, decoded), Is.True);
            Assert.That(decoded.Select(d => d.Event.Kind), Is.EqualTo(events.Select(x => x.Kind)));

            var offer = decoded.Single(d => d.Event.Kind == RuleEventKind.LevelUpOffer);
            Assert.That(offer.Event.B, Is.EqualTo(w.Players[0].Progress.Current.OfferId));
            Assert.That(offer.Options, Is.EqualTo(w.Players[0].Progress.Current.Options));

            var died = decoded.Single(d => d.Event.Kind == RuleEventKind.EnemyDied);
            var orig = events.Single(x => x.Kind == RuleEventKind.EnemyDied);
            Assert.That(died.Event.A, Is.EqualTo(orig.A));
            Assert.That(died.Event.X, Is.EqualTo(orig.X).Within(1f / 1024));
        }

        [Test]
        public void RuleEvents_Garbage_NoThrow()
        {
            var rng = new Rng(5);
            for (int k = 0; k < 5000; k++)
            {
                var data = new byte[rng.Range(0, 120)];
                for (int i = 0; i < data.Length; i++) data[i] = (byte)rng.NextUInt();
                Assert.DoesNotThrow(() => RuleEventCodec.TryRead(data, data.Length, 64, 64, new List<DecodedRuleEvent>()));
            }
        }

        [Test]
        public void Hello_SanitizesName()
        {
            Assert.That(HelloCodec.SanitizeName("\u0007Ana\nBeatriz Souza da Silva"), Is.EqualTo("AnaBeatriz Souza"));
            Assert.That(HelloCodec.SanitizeName("   "), Is.EqualTo("Engenheiro(a)"));
            Assert.That(HelloCodec.SanitizeName(null), Is.EqualTo("Engenheiro(a)"));
            // a interface (UI Toolkit) interpreta rich text: nomes não podem trazer tags
            Assert.That(HelloCodec.SanitizeName("<size=300>Ana</size>"), Is.EqualTo("size=300Ana/size"));
            Assert.That(HelloCodec.SanitizeName("<>"), Is.EqualTo("Engenheiro(a)"));
        }

        [Test]
        public void Hello_RoundTrip()
        {
            var bw = new ByteWriter(64);
            HelloCodec.Write(bw, new Hello { BuildVersion = "0.1.0", ContentHash = 0xDEADBEEFCAFEUL, Name = "Ana", SpecializationId = "eletrica" });
            Assert.That(HelloCodec.TryRead(bw.Buffer, bw.Length, out var h), Is.True);
            Assert.That(h.BuildVersion, Is.EqualTo("0.1.0"));
            Assert.That(h.ContentHash, Is.EqualTo(0xDEADBEEFCAFEUL));
            Assert.That(h.Name, Is.EqualTo("Ana"));
            Assert.That(h.SpecializationId, Is.EqualTo("eletrica"));
        }

        [Test]
        public void MatchStart_RoundTrip()
        {
            var cfg = new MatchConfig
            {
                Seed = 123456789UL, Difficulty = ES.Sim.Difficulty.Hard, TeamSize = 2, ArenaId = "laboratorio", Coop = true,
                Roster = new[] { new PlayerSlot { Name = "Ana", SpecializationId = "eletrica" }, new PlayerSlot { Name = "Bruno", SpecializationId = "mecanica" } },
            };
            var bw = new ByteWriter(64);
            LobbyCodec.WriteMatchStart(bw, cfg, 42UL);
            Assert.That(LobbyCodec.TryReadMatchStart(bw.Buffer, bw.Length, out var back, out ulong hash), Is.True);
            Assert.That(hash, Is.EqualTo(42UL));
            Assert.That(back.Seed, Is.EqualTo(cfg.Seed));
            Assert.That(back.Difficulty, Is.EqualTo(cfg.Difficulty));
            Assert.That(back.TeamSize, Is.EqualTo(2));
            Assert.That(back.Coop, Is.True);
            Assert.That(back.Roster.Select(r => r.Name + "/" + r.SpecializationId), Is.EqualTo(new[] { "Ana/eletrica", "Bruno/mecanica" }));
        }
    }
}
