using System;
using System.Collections.Generic;
using System.Text;
using ES.Content;
using ES.Net;
using ES.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = ES.Sim.PlayerInput;

namespace ES.Game.Proof
{
    /// <summary>
    /// Cena de prova de rede (Task 20): sala via Relay, movimento, inimigos e eventos
    /// sincronizados, desenhados com quadrados. Automatizável pela URL:
    /// ?mode=host&amp;autostart=2  |  ?mode=join&amp;code=ABC123  |  ?mode=solo
    /// </summary>
    public sealed class NetProof : MonoBehaviour
    {
        public ContentDatabaseAsset content;

        NetGame _net;
        GameDatabase _db;
        readonly RenderFrame _frame = new RenderFrame();
        readonly Dictionary<ushort, SpriteRenderer> _enemyViews = new Dictionary<ushort, SpriteRenderer>();
        readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        readonly HashSet<ushort> _seen = new HashSet<ushort>();
        readonly List<ushort> _gone = new List<ushort>();
        readonly SpriteRenderer[] _players = new SpriteRenderer[4];
        Sprite _square;
        string _code = "", _status = "", _name = "Engenheiro(a)";
        string _spec = "eletrica";
        int _autostart;
        float _logAt;
        static readonly Color[] PlayerColors = { new Color(1f, 0.7f, 0.2f), new Color(0.25f, 0.9f, 1f), new Color(0.5f, 1f, 0.4f), new Color(1f, 0.4f, 0.9f) };

        void Start()
        {
            Application.targetFrameRate = 60;
            var r = content.Build();
            if (r.Errors.Length > 0) { _status = "conteúdo inválido: " + r.Errors[0]; return; }
            _db = r.Db;
            _net = NetGame.Create(_db, Application.version);
            _net.Error += k => Log("ERRO " + k);
            _net.Disconnected += k => Log("DESCONECTADO " + k);

            var tex = new Texture2D(4, 4);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            _square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            BuildArena();
            for (int i = 0; i < 4; i++) { _players[i] = NewView(0.8f, PlayerColors[i], 10); _players[i].gameObject.SetActive(false); }
            HandleUrl();
        }

        void HandleUrl()
        {
            var q = ParseQuery(Application.absoluteURL);
            if (q.TryGetValue("spec", out var spec)) _spec = spec;
            if (q.TryGetValue("name", out var name)) _name = name;
            if (q.TryGetValue("mode", out var mode))
            {
                if (mode == "host") { int.TryParse(q.TryGetValue("autostart", out var a) ? a : "0", out _autostart); CreateRoom(); }
                else if (mode == "join" && q.TryGetValue("code", out var code)) { _code = code; JoinRoom(); }
                else if (mode == "solo") _net.StartSolo(_name, _spec, Difficulty.Normal, (ulong)Environment.TickCount);
            }
        }

        async void CreateRoom()
        {
            _status = "criando sala...";
            if (await _net.CreateRoom(_name, _spec)) Log("CODE=" + _net.RoomCode);
        }

        async void JoinRoom()
        {
            _status = "entrando...";
            if (await _net.JoinRoom(_code, _name, _spec)) Log("JOINED " + _code);
        }

        void Update()
        {
            if (_net == null) return;
            var kb = Keyboard.current;
            var input = PlayerInput.None;
            if (kb != null)
            {
                float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);
                float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);
                input.MoveX = Quant.Axis(x);
                input.MoveY = Quant.Axis(y);
            }
            _net.SetLocalInput(input);
            if (kb != null && kb.spaceKey.wasPressedThisFrame) _net.RequestSpecial();

            if (_autostart > 0 && _net.Mode == NetMode.Host && !_net.Started && _net.Lobby.Players.Count >= _autostart)
            {
                _net.StartMatch((ulong)Environment.TickCount);
                Log("START players=" + _net.Lobby.Players.Count);
            }
            Draw();
            if (Time.unscaledTime >= _logAt && _net.Started) { _logAt = Time.unscaledTime + 2f; LogState(); }
        }

        void Draw()
        {
            var feed = _net.Feed;
            if (feed == null || !_net.Started) return;
            feed.Tracks.Fill(feed.RenderTick, _frame);
            feed.RuleEvents.Clear();
            feed.Cosmetics.Clear();

            _seen.Clear();
            foreach (var e in _frame.Enemies)
            {
                _seen.Add(e.Id);
                if (!_enemyViews.TryGetValue(e.Id, out var v))
                {
                    var r = _db.Enemy(e.Type).Def.radius * 2f;
                    v = _pool.Count > 0 ? _pool.Pop() : NewView(1, Color.red, 5);
                    v.transform.localScale = new Vector3(r, r, 1);
                    v.color = (e.Flags & EnemyFlags.Elite) != 0 ? Color.magenta : e.Type == 0 ? new Color(1f, 0.3f, 0.3f) : e.Type == 1 ? new Color(0.9f, 0.6f, 0.2f) : new Color(0.3f, 0.6f, 1f);
                    v.gameObject.SetActive(true);
                    _enemyViews[e.Id] = v;
                }
                v.transform.position = new Vector3(e.Pos.X, e.Pos.Y, 0);
            }
            _gone.Clear();
            foreach (var kv in _enemyViews) if (!_seen.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var id in _gone) { var v = _enemyViews[id]; v.gameObject.SetActive(false); _pool.Push(v); _enemyViews.Remove(id); }

            int local = _net.LocalSlot;
            for (int s = 0; s < 4; s++)
            {
                var snap = feed.Players[s];
                _players[s].gameObject.SetActive(snap.Present);
                if (!snap.Present) continue;
                var pos = s == local && _net.Client?.Prediction != null ? _net.Client.Prediction.RenderPosition : _frame.PlayerPositions[s];
                _players[s].transform.position = new Vector3(pos.X, pos.Y, 0);
                _players[s].color = snap.Status == PlayerStatus.Active ? PlayerColors[s] : Color.gray;
                if (s == local) Camera.main.transform.position = new Vector3(pos.X, pos.Y, -10);
            }
        }

        void LogState()
        {
            var f = _net.Feed;
            var sb = new StringBuilder();
            sb.Append($"STATE mode={_net.Mode} slot={_net.LocalSlot} tick={f.Tick} t={f.Elapsed:0.0} enemies={f.Tracks.EnemyCount} phase={f.Phase}");
            for (int s = 0; s < 4; s++) if (f.Players[s].Present) sb.Append($" p{s}=({f.Players[s].Pos.X:0.0},{f.Players[s].Pos.Y:0.0}) hp={f.Players[s].Hp:0} lv={f.Players[s].Level}");
            Log(sb.ToString());
        }

        void Log(string s)
        {
            _status = s.StartsWith("STATE") ? _status : s;
            Debug.Log("[ES] " + s);
        }

        void OnGUI()
        {
            GUI.skin.label.fontSize = 18;
            GUI.skin.button.fontSize = 18;
            GUI.skin.textField.fontSize = 18;
            GUILayout.BeginArea(new Rect(10, 10, 420, 600));
            GUILayout.Label("Engineering Survivors — prova de rede");
            GUILayout.Label(_status);
            if (_net == null) { GUILayout.EndArea(); return; }
            if (_net.Mode == NetMode.None)
            {
                _name = GUILayout.TextField(_name, 16);
                if (GUILayout.Button(_spec == "eletrica" ? "Especialização: Elétrica" : "Especialização: Mecânica")) _spec = _spec == "eletrica" ? "mecanica" : "eletrica";
                if (GUILayout.Button("Jogar solo")) _net.StartSolo(_name, _spec, Difficulty.Normal, (ulong)Environment.TickCount);
                if (GUILayout.Button("Criar sala")) CreateRoom();
                _code = GUILayout.TextField(_code, 8);
                if (GUILayout.Button("Entrar com código")) JoinRoom();
            }
            else if (!_net.Started && _net.Lobby != null)
            {
                GUILayout.Label("Código da sala: " + (_net.RoomCode ?? "..."));
                GUILayout.Label($"Jogadores: {_net.Lobby.Players.Count}/4 · dificuldade {_net.Lobby.Difficulty}");
                foreach (var p in _net.Lobby.Players) GUILayout.Label($"  {p.Slot}: {p.Name} ({p.SpecializationId})");
                if (_net.Mode == NetMode.Host && GUILayout.Button("Iniciar partida")) _net.StartMatch((ulong)Environment.TickCount);
            }
            else if (_net.Feed != null)
            {
                var f = _net.Feed;
                GUILayout.Label($"t={f.Elapsed:0.0}s inimigos={f.Tracks.EnemyCount} fase={f.Phase}");
                if (f.HostBackgrounded) GUILayout.Label("O anfitrião minimizou o jogo: a partida pode travar.");
                if (f.CurrentOffer != null)
                    for (int i = 0; i < f.CurrentOffer.Options.Length; i++)
                    {
                        var o = f.CurrentOffer.Options[i];
                        string label = o.Kind == OptionKind.Evolution ? _db.Evolution(o.Ref).Def.name : _db.Ability(o.Ref).Def.name;
                        if (GUILayout.Button($"[{o.Kind}] {label}")) Choose(f.CurrentOffer.OfferId, i);
                    }
            }
            GUILayout.EndArea();
        }

        void Choose(short offer, int index) => _net.Choose(offer, index);

        SpriteRenderer NewView(float size, Color c, int order)
        {
            var go = new GameObject("v");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _square;
            sr.color = c;
            sr.sortingOrder = order;
            go.transform.localScale = new Vector3(size, size, 1);
            return sr;
        }

        void BuildArena()
        {
            var arena = _db.Arena("laboratorio");
            var floor = NewView(1, new Color(0.18f, 0.19f, 0.22f), -10);
            floor.transform.position = new Vector3(arena.width / 2, arena.height / 2, 1);
            floor.transform.localScale = new Vector3(arena.width, arena.height, 1);
            foreach (var o in arena.obstacles)
            {
                var v = NewView(1, new Color(0.3f, 0.33f, 0.38f), -5);
                v.transform.position = new Vector3(o.x + o.w / 2, o.y + o.h / 2, 0.5f);
                v.transform.localScale = new Vector3(o.w, o.h, 1);
            }
            Camera.main.orthographicSize = 9;
            Camera.main.backgroundColor = Color.black;
        }

        static Dictionary<string, string> ParseQuery(string url)
        {
            var d = new Dictionary<string, string>();
            int q = url?.IndexOf('?') ?? -1;
            if (q < 0) return d;
            foreach (var part in url.Substring(q + 1).Split('&'))
            {
                var kv = part.Split('=');
                if (kv.Length == 2) d[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
            }
            return d;
        }
    }
}
