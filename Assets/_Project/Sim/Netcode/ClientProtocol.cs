using System.Collections.Generic;

namespace ES.Sim
{
    /// <summary>
    /// Lado do cliente: recebe sala, início, snapshots e eventos; envia entradas a 30 Hz
    /// com previsão do próprio movimento; monta o MatchFeed para a apresentação.
    /// </summary>
    public sealed class ClientProtocol
    {
        readonly GameDatabase _db;
        readonly string _version;
        readonly ILink _link;
        readonly ByteWriter _w = new ByteWriter(64);
        readonly SnapshotView _scratch = new SnapshotView();
        readonly InterpBuffer _interp;
        readonly List<DecodedRuleEvent> _decoded = new List<DecodedRuleEvent>(64);
        TickClock _sendClock;
        ArenaDef _arena;
        ushort _seq;
        byte _specialCounter;
        short _choiceOffer;
        sbyte _choiceIndex = -1;
        int _lastTick = -1;
        bool _needsReset = true;
        double _now;

        public int LocalSlot { get; private set; } = -1;
        public readonly LobbyState Lobby = new LobbyState();
        public readonly MatchFeed Feed = new MatchFeed();
        public MatchConfig Config { get; private set; }
        public bool Started { get; private set; }
        /// <summary>O host enviou um conteúdo diferente do deste build.</summary>
        public bool ContentMismatch { get; private set; }
        public ClientPrediction Prediction { get; private set; }

        public ClientProtocol(GameDatabase db, string buildVersion, ILink link)
        {
            _db = db;
            _version = buildVersion;
            _link = link;
            _interp = new InterpBuffer(0.1, 0.2, 1.0 / db.Def.rules.tickRate);
        }

        /// <summary>Bytes do Hello enviados junto do pedido de conexão.</summary>
        public static byte[] BuildHello(string version, ulong contentHash, string name, string specializationId)
        {
            var w = new ByteWriter(64);
            HelloCodec.Write(w, new Hello { BuildVersion = version, ContentHash = contentHash, Name = name, SpecializationId = specializationId });
            var bytes = new byte[w.Length];
            System.Array.Copy(w.Buffer, bytes, w.Length);
            return bytes;
        }

        public void OnMessage(MessageType type, byte[] data, int length)
        {
            switch (type)
            {
                case MessageType.Welcome:
                    if (LobbyCodec.TryReadWelcome(data, length, out int slot)) { LocalSlot = slot; Feed.LocalSlot = slot; }
                    break;
                case MessageType.Lobby:
                    LobbyCodec.TryReadLobby(data, length, Lobby);
                    break;
                case MessageType.Start:
                    if (!LobbyCodec.TryReadMatchStart(data, length, out var cfg, out ulong hash)) return;
                    if (hash != _db.ContentHash) { ContentMismatch = true; return; }
                    Config = cfg;
                    Started = true;
                    Feed.Loadouts = new LoadoutTracker(_db);
                    Feed.Loadouts.Reset(cfg);
                    _arena = _db.Arena(cfg.ArenaId);
                    Prediction = new ClientPrediction(_db.Def.rules, _arena);
                    _sendClock = new TickClock(_db.Def.rules.tickRate, 5);
                    _needsReset = true;
                    break;
                case MessageType.Summary:
                    if (SummaryCodec.TryRead(data, length, out var summary)) Feed.Summary = summary;
                    break;
                case MessageType.Snapshot:
                    OnSnapshot(data, length);
                    break;
                case MessageType.Events:
                    _decoded.Clear();
                    if (_arena != null && RuleEventCodec.TryRead(data, length, _arena.width, _arena.height, _decoded))
                        foreach (var d in _decoded) Feed.ApplyRuleEvent(d);
                    break;
                case MessageType.HostBackground:
                    if (length == 1) Feed.HostBackgrounded = data[0] != 0;
                    break;
            }
        }

        void OnSnapshot(byte[] data, int length)
        {
            if (!Started) return;
            _scratch.Tick = -1;
            _scratch.Enemies.Clear();
            _scratch.Hostile.Clear();
            _scratch.Allies.Clear();
            _scratch.Cosmetic.Clear();
            for (int i = 0; i < _scratch.Players.Length; i++) _scratch.Players[i].Present = false;
            if (!SnapshotCodec.TryRead(data, length, _arena.width, _arena.height, _scratch)) return;

            int tick = _scratch.Tick;
            if (tick > _lastTick)
            {
                _lastTick = tick;
                _interp.Push(tick, _now);
                Feed.Tick = tick;
                Feed.Elapsed = tick / _db.Def.rules.tickRate;
                Feed.Tracks.PruneHostile(tick - 4);
                Feed.Tracks.PruneAllies(tick - 4);
            }

            for (int s = 0; s < _scratch.Players.Length; s++)
            {
                var p = _scratch.Players[s];
                if (!p.Present) continue;
                Feed.Players[s] = p;
                Feed.Tracks.SetPlayer(s, p.Pos, tick);
                if (s != LocalSlot) continue;
                if (_needsReset) { Prediction.Reset(p.Pos); _needsReset = false; }
                else Prediction.Reconcile(p.Pos, p.LastSeq, p.MoveSpeed > 0 ? p.MoveSpeed : _db.Def.rules.playerSpeed);
            }
            foreach (var kv in _scratch.Enemies) Feed.Tracks.SetEnemy(kv.Key, kv.Value.Pos, kv.Value.Type, kv.Value.Flags, kv.Value.HpFraction, tick, kv.Value.Status);
            foreach (var kv in _scratch.Hostile) Feed.Tracks.SetHostile(kv.Key, kv.Value.Pos, kv.Value.Type, tick);
            foreach (var kv in _scratch.Allies) Feed.Tracks.SetAlly(kv.Key, kv.Value.Pos, kv.Value.Sprite, kv.Value.Param, tick);
            Feed.Cosmetics.AddRange(_scratch.Cosmetic);
        }

        public void SendPick(string specializationId)
        {
            _w.Reset();
            LobbyCodec.WritePick(_w, specializationId);
            _link.Send(Peers.Host, MessageType.Pick, _w.Buffer, _w.Length, Channel.Reliable);
        }

        /// <summary>Escolha de carta; repetida nas entradas até o host confirmar.</summary>
        public void Choose(short offerId, int index)
        {
            _choiceOffer = offerId;
            _choiceIndex = (sbyte)index;
        }

        public void RequestSpecial() => _specialCounter++;

        /// <summary>Chamado a cada quadro: envia entradas a 30 Hz e avança a suavização.</summary>
        public void Update(double frameSeconds, double now, PlayerInput input)
        {
            _now = now;
            if (!Started) return;

            int ticks = _sendClock.Advance(frameSeconds);
            var me = LocalSlot >= 0 ? Feed.Players[LocalSlot] : default;
            bool active = me.Present && me.Status == PlayerStatus.Active;
            var offer = Feed.CurrentOffer;
            bool choosing = offer != null && offer.OfferId == _choiceOffer && _choiceIndex >= 0;

            for (int t = 0; t < ticks; t++)
            {
                var send = new PlayerInput
                {
                    Seq = ++_seq,
                    MoveX = active ? input.MoveX : (sbyte)0,
                    MoveY = active ? input.MoveY : (sbyte)0,
                    SpecialCounter = _specialCounter,
                    ChoiceOfferId = choosing ? _choiceOffer : (short)0,
                    ChoiceIndex = choosing ? _choiceIndex : (sbyte)-1,
                };
                _w.Reset();
                InputCodec.Write(_w, send);
                _link.Send(Peers.Host, MessageType.Input, _w.Buffer, _w.Length, Channel.Unreliable);
                if (active && !_needsReset) Prediction.Record(send, me.MoveSpeed > 0 ? me.MoveSpeed : _db.Def.rules.playerSpeed);
            }

            Prediction?.Tick((float)frameSeconds);
            if (_interp.TrySample(now, out int from, out int to, out float a)) Feed.RenderTick = from + (to - from) * a;
        }
    }
}
