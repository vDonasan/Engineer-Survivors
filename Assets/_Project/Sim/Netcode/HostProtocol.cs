using System;
using System.Collections.Generic;

namespace ES.Sim
{
    /// <summary>
    /// Autoridade da partida: sala (aprovação, seleção, dificuldade), início, ticks da
    /// simulação, envio de snapshots e eventos, validação das entradas dos clientes.
    /// Independente de engine; o adaptador de rede chama os métodos On*.
    /// </summary>
    public sealed class HostProtocol
    {
        const int MaxQueuedInputs = 8;
        const int SnapshotEveryTicks = 2;
        static readonly OfferOption[] NoOptions = new OfferOption[0];

        readonly GameDatabase _db;
        readonly string _version;
        readonly ILink _link;
        readonly bool _solo;
        readonly Dictionary<ulong, LobbyEntry> _byPeer = new Dictionary<ulong, LobbyEntry>();
        readonly Dictionary<ulong, LobbyEntry> _pending = new Dictionary<ulong, LobbyEntry>();
        readonly InputValidator _validator = new InputValidator();
        readonly Queue<PlayerInput>[] _queues = new Queue<PlayerInput>[PlayerStore.MaxSlots];
        readonly PlayerInput[] _last = new PlayerInput[PlayerStore.MaxSlots];
        readonly PlayerInput[] _inputs = new PlayerInput[PlayerStore.MaxSlots];
        readonly TickClock _clock;
        readonly ByteWriter _w = new ByteWriter(2048);
        readonly ChunkSet _chunks = new ChunkSet();
        readonly List<CosmeticEvent> _cosmetics = new List<CosmeticEvent>(128);
        int _damageQueued;
        readonly List<AllySnap> _allies = new List<AllySnap>(SnapshotCodec.MaxAllies);
        readonly List<RuleEvent> _rules = new List<RuleEvent>(128);
        PlayerInput _local = PlayerInput.None;
        double _time;

        public readonly LobbyState Lobby = new LobbyState();
        public readonly MatchFeed Feed = new MatchFeed();
        public World World { get; private set; }
        public bool Solo => _solo;
        public int PeerCount => _byPeer.Count;

        /// <summary>Pedido ao adaptador para desconectar um par (mensagens malformadas).</summary>
        public event Action<ulong> KickRequested;

        public HostProtocol(GameDatabase db, string buildVersion, ILink link, Hello local, bool solo = false)
        {
            _db = db;
            _version = buildVersion;
            _link = link;
            _solo = solo;
            _clock = new TickClock(db.Def.rules.tickRate, 5);
            for (int i = 0; i < _queues.Length; i++) { _queues[i] = new Queue<PlayerInput>(MaxQueuedInputs); _last[i] = PlayerInput.None; }
            Lobby.Players.Add(new LobbyEntry { Peer = Peers.Host, Slot = 0, Name = HelloCodec.SanitizeName(local.Name), SpecializationId = local.SpecializationId });
        }

        // ---------- Sala ----------

        public bool Approve(ulong peer, byte[] hello, int length, out RejectReason reason)
        {
            reason = RejectReason.Malformed;
            if (!HelloCodec.TryRead(hello, length, out var h) || _db.SpecializationIndex(h.SpecializationId) < 0) return false;
            if (h.BuildVersion != _version || h.ContentHash != _db.ContentHash) { reason = RejectReason.VersionMismatch; return false; }
            if (Lobby.Started) { reason = RejectReason.AlreadyStarted; return false; }
            if (_solo || Lobby.Players.Count + _pending.Count >= PlayerStore.MaxSlots) { reason = RejectReason.Full; return false; }

            int slot = 0;
            while (SlotTaken(slot)) slot++;
            _pending[peer] = new LobbyEntry { Peer = peer, Slot = slot, Name = h.Name, SpecializationId = h.SpecializationId };
            return true;
        }

        bool SlotTaken(int slot)
        {
            foreach (var p in Lobby.Players) if (p.Slot == slot) return true;
            foreach (var p in _pending.Values) if (p.Slot == slot) return true;
            return false;
        }

        public void OnPeerConnected(ulong peer)
        {
            if (!_pending.TryGetValue(peer, out var e)) return;
            _pending.Remove(peer);
            _byPeer[peer] = e;
            Lobby.Players.Add(e);
            Lobby.Players.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            SendWelcome(e);
            BroadcastLobby();
        }

        public void OnPeerDisconnected(ulong peer)
        {
            _pending.Remove(peer);
            if (!_byPeer.TryGetValue(peer, out var e)) return;
            _byPeer.Remove(peer);
            if (!Lobby.Started)
            {
                Lobby.Players.Remove(e);
                BroadcastLobby();
                return;
            }
            if (World != null) MatchRules.SetInactive(World, e.Slot);
        }

        public void SetDifficulty(Difficulty d)
        {
            if (Lobby.Started) return;
            Lobby.Difficulty = d;
            BroadcastLobby();
        }

        public void SetLocalSpecialization(string id)
        {
            if (Lobby.Started || _db.SpecializationIndex(id) < 0) return;
            Lobby.Players[0].SpecializationId = id;
            BroadcastLobby();
        }

        public void OnMessage(ulong peer, MessageType type, byte[] data, int length)
        {
            if (!_byPeer.TryGetValue(peer, out var e)) return;
            switch (type)
            {
                case MessageType.Pick:
                    if (!Lobby.Started && LobbyCodec.TryReadPick(data, length, out string spec) && _db.SpecializationIndex(spec) >= 0)
                    {
                        e.SpecializationId = spec;
                        BroadcastLobby();
                    }
                    else Malformed(peer, e);
                    break;
                case MessageType.Input:
                {
                    var r = new ByteReader(data, length);
                    if (World != null && InputCodec.TryRead(ref r, out var input) && r.Remaining == 0)
                    {
                        if (_validator.Accept(e.Slot, input, _time, out var clean))
                        {
                            var q = _queues[e.Slot];
                            if (q.Count >= MaxQueuedInputs) q.Dequeue();
                            q.Enqueue(clean);
                        }
                    }
                    else Malformed(peer, e);
                    break;
                }
                default:
                    Malformed(peer, e);
                    break;
            }
        }

        void Malformed(ulong peer, LobbyEntry e)
        {
            _validator.CountMalformed(e.Slot);
            if (_validator.ShouldKick(e.Slot)) KickRequested?.Invoke(peer);
        }

        // ---------- Partida ----------

        /// <summary>Tranca a sala, fixa slots contíguos e inicia a simulação.</summary>
        public void Start(ulong seed)
        {
            if (Lobby.Started) return;
            Lobby.Started = true;
            _pending.Clear();

            Lobby.Players.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            var roster = new PlayerSlot[Lobby.Players.Count];
            for (int i = 0; i < roster.Length; i++)
            {
                var p = Lobby.Players[i];
                p.Slot = i;
                roster[i] = new PlayerSlot { Name = p.Name, SpecializationId = p.SpecializationId };
            }
            var cfg = new MatchConfig
            {
                Seed = seed, Difficulty = Lobby.Difficulty, TeamSize = roster.Length, ArenaId = "laboratorio",
                Roster = roster, Coop = !_solo && roster.Length > 1,
            };
            World = new World(_db, cfg);
            Feed.LocalSlot = 0;
            Feed.Loadouts = new LoadoutTracker(_db);
            Feed.Loadouts.Reset(cfg);

            foreach (var e in _byPeer.Values) SendWelcome(e);
            _w.Reset();
            LobbyCodec.WriteMatchStart(_w, cfg, _db.ContentHash);
            Broadcast(MessageType.Start, Channel.Reliable);
            Harvest();
            FeedFromWorld();
        }

        public void SetLocalInput(in PlayerInput input) => _local = input;

        /// <summary>Avança a simulação pelo tempo do quadro. Retorna os ticks executados.</summary>
        public int Update(double frameSeconds)
        {
            _time += frameSeconds;
            if (World == null) return 0;
            int ticks = _clock.Advance(frameSeconds);
            for (int t = 0; t < ticks; t++)
            {
                Harvest(); // eventos gerados fora do Step (desconexões etc.)
                _inputs[0] = _local;
                for (int s = 1; s < PlayerStore.MaxSlots; s++)
                {
                    var q = _queues[s];
                    if (q.Count > 0) _last[s] = q.Dequeue();
                    else _last[s].ChoiceIndex = -1;
                    _inputs[s] = _last[s];
                }
                World.Step(_inputs);
                Harvest();
                if (World.Tick % SnapshotEveryTicks == 0) SendSnapshot();
                FeedFromWorld();
            }
            Feed.RenderTick = World.Tick - 1 + (float)_clock.Alpha;
            return ticks;
        }

        public void SetBackgrounded(bool hidden)
        {
            _w.Reset();
            _w.Bool(hidden);
            Broadcast(MessageType.HostBackground, Channel.Reliable);
        }

        /// <summary>Recolhe os eventos pendentes do World: envia aos clientes e aplica no feed local.</summary>
        void Harvest()
        {
            var ev = World.Events;
            if (ev.Rule.Count > 0)
            {
                _rules.Clear();
                _rules.AddRange(ev.Rule);
                if (_byPeer.Count > 0)
                {
                    _w.Reset();
                    RuleEventCodec.Write(_w, _rules, World);
                    Broadcast(MessageType.Events, Channel.Reliable);
                }
                foreach (var e in _rules) Feed.ApplyRuleEvent(Decode(e));
                foreach (var e in _rules) if (e.Kind == RuleEventKind.MatchEnded) SendSummary();
            }
            if (ev.Cosmetic.Count > 0)
            {
                Feed.Cosmetics.AddRange(ev.Cosmetic);
                if (_byPeer.Count > 0)
                {
                    int cap = World.Rules.capCosmeticEvents;
                    for (int i = 0; i < ev.Cosmetic.Count && _cosmetics.Count < cap; i++)
                    {
                        // números de dano: no máximo metade da cota do snapshot
                        if (ev.Cosmetic[i].Kind == CosmeticKind.Damage && ++_damageQueued > cap / 2) continue;
                        _cosmetics.Add(ev.Cosmetic[i]);
                    }
                }
            }
            ev.Clear();
        }

        DecodedRuleEvent Decode(in RuleEvent e)
        {
            var d = new DecodedRuleEvent { Event = e, Options = NoOptions };
            if (e.Kind == RuleEventKind.LevelUpOffer)
            {
                var cur = World.Players[e.A]?.Progress.Current;
                if (cur != null && cur.OfferId == e.B) d.Options = cur.Options;
            }
            return d;
        }

        void SendSummary()
        {
            Feed.Summary = MatchSummary.FromWorld(World);
            if (_byPeer.Count == 0) return;
            _w.Reset();
            SummaryCodec.Write(_w, Feed.Summary);
            Broadcast(MessageType.Summary, Channel.Reliable);
        }

        void SendSnapshot()
        {
            if (_byPeer.Count == 0) return;
            SnapshotCodec.Write(World, _cosmetics, _chunks);
            _cosmetics.Clear();
            _damageQueued = 0;
            for (int c = 0; c < _chunks.Count; c++)
                foreach (var peer in _byPeer.Keys) _link.Send(peer, MessageType.Snapshot, _chunks.Buffers[c], _chunks.Lengths[c], Channel.Unreliable);
        }

        void FeedFromWorld()
        {
            var w = World;
            Feed.Tick = w.Tick;
            Feed.Elapsed = w.Elapsed;
            Feed.Phase = w.Phase;
            var e = w.Enemies;
            for (int i = 0; i < e.Count; i++)
            {
                if ((e.Flags[i] & EnemyFlags.Dead) != 0) continue;
                Feed.Tracks.SetEnemy(e.Id[i], e.Pos[i], e.Type[i], e.Flags[i], e.MaxHp[i] > 0 ? e.Hp[i] / e.MaxHp[i] : 1f, w.Tick, EnemyStatus.Bits(e, i, w.Elapsed));
            }
            AllyList.Collect(w, _allies, SnapshotCodec.MaxAllies * 2);
            foreach (var a in _allies) Feed.Tracks.SetAlly(a.Id, a.Pos, a.Sprite, a.Param, w.Tick);
            Feed.Tracks.PruneAllies(w.Tick);
            var h = w.Hostile;
            for (int i = 0; i < h.Count; i++) Feed.Tracks.SetHostile(h.Id[i], h.Pos[i], h.Type[i], w.Tick);
            Feed.Tracks.PruneHostile(w.Tick);
            foreach (var p in w.Players.Slots)
            {
                if (p == null) continue;
                Feed.Tracks.SetPlayer(p.Slot, p.Pos, w.Tick);
                byte flags = 0;
                if (p.InvulnUntil > w.Elapsed) flags |= PlayerSnap.FlagInvulnerable;
                if (p.SpecialActiveLeft > 0) flags |= PlayerSnap.FlagSpecialActive;
                if (p.Progress.Current != null) flags |= PlayerSnap.FlagHasOffer;
                Feed.Players[p.Slot] = new PlayerSnap
                {
                    Present = true, Status = p.Status, Pos = p.Pos, Vel = p.Vel, Facing = p.Facing,
                    Hp = p.Hp, MaxHp = p.Stats.Get(StatId.MaxHp), Flags = flags, LastSeq = p.LastInputSeq,
                    SpecialCooldown = p.Special?.CooldownLeft ?? 0, SpecialActiveLeft = p.SpecialActiveLeft,
                    Level = p.Progress.Level, XpProgress = p.Progress.Xp / Math.Max(1, XpCurve.ToNext(w.Rules, p.Progress.Level)),
                    MoveSpeed = p.Stats.Get(StatId.MoveSpeed), Tick = w.Tick,
                };
            }
            var local = w.Players[0]?.Progress.Current;
            Feed.CurrentOffer = local;
        }

        void SendWelcome(LobbyEntry e)
        {
            _w.Reset();
            LobbyCodec.WriteWelcome(_w, e.Slot);
            _link.Send(e.Peer, MessageType.Welcome, _w.Buffer, _w.Length, Channel.Reliable);
        }

        void BroadcastLobby()
        {
            _w.Reset();
            LobbyCodec.WriteLobby(_w, Lobby);
            Broadcast(MessageType.Lobby, Channel.Reliable);
        }

        void Broadcast(MessageType type, Channel channel)
        {
            foreach (var peer in _byPeer.Keys) _link.Send(peer, type, _w.Buffer, _w.Length, channel);
        }
    }
}
