using System.Collections.Generic;
using ES.Net;
using ES.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES.UI
{
    /// <summary>
    /// Mostra a tela certa para o estado do NetGame: menus → sala → HUD (+ subida de nível,
    /// pausa) → resultado; trata Esc, F3, erros e conexão perdida.
    /// </summary>
    public sealed class UiRouter : MonoBehaviour
    {
        UiContext _ctx;
        UIDocument _doc;
        PanelSettings _panel;
        readonly List<UiScreen> _all = new List<UiScreen>();
        readonly List<UiScreen> _main = new List<UiScreen>();

        public MainMenuController Menu { get; private set; }
        public SoloSetupController Solo { get; private set; }
        public CoopController Coop { get; private set; }
        public LobbyController Lobby { get; private set; }
        public CreditsController Credits { get; private set; }
        public HudController Hud { get; private set; }
        public ResultController Result { get; private set; }
        public LevelUpController LevelUp { get; private set; }
        public PauseController Pause { get; private set; }
        public SettingsController Settings { get; private set; }
        public MessageController Message { get; private set; }
        public VisualElement RootElement => _doc != null ? _doc.rootVisualElement : null;
        public IReadOnlyList<UiScreen> Screens => _all;

        MatchFeed _lastFeed;
        int _lastSlot;
        float _endTimer;
        bool _resultShown;

        public static UiRouter Create(UiContext ctx, GameObject host)
        {
            var r = host.AddComponent<UiRouter>();
            r._ctx = ctx;
            ctx.Router = r;
            r._panel = Instantiate(ctx.Ui.panel);
            r._doc = host.AddComponent<UIDocument>();
            r._doc.panelSettings = r._panel;
            r.Build();
            return r;
        }

        void Build()
        {
            var root = _doc.rootVisualElement;
            root.AddToClassList("root");
            if (_ctx.Ui.theme != null) root.styleSheets.Add(_ctx.Ui.theme);
            if (_ctx.Assets.font != null) root.style.unityFontDefinition = FontDefinition.FromFont(_ctx.Assets.font);
            root.style.flexGrow = 1;

            Menu = Add(new MainMenuController(), true);
            Solo = Add(new SoloSetupController(), true);
            Coop = Add(new CoopController(), true);
            Lobby = Add(new LobbyController(), true);
            Credits = Add(new CreditsController(), true);
            Hud = Add(new HudController(), true);
            Result = Add(new ResultController(), true);
            LevelUp = Add(new LevelUpController(), false);
            Pause = Add(new PauseController(), false);
            Settings = Add(new SettingsController(), false);
            Message = Add(new MessageController(), false);

            _ctx.Net.Error += key => ShowMessage(Strings.Get(key));
            _ctx.Net.Disconnected += OnDisconnected;
            _ctx.Settings.Changed += ApplySettings;
            ApplySettings();
            ShowMain(Menu);
        }

        T Add<T>(T screen, bool main) where T : UiScreen
        {
            screen.Create(_ctx, _doc.rootVisualElement);
            _all.Add(screen);
            if (main) _main.Add(screen);
            return screen;
        }

        public void ShowMain(UiScreen screen)
        {
            foreach (var s in _all) if (s != screen && s != Message) s.Hide();
            screen.Show();
            if (Message.Visible) Message.Root.BringToFront();
        }

        public void ShowMessage(string text, System.Action onOk = null) => Message.Show(text, onOk);

        public void OpenSettings(UiScreen from)
        {
            if (from == Menu) from.Hide();
            Settings.Open(from);
        }

        public void OpenPause()
        {
            if (!Hud.Visible || Result.Visible) return;
            if (_ctx.Net.Mode == NetMode.Solo) _ctx.Net.Paused = true;
            Pause.Show();
        }

        public void ClosePause()
        {
            Pause.Hide();
            _ctx.Net.Paused = false;
        }

        public async void Leave()
        {
            _ctx.Net.Paused = false;
            await _ctx.Net.LeaveAsync();
            _lastFeed = null;
            ShowMain(Menu);
        }

        void OnDisconnected(string key)
        {
            ShowResult(true);
            ShowMessage(Strings.Get(key));
        }

        void ShowResult(bool disconnected)
        {
            _resultShown = true;
            Result.Fill(_lastFeed?.Summary, _lastFeed, _lastSlot, disconnected);
            ShowMain(Result);
        }

        void ApplySettings()
        {
            var s = _ctx.Settings;
            SfxPlayerVolumes();
            _ctx.Input.SetSpecialBinding(s.SpecialBinding);
            _panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panel.referenceResolution = new Vector2Int(Mathf.RoundToInt(1280 / s.UiScale), Mathf.RoundToInt(720 / s.UiScale));
            _panel.match = 0.5f;
        }

        void SfxPlayerVolumes()
        {
            var s = _ctx.Settings;
            ES.Game.SfxPlayer.Instance?.SetVolumes(s.Master, s.Music, s.Sfx);
        }

        bool TextFieldFocused => _doc.rootVisualElement?.focusController?.focusedElement is TextField
            || _doc.rootVisualElement?.focusController?.focusedElement is TextElement te && te.parent is TextField;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var net = _ctx.Net;
            var input = _ctx.Input;

            if (input.Perf.WasPressedThisFrame()) _ctx.Perf.Visible = !_ctx.Perf.Visible;

            if (net.Mode != NetMode.None && !net.Started)
            {
                if (!Lobby.Visible && net.Mode != NetMode.Solo && net.Lobby != null && (net.Mode == NetMode.Host ? net.RoomCode != null : net.LocalSlot >= 0))
                    ShowMain(Lobby);
            }
            else if (net.Started && net.Feed != null)
            {
                _lastFeed = net.Feed;
                _lastSlot = net.LocalSlot;
                if (!Hud.Visible && !Result.Visible) { _resultShown = false; _endTimer = 0; ShowMain(Hud); }
                if (net.Feed.Phase != MatchPhase.Running)
                {
                    _endTimer += dt;
                    if (!_resultShown && _endTimer > 1.8f && (net.Feed.Summary != null || _endTimer > 4f))
                    {
                        Pause.Hide();
                        Settings.Hide();
                        LevelUp.Hide();
                        ShowResult(false);
                    }
                }
                else
                {
                    bool offer = net.Feed.CurrentOffer != null && !Pause.Visible && !Settings.Visible && Hud.Visible;
                    if (offer && !LevelUp.Visible) LevelUp.Show();
                    else if (!offer && LevelUp.Visible) LevelUp.Hide();
                }
            }

            if (input.Pause.WasPressedThisFrame() && !input.Rebinding)
            {
                if (Message.Visible) Message.Hide();
                else if (Settings.Visible) Settings.Back();
                else if (Pause.Visible) ClosePause();
                else if (Hud.Visible) OpenPause();
                else if (Solo.Visible || Coop.Visible || Credits.Visible) ShowMain(Menu);
            }

            if (_ctx.Match != null)
                _ctx.Match.InputBlocked = Pause.Visible || Settings.Visible || Message.Visible || TextFieldFocused || input.Rebinding;

            foreach (var s in _all) if (s.Visible) s.Tick(dt);
        }
    }
}
