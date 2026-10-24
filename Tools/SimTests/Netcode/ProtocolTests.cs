using System;
using System.Collections.Generic;
using System.Linq;

namespace ES.SimTests.Netcode
{
    /// <summary>Rede em memória: host + clientes trocando bytes pelos mesmos protocolos do jogo.</summary>
    sealed class LoopbackNet
    {
        public const string Version = "0.1.0";
        public readonly HostProtocol Host;
        public readonly Dictionary<ulong, ClientProtocol> Clients = new();
        readonly Queue<(ulong from, ulong to, MessageType type, byte[] data)> _queue = new();
        ulong _nextPeer = 1;
        public double Now;
        public Func<ulong, PlayerInput> ClientInput = _ => PlayerInput.None;

        sealed class Link : ILink
        {
            readonly LoopbackNet _net; readonly ulong _self;
            public Link(LoopbackNet net, ulong self) { _net = net; _self = self; }
            public void Send(ulong peer, MessageType type, byte[] data, int length, Channel channel)
            {
                var copy = new byte[length];
                Array.Copy(data, copy, length);
                _net._queue.Enqueue((_self, peer, type, copy));
            }
        }

        public LoopbackNet(string hostSpec = "eletrica", bool solo = false)
        {
            Host = new HostProtocol(TestWorlds.Db, Version, new Link(this, Peers.Host),
                new Hello { BuildVersion = Version, ContentHash = TestWorlds.Db.ContentHash, Name = "Host", SpecializationId = hostSpec }, solo);
        }

        public (ulong peer, ClientProtocol client, RejectReason? reason) Connect(string name, string spec = "mecanica", string version = Version, ulong? hash = null)
        {
            ulong peer = _nextPeer++;
            var hello = ClientProtocol.BuildHello(version, hash ?? TestWorlds.Db.ContentHash, name, spec);
            if (!Host.Approve(peer, hello, hello.Length, out var reason)) return (peer, null, reason);
            var client = new ClientProtocol(TestWorlds.Db, Version, new Link(this, peer));
            Clients[peer] = client;
            Host.OnPeerConnected(peer);
            Pump();
            return (peer, client, null);
        }

        public void Disconnect(ulong peer)
        {
            Clients.Remove(peer);
            Host.OnPeerDisconnected(peer);
            Pump();
        }

        /// <summary>Retém snapshots (canal não confiável) para simular chegada atrasada.</summary>
        public bool HoldSnapshots;
        readonly List<(ulong to, byte[] data)> _held = new();

        public void ReleaseHeldSnapshots()
        {
            foreach (var (to, data) in _held) if (Clients.TryGetValue(to, out var c)) c.OnMessage(MessageType.Snapshot, data, data.Length);
            _held.Clear();
        }

        public void Pump()
        {
            while (_queue.Count > 0)
            {
                var (from, to, type, data) = _queue.Dequeue();
                if (HoldSnapshots && type == MessageType.Snapshot) { _held.Add((to, data)); continue; }
                if (to == Peers.Host) Host.OnMessage(from, type, data, data.Length);
                else if (Clients.TryGetValue(to, out var c)) c.OnMessage(type, data, data.Length);
            }
        }

        public void Run(double seconds, double frame = 1.0 / 60.0)
        {
            for (double t = 0; t < seconds; t += frame)
            {
                Now += frame;
                Host.Update(frame);
                Pump();
                foreach (var kv in Clients.ToArray()) kv.Value.Update(frame, Now, ClientInput(kv.Key));
                Pump();
            }
        }
    }

    public class ProtocolTests
    {
        [Test]
        public void JoinCode_NormalizesSpacesAndCase()
        {
            Assert.That(JoinCode.TryNormalize(" f6gplw ", out var code), Is.True);
            Assert.That(code, Is.EqualTo("F6GPLW"));
            Assert.That(JoinCode.TryNormalize("ab cd 12", out var c2), Is.True);
            Assert.That(c2, Is.EqualTo("ABCD12"));
        }

        [Test]
        public void JoinCode_RejectsInvalidCharsAndLength()
        {
            Assert.That(JoinCode.TryNormalize("AB-12!", out _), Is.False);
            Assert.That(JoinCode.TryNormalize("ABC", out _), Is.False);
            Assert.That(JoinCode.TryNormalize("ABCDEFGHIJ", out _), Is.False);
            Assert.That(JoinCode.TryNormalize(null, out _), Is.False);
        }

        [Test]
        public void Hello_VersionMismatch_Rejected()
        {
            var net = new LoopbackNet();
            Assert.That(net.Connect("Ana", version: "0.0.9").reason, Is.EqualTo(RejectReason.VersionMismatch));
            Assert.That(net.Connect("Bia", hash: 123).reason, Is.EqualTo(RejectReason.VersionMismatch));
        }

        [Test]
        public void Hello_Garbage_RejectedAsMalformed()
        {
            var net = new LoopbackNet();
            var junk = new byte[] { 1, 2, 3 };
            Assert.That(net.Host.Approve(9, junk, junk.Length, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo(RejectReason.Malformed));
        }

        [Test]
        public void Lobby_FullAfter4_AndClosedAfterStart()
        {
            var net = new LoopbackNet();
            for (int i = 0; i < 3; i++) Assert.That(net.Connect("P" + i).reason, Is.Null);
            Assert.That(net.Connect("Quinto").reason, Is.EqualTo(RejectReason.Full));

            var net2 = new LoopbackNet();
            net2.Connect("A");
            net2.Host.Start(7);
            Assert.That(net2.Connect("Atrasado").reason, Is.EqualTo(RejectReason.AlreadyStarted));
        }

        [Test]
        public void Solo_RejectsEveryone()
        {
            var net = new LoopbackNet(solo: true);
            Assert.That(net.Connect("Ana").reason, Is.EqualTo(RejectReason.Full));
        }

        [Test]
        public void Lobby_WelcomeSlot_PickAndDifficulty_ReachClients()
        {
            var net = new LoopbackNet();
            var (_, a, _) = net.Connect("Ana", "eletrica");
            var (_, b, _) = net.Connect("Bruno", "eletrica");
            Assert.That(a.LocalSlot, Is.EqualTo(1));
            Assert.That(b.LocalSlot, Is.EqualTo(2));
            b.SendPick("mecanica");
            net.Pump();
            net.Host.SetDifficulty(ES.Sim.Difficulty.Hard);
            net.Pump();
            Assert.That(a.Lobby.Difficulty, Is.EqualTo(ES.Sim.Difficulty.Hard));
            Assert.That(a.Lobby.Players.Select(p => p.Name + ":" + p.SpecializationId),
                Is.EqualTo(new[] { "Host:eletrica", "Ana:eletrica", "Bruno:mecanica" }));
        }

        [Test]
        public void Start_ClientsReceiveSameConfig()
        {
            var net = new LoopbackNet();
            var (_, a, _) = net.Connect("Ana");
            net.Host.SetDifficulty(ES.Sim.Difficulty.Easy);
            net.Host.Start(4242);
            net.Pump();
            Assert.That(a.Started, Is.True);
            Assert.That(a.Config.Seed, Is.EqualTo(4242UL));
            Assert.That(a.Config.Difficulty, Is.EqualTo(ES.Sim.Difficulty.Easy));
            Assert.That(a.Config.TeamSize, Is.EqualTo(2));
            Assert.That(a.Config.Coop, Is.True);
            Assert.That(net.Host.World.Balance.TeamSize, Is.EqualTo(2));
        }

        [Test]
        public void Client_InputMovesPlayerOnHost_AndPredictionAgrees()
        {
            var net = new LoopbackNet();
            var (peer, a, _) = net.Connect("Ana");
            net.Host.Start(1);
            net.Pump();
            net.Host.World.SpawningEnabled = false;
            var start = net.Host.World.Players[1].Pos;
            net.ClientInput = _ => new PlayerInput { MoveX = 127, ChoiceIndex = -1 };
            net.Run(1.0);
            var hostPos = net.Host.World.Players[1].Pos;
            Assert.That(hostPos.X - start.X, Is.GreaterThan(3.5f));
            Assert.That((a.Prediction.Predicted - hostPos).Length(), Is.LessThan(0.6f), "previsão perto do host (até alguns ticks à frente)");
        }

        [Test]
        public void Client_ReceivesEnemiesInSnapshot()
        {
            var net = new LoopbackNet();
            var (_, a, _) = net.Connect("Ana");
            net.Host.Start(3);
            net.Pump();
            var w = net.Host.World;
            w.SpawningEnabled = false;
            TestWorlds.Disarm(w);
            foreach (var p in w.Players.Slots) if (p != null) p.InvulnUntil = float.MaxValue;
            for (int i = 0; i < 40; i++) SpawnDirector.SpawnEnemy(w, 0, new Vec2(10 + i, 50), false);
            net.Run(0.5);
            Assert.That(a.Feed.Tracks.EnemyCount, Is.EqualTo(40));
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                Assert.That(a.Feed.Tracks.TryGetEnemy(w.Enemies.Id[i], out var track), Is.True);
                Assert.That((track.Cur - w.Enemies.Pos[i]).Length(), Is.LessThan(0.5f));
            }
        }

        [Test]
        public void EnemyDied_RemovesTrackOnClient()
        {
            var net = new LoopbackNet();
            var (_, a, _) = net.Connect("Ana");
            net.Host.Start(3);
            net.Pump();
            var w = net.Host.World;
            w.SpawningEnabled = false;
            TestWorlds.Disarm(w);
            int e = SpawnDirector.SpawnEnemy(w, 0, new Vec2(5, 5), false);
            ushort id = w.Enemies.Id[e];
            net.Run(0.2);
            Assert.That(a.Feed.Tracks.TryGetEnemy(id, out _), Is.True);
            CombatSystem.DamageEnemy(w, e, 999, Vec2.Zero, 0, 0, -1);
            net.Run(0.2);
            Assert.That(a.Feed.Tracks.TryGetEnemy(id, out _), Is.False);
        }

        [Test]
        public void StaleSnapshotAfterDeath_DoesNotResurrectEnemy()
        {
            var net = new LoopbackNet();
            var (_, a, _) = net.Connect("Ana");
            net.Host.Start(3);
            net.Pump();
            var w = net.Host.World;
            w.SpawningEnabled = false;
            TestWorlds.Disarm(w);
            foreach (var p in w.Players.Slots) if (p != null) p.InvulnUntil = float.MaxValue;
            int e = SpawnDirector.SpawnEnemy(w, 0, new Vec2(5, 5), false);
            ushort id = w.Enemies.Id[e];
            net.Run(0.2);
            net.HoldSnapshots = true;
            net.Run(0.1); // snapshots com o inimigo vivo ficam retidos
            CombatSystem.DamageEnemy(w, e, 999, Vec2.Zero, 0, 0, -1);
            net.Run(0.1); // o evento de morte chega antes dos snapshots antigos
            Assert.That(a.Feed.Tracks.TryGetEnemy(id, out _), Is.False);
            net.HoldSnapshots = false;
            net.ReleaseHeldSnapshots();
            Assert.That(a.Feed.Tracks.TryGetEnemy(id, out _), Is.False, "snapshot atrasado ressuscitou o inimigo");
        }

        [Test]
        public void Client_LevelUpOffer_ChoiceAppliedOnHost()
        {
            var net = new LoopbackNet();
            var (peer, a, _) = net.Connect("Ana");
            net.Host.Start(5);
            net.Pump();
            var w = net.Host.World;
            w.SpawningEnabled = false;
            ProgressionSystem.AddSharedXp(w, 10);
            net.Run(0.1);
            Assert.That(a.Feed.CurrentOffer, Is.Not.Null);
            short offer = a.Feed.CurrentOffer.OfferId;
            a.Choose(offer, 0);
            net.Run(0.2);
            Assert.That(w.Players[1].Progress.Current, Is.Null);
            Assert.That(a.Feed.CurrentOffer, Is.Null);
        }

        [Test]
        public void Client_SpecialActivatesOnHost()
        {
            var net = new LoopbackNet();
            var (_, a, _) = net.Connect("Ana", "eletrica");
            net.Host.Start(5);
            net.Pump();
            a.RequestSpecial();
            net.Run(0.2);
            Assert.That(net.Host.World.Players[1].Special.CooldownLeft, Is.GreaterThan(20f));
            Assert.That(a.Feed.Players[1].SpecialCooldown, Is.GreaterThan(20f));
        }

        [Test]
        public void MatchEnd_ReachesClient()
        {
            var net = new LoopbackNet();
            var (_, a, _) = net.Connect("Ana");
            net.Host.Start(5);
            net.Pump();
            CombatSystem.DamagePlayer(net.Host.World, 0, 1000);
            CombatSystem.DamagePlayer(net.Host.World, 1, 1000);
            net.Run(0.2);
            Assert.That(a.Feed.Phase, Is.EqualTo(MatchPhase.Defeat));
        }

        [Test]
        public void Disconnect_InMatch_SetsInactive_AndLobbyRemovesBeforeStart()
        {
            var net = new LoopbackNet();
            var (p1, _, _) = net.Connect("Ana");
            var (p2, b, _) = net.Connect("Bruno");
            net.Disconnect(p1);
            Assert.That(b.Lobby.Players.Count, Is.EqualTo(2));
            net.Host.Start(5);
            net.Pump();
            net.Disconnect(p2);
            Assert.That(net.Host.World.Players[b.LocalSlot].Status, Is.EqualTo(PlayerStatus.Inactive));
        }

        [Test]
        public void Malformed_Messages_KickAfter50()
        {
            var net = new LoopbackNet();
            var (peer, _, _) = net.Connect("Ana");
            net.Host.Start(5);
            ulong kicked = 0;
            net.Host.KickRequested += p => kicked = p;
            for (int i = 0; i < 50; i++) net.Host.OnMessage(peer, MessageType.Input, new byte[] { 1 }, 1);
            Assert.That(kicked, Is.EqualTo(peer));
        }

        [Test]
        public void HostFeed_HasLocalPlayerAndEvents()
        {
            var net = new LoopbackNet(solo: true);
            net.Host.Start(9);
            net.Run(0.5);
            Assert.That(net.Host.Feed.Players[0].Present, Is.True);
            Assert.That(net.Host.Feed.Elapsed, Is.GreaterThan(0.4f));
        }
    }
}
