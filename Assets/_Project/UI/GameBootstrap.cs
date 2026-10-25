using System;
using System.Collections.Generic;
using System.Text;
using ES.Content;
using ES.Game;
using ES.Net;
using ES.Sim;
using UnityEngine;

namespace ES.UI
{
    /// <summary>
    /// Ponto de entrada da cena Main: carrega conteúdo, configurações e textos, cria rede,
    /// áudio, câmera pixel perfect, apresentação da partida e interface.
    /// Automação pela URL (testes): ?mode=setup (tela de escolha) | ?mode=solo | ?mode=host&amp;autostart=2 | ?mode=join&amp;code=ABC123,
    /// com &amp;spec=, &amp;name=, &amp;log=1 (estado no console a cada 2 s), &amp;bench=N (só solo: medição com N inimigos)
    /// e &amp;evolve=N com log=1 (solo/anfitrião: todos começam com a arma evoluída no nível N, para testes).
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public ContentDatabaseAsset content;
        public GameAssets assets;
        public UiAssets ui;

        public UiContext Context { get; private set; }
        public UiRouter Router { get; private set; }
        public MatchView Match { get; private set; }
        public NetGame Net { get; private set; }
        public string FatalError { get; private set; }

        int _autostart, _bench, _evolve;
        bool _log;
        float _logAt, _benchAt, _benchChoiceAt;

        void Start()
        {
            var settings = new SettingsStore();
            settings.Load();
            Strings.Load(assets.strings != null ? assets.strings.text : "{}");

            var built = content.Build();
            if (built.Errors.Length > 0)
            {
                FatalError = built.Errors[0];
                Debug.LogError("[ES] Conteúdo inválido: " + string.Join("; ", built.Errors));
                return;
            }
            var db = built.Db;

            Net = NetGame.Create(db, Application.version);
            var sfx = SfxPlayer.Create(assets);
            sfx.SetVolumes(settings.Master, settings.Music, settings.Sfx);
            var input = new GameInput(settings.SpecialBinding);

            var cam = Camera.main;
            if (cam == null) cam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" }.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x14, 0x16, 0x1b, 0xff);
            PixelPerfectSetup.Apply(cam);

            Match = gameObject.AddComponent<MatchView>();
            Match.Init(assets, Net, settings, input, cam);
            var perf = gameObject.AddComponent<PerfOverlay>();
            perf.Match = Match;

            Context = new UiContext { Net = Net, Db = db, Assets = assets, Ui = ui, Settings = settings, Input = input, Match = Match, Perf = perf };
            Router = UiRouter.Create(Context, gameObject);
            HandleUrl(db);
        }

        void HandleUrl(GameDatabase db)
        {
            var q = ParseQuery(Application.absoluteURL);
            if (!q.TryGetValue("mode", out var mode)) return;
            string spec = q.TryGetValue("spec", out var s) && db.SpecializationIndex(s) >= 0 ? s : db.Specialization(0).Def.id;
            string name = q.TryGetValue("name", out var n) ? n : Context.PlayerName;
            _log = q.TryGetValue("log", out var l) && l == "1";
            if (q.TryGetValue("bench", out var b)) int.TryParse(b, out _bench);
            if (_log && q.TryGetValue("evolve", out var ev)) int.TryParse(ev, out _evolve);
            if (mode == "setup") { Router.ShowMain(Router.Solo); return; }
            if (mode == "solo") Net.StartSolo(name, spec, Difficulty.Normal, (ulong)DateTime.UtcNow.Ticks);
            else if (mode == "host")
            {
                int.TryParse(q.TryGetValue("autostart", out var a) ? a : "0", out _autostart);
                CreateRoom(name, spec);
            }
            else if (mode == "join" && q.TryGetValue("code", out var code)) JoinRoom(code, name, spec);
        }

        async void CreateRoom(string name, string spec)
        {
            if (await Net.CreateRoom(name, spec)) Debug.Log("[ES] CODE=" + Net.RoomCode);
        }

        async void JoinRoom(string code, string name, string spec)
        {
            if (await Net.JoinRoom(code, name, spec)) Debug.Log("[ES] JOINED " + code);
        }

        void Update()
        {
            if (Net == null) return;
            if (_autostart > 0 && Net.Mode == NetMode.Host && !Net.Started && Net.Lobby != null && Net.Lobby.Players.Count >= _autostart)
            {
                _autostart = 0;
                Net.StartMatch((ulong)DateTime.UtcNow.Ticks);
                Debug.Log("[ES] START players=" + Net.Lobby.Players.Count);
            }
            if (_bench > 0 && Net.Mode == NetMode.Solo && Net.Started) Bench();
            if (_evolve > 0 && Net.Started && Net.Host?.World != null)
            {
                DebugEvolve.All(Net.Host.World, _evolve);
                Debug.Log("[ES] EVOLVED level=" + _evolve);
                _evolve = 0;
            }
            if (_log && Net.Started && Time.unscaledTime >= _logAt)
            {
                _logAt = Time.unscaledTime + 2f;
                LogState();
            }
        }

        /// <summary>Medição de desempenho (?mode=solo&amp;bench=N): mantém N inimigos, o jogador invulnerável e escolhe as cartas sozinho.</summary>
        void Bench()
        {
            var w = Net.Host.World;
            if (w == null || w.Players[0] == null) return;
            w.Players[0].InvulnUntil = float.MaxValue;
            // a medição não para nas cartas de nível: escolhe a primeira
            var offer = Net.Feed?.CurrentOffer;
            if (offer != null && Time.unscaledTime >= _benchChoiceAt)
            {
                _benchChoiceAt = Time.unscaledTime + 0.2f;
                Net.Choose(offer.OfferId, 0);
            }
            int missing = _bench - w.Enemies.Count;
            if (missing > 0 && Time.unscaledTime >= _benchAt)
            {
                _benchAt = Time.unscaledTime + 0.25f;
                Match.DebugSpawn(Mathf.Min(50, missing));
            }
        }

        void LogState()
        {
            var f = Net.Feed;
            if (f == null) return;
            var sb = new StringBuilder();
            sb.Append(FormattableString.Invariant($"STATE mode={Net.Mode} slot={Net.LocalSlot} tick={f.Tick} t={f.Elapsed:0.0} enemies={f.Tracks.EnemyCount} allies={f.Tracks.AllyCount} phase={f.Phase} fps={Context.Perf.Fps:0}"));
            for (int s = 0; s < PlayerStore.MaxSlots; s++)
                if (f.Players[s].Present) sb.Append(FormattableString.Invariant($" p{s}=({f.Players[s].Pos.X:0.0};{f.Players[s].Pos.Y:0.0}) hp={f.Players[s].Hp:0} lv={f.Players[s].Level}"));
            Debug.Log("[ES] " + sb);
        }

        public static Dictionary<string, string> ParseQuery(string url)
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
