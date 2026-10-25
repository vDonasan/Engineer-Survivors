using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ES.Sim;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace ES.Net
{
    public enum NetMode { None, Solo, Host, Client }

    /// <summary>
    /// Ponto único entre a interface e a partida: solo (HostProtocol sem rede), sala
    /// hospedada (HostProtocol + NGO + Relay) ou cliente (ClientProtocol + NGO).
    /// </summary>
    public sealed class NetGame : MonoBehaviour
    {
        public static NetGame Instance { get; private set; }

        GameDatabase _db;
        string _version;
        NetworkManager _nm;
        NgoLink _link;
        readonly SessionService _sessions = new SessionService();
        bool _leaving;

        public NetMode Mode { get; private set; }
        public HostProtocol Host { get; private set; }
        public ClientProtocol Client { get; private set; }
        public string RoomCode => _sessions.Code;
        public MatchFeed Feed => Host?.Feed ?? Client?.Feed;
        public LobbyState Lobby => Host?.Lobby ?? Client?.Lobby;
        public bool Started => Host != null ? Host.World != null : Client != null && Client.Started;
        public int LocalSlot => Host != null ? 0 : Client?.LocalSlot ?? -1;
        public MatchConfig Config => Host?.World?.Config ?? Client?.Config;
        public GameDatabase Db => _db;
        /// <summary>Só no solo: congela a simulação (menu de pausa).</summary>
        public bool Paused { get; set; }
        /// <summary>Bytes/s enviados e recebidos e latência (ms), atualizados a cada segundo.</summary>
        public float SentPerSecond { get; private set; }
        public float ReceivedPerSecond { get; private set; }
        public float RttMs { get; private set; }

        /// <summary>Erro para o jogador (chave da tabela de textos).</summary>
        public event Action<string> Error;
        /// <summary>Conexão encerrada (chave da tabela de textos).</summary>
        public event Action<string> Disconnected;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void ES_RegisterVisibility(string gameObjectName);
#endif

        public static NetGame Create(GameDatabase db, string buildVersion)
        {
            var go = new GameObject("NetGame");
            DontDestroyOnLoad(go);
            var g = go.AddComponent<NetGame>();
            g._db = db;
            g._version = buildVersion;
            g.SetupNetworkManager();
            Instance = g;
#if UNITY_WEBGL && !UNITY_EDITOR
            ES_RegisterVisibility(go.name);
#endif
            return g;
        }

        void SetupNetworkManager()
        {
            var go = new GameObject("NetworkManager");
            DontDestroyOnLoad(go);
            var transport = go.AddComponent<UnityTransport>();
            transport.UseWebSockets = Application.platform == RuntimePlatform.WebGLPlayer;
            _nm = go.AddComponent<NetworkManager>();
            _nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                ConnectionApproval = true,
                EnableSceneManagement = false,
                TickRate = 30,
            };
            _nm.ConnectionApprovalCallback = OnApproval;
            _nm.OnClientConnectedCallback += OnClientConnected;
            _nm.OnClientDisconnectCallback += OnClientDisconnected;
            _nm.OnServerStarted += () => RegisterMessages();
        }

        // ---------- Modos ----------

        public void StartSolo(string name, string specializationId, Difficulty difficulty, ulong seed)
        {
            Shutdown();
            Mode = NetMode.Solo;
            Host = new HostProtocol(_db, _version, new NullLink(), Hello(name, specializationId), solo: true);
            Host.SetDifficulty(difficulty);
            Host.Start(seed);
        }

        public async Task<bool> CreateRoom(string name, string specializationId)
        {
            Shutdown();
            Mode = NetMode.Host;
            _link = new NgoLink(_nm);
            Host = new HostProtocol(_db, _version, _link, Hello(name, specializationId));
            Host.KickRequested += peer => _nm.DisconnectClient(peer, RejectReason.Kicked.ToString());
            try
            {
                await _sessions.CreateAsync();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ES] Falha ao criar sala: " + e);
                await LeaveAsync();
                Error?.Invoke(SessionService.ErrorKey(e));
                return false;
            }
        }

        public async Task<bool> JoinRoom(string rawCode, string name, string specializationId)
        {
            if (!JoinCode.TryNormalize(rawCode, out string code)) { Error?.Invoke("erro_codigo_invalido"); return false; }
            Shutdown();
            Mode = NetMode.Client;
            _link = new NgoLink(_nm);
            Client = new ClientProtocol(_db, _version, _link);
            _nm.NetworkConfig.ConnectionData = ClientProtocol.BuildHello(_version, _db.ContentHash, name, specializationId);
            _nm.OnClientStarted += OnClientStarted;
            try
            {
                await _sessions.JoinAsync(code);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ES] Falha ao entrar: " + e);
                await LeaveAsync();
                Error?.Invoke(SessionService.ErrorKey(e));
                return false;
            }
        }

        void OnClientStarted()
        {
            _nm.OnClientStarted -= OnClientStarted;
            RegisterMessages();
        }

        public async void StartMatch(ulong seed)
        {
            if (Host == null || Host.World != null) return;
            Host.Start(seed);
            if (Mode == NetMode.Host)
            {
                try { await _sessions.LockAsync(); }
                catch (Exception e) { Debug.LogWarning("[ES] Não trancou a sala: " + e.Message); }
            }
        }

        public void SetDifficulty(Difficulty d) => Host?.SetDifficulty(d);

        public void PickSpecialization(string id)
        {
            if (Host != null) Host.SetLocalSpecialization(id);
            else Client?.SendPick(id);
        }

        /// <summary>Movimento do jogador local (a cada quadro). Escolha e especial têm métodos próprios.</summary>
        public void SetLocalInput(PlayerInput input)
        {
            if (Host == null) { _clientInput = input; return; }
            var offer = Host.Feed.CurrentOffer;
            bool choosing = offer != null && offer.OfferId == _hostChoiceOffer && _hostChoiceIndex >= 0;
            input.SpecialCounter = _hostSpecial;
            input.ChoiceOfferId = choosing ? _hostChoiceOffer : (short)0;
            input.ChoiceIndex = choosing ? _hostChoiceIndex : (sbyte)-1;
            Host.SetLocalInput(input);
        }

        public void RequestSpecial()
        {
            if (Host != null) _hostSpecial++;
            else Client?.RequestSpecial();
        }

        public void Choose(short offerId, int index)
        {
            if (Host != null) { _hostChoiceOffer = offerId; _hostChoiceIndex = (sbyte)index; }
            else Client?.Choose(offerId, index);
        }

        PlayerInput _clientInput = PlayerInput.None;
        byte _hostSpecial;
        short _hostChoiceOffer;
        sbyte _hostChoiceIndex = -1;

        public async Task LeaveAsync()
        {
            if (_leaving) return;
            _leaving = true;
            try
            {
                if (_nm != null && _nm.IsListening) _nm.Shutdown();
                await _sessions.LeaveAsync();
            }
            finally
            {
                Shutdown();
                _leaving = false;
            }
        }

        void Shutdown()
        {
            _link?.Unregister();
            _link = null;
            Host = null;
            Client = null;
            Mode = NetMode.None;
            Paused = false;
            _clientInput = PlayerInput.None;
            _hostSpecial = 0;
            _hostChoiceOffer = 0;
            _hostChoiceIndex = -1;
        }

        Hello Hello(string name, string spec) => new Hello { BuildVersion = _version, ContentHash = _db.ContentHash, Name = name, SpecializationId = spec };

        // ---------- NGO ----------

        void RegisterMessages()
        {
            _link?.Register((sender, type, data, length) =>
            {
                if (Host != null)
                {
                    if (length < 0) Host.OnMessage(sender, MessageType.Input, data, 0); // conta como malformada
                    else Host.OnMessage(sender, type, data, length);
                }
                else if (Client != null && length >= 0) Client.OnMessage(type, data, length);
            });
        }

        void OnApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;
            if (request.ClientNetworkId == _nm.LocalClientId) { response.Approved = true; return; }
            if (Host == null) { response.Approved = false; response.Reason = RejectReason.Full.ToString(); return; }
            var payload = request.Payload ?? new byte[0];
            response.Approved = Host.Approve(request.ClientNetworkId, payload, payload.Length, out var reason);
            if (!response.Approved) response.Reason = reason.ToString();
        }

        void OnClientConnected(ulong clientId)
        {
            if (Host != null && clientId != _nm.LocalClientId) Host.OnPeerConnected(clientId);
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (Host != null)
            {
                if (clientId != _nm.LocalClientId) Host.OnPeerDisconnected(clientId);
                return;
            }
            if (Client != null && (clientId == _nm.LocalClientId || !_nm.IsConnectedClient))
            {
                string key = SessionService.RejectKey(_nm.DisconnectReason);
                _ = LeaveAsync();
                Disconnected?.Invoke(key);
            }
        }

        // ---------- Laço ----------

        void Update()
        {
            double dt = Time.unscaledDeltaTime;
            UpdateStats();
            if (Host != null) { if (!(Paused && Mode == NetMode.Solo)) Host.Update(dt); }
            else if (Client != null) Client.Update(dt, Time.realtimeSinceStartupAsDouble, _clientInput);
        }

        float _statsAt;
        long _lastSent, _lastReceived;

        void UpdateStats()
        {
            float now = Time.unscaledTime;
            if (now < _statsAt) return;
            float span = Mathf.Max(0.25f, now - (_statsAt - 1f));
            _statsAt = now + 1f;
            long sent = _link?.BytesSent ?? 0, received = _link?.BytesReceived ?? 0;
            SentPerSecond = Mathf.Max(0, sent - _lastSent) / span;
            ReceivedPerSecond = Mathf.Max(0, received - _lastReceived) / span;
            _lastSent = sent;
            _lastReceived = received;
            RttMs = 0;
            if (_nm != null && _nm.IsListening && _nm.NetworkConfig.NetworkTransport is UnityTransport ut)
            {
                if (Client != null) RttMs = ut.GetCurrentRtt(NetworkManager.ServerClientId);
                else if (Host != null && _nm.ConnectedClientsIds.Count > 1)
                {
                    float sum = 0; int n = 0;
                    foreach (var id in _nm.ConnectedClientsIds) if (id != _nm.LocalClientId) { sum += ut.GetCurrentRtt(id); n++; }
                    RttMs = n > 0 ? sum / n : 0;
                }
            }
        }

        /// <summary>Chamado pelo .jslib quando a aba muda de visibilidade.</summary>
        public void OnVisibilityChanged(string state)
        {
            if (Mode == NetMode.Host && Host != null) Host.SetBackgrounded(state == "hidden");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        sealed class NullLink : ILink
        {
            public void Send(ulong peer, MessageType type, byte[] data, int length, Channel channel) { }
        }
    }
}
