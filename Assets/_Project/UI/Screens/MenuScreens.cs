using System;
using System.Collections.Generic;
using ES.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES.UI
{
    public sealed class MainMenuController : UiScreen
    {
        public override string Uxml => "MainMenu";
        public override string[] Buttons => new[] { "btn-solo", "btn-coop", "btn-settings", "btn-credits" };

        protected override void OnCreate()
        {
            Q<Label>("label-version").text = Strings.Format("versao", Application.version);
            Bind("btn-solo", () => Ctx.Router.ShowMain(Ctx.Router.Solo));
            Bind("btn-coop", () => Ctx.Router.ShowMain(Ctx.Router.Coop));
            Bind("btn-settings", () => Ctx.Router.OpenSettings(this));
            Bind("btn-credits", () => Ctx.Router.ShowMain(Ctx.Router.Credits));
        }
    }

    /// <summary>Cartões de especialização (retrato, arma inicial, especial, afinidades).</summary>
    public static class SpecCards
    {
        public static Dictionary<string, Button> Build(UiContext ctx, VisualElement list, Action<string> pick, bool compact)
        {
            list.Clear();
            var map = new Dictionary<string, Button>();
            for (int i = 0; i < ctx.Db.SpecializationCount; i++)
            {
                var spec = ctx.Db.Specialization(i);
                string id = spec.Def.id;
                var b = new Button(() => { ctx.Click(); pick(id); }) { name = "spec-" + id };
                b.AddToClassList("btn");
                if (!compact) b.AddToClassList("spec-card");
                var portrait = new VisualElement();
                portrait.AddToClassList("portrait");
                if (compact) { portrait.style.width = 64; portrait.style.height = 64; portrait.style.marginRight = 8; }
                UiText.Image(portrait, ctx.Assets.Sprite($"portrait_{id}_determinacao"));
                b.Add(portrait);
                var col = new VisualElement();
                col.style.flexShrink = 1;
                col.style.flexGrow = 1;
                UiText.Line(col, spec.Def.name, "spec-name");
                if (!compact)
                {
                    var weapon = ctx.Db.Ability(spec.StartingAbility).Def;
                    var special = ctx.Db.Special(spec.Special).Def;
                    UiText.Line(col, Strings.Format("arma_inicial", weapon.name) + " — " + weapon.description);
                    UiText.Line(col, Strings.Format("especial", ctx.Input.SpecialDisplayName(), special.name) + " — " + special.description);
                    UiText.Line(col, Strings.Format("afinidades", LevelUpCards.TagList(spec.Def.affinityTags)), "small");
                }
                b.Add(col);
                if (compact) { b.style.flexDirection = FlexDirection.Row; b.style.alignItems = Align.Center; }
                list.Add(b);
                map[id] = b;
            }
            return map;
        }

        public static void Select(Dictionary<string, Button> map, string id)
        {
            foreach (var kv in map) kv.Value.EnableInClassList("selected", kv.Key == id);
        }
    }

    public sealed class SoloSetupController : UiScreen
    {
        public override string Uxml => "SoloSetup";
        public override string[] Buttons => new[] { "btn-diff-easy", "btn-diff-normal", "btn-diff-hard", "btn-solo-start", "btn-solo-back" };

        Dictionary<string, Button> _specs;
        string _spec;
        Difficulty _difficulty = Difficulty.Normal;

        public string SelectedSpec => _spec;
        public Difficulty SelectedDifficulty => _difficulty;

        protected override void OnCreate()
        {
            Bind("btn-diff-easy", () => SetDifficulty(Difficulty.Easy));
            Bind("btn-diff-normal", () => SetDifficulty(Difficulty.Normal));
            Bind("btn-diff-hard", () => SetDifficulty(Difficulty.Hard));
            Bind("btn-solo-back", () => Ctx.Router.ShowMain(Ctx.Router.Menu));
            Bind("btn-solo-start", Start);
        }

        protected override void OnShow()
        {
            _specs = SpecCards.Build(Ctx, Q<VisualElement>("list-specs"), Pick, false);
            Pick(_spec ?? Ctx.Db.Specialization(0).Def.id);
            SetDifficulty(_difficulty);
        }

        public void Pick(string id)
        {
            _spec = id;
            SpecCards.Select(_specs, id);
        }

        public void SetDifficulty(Difficulty d)
        {
            _difficulty = d;
            string key = d == Difficulty.Easy ? "easy" : d == Difficulty.Hard ? "hard" : "normal";
            foreach (var k in new[] { "easy", "normal", "hard" }) Q<Button>("btn-diff-" + k).EnableInClassList("selected", k == key);
            Q<Label>("label-diff-desc").text = Strings.Get("dificuldade_desc_" + key);
        }

        public void Start()
        {
            Ctx.Net.StartSolo(Ctx.PlayerName, _spec, _difficulty, (ulong)DateTime.UtcNow.Ticks);
        }
    }

    public sealed class CoopController : UiScreen
    {
        public override string Uxml => "Coop";
        public override string[] Buttons => new[] { "btn-create", "btn-join", "btn-coop-back" };

        TextField _name, _code;
        Label _status;
        bool _busy;

        protected override void OnCreate()
        {
            _name = Q<TextField>("field-name");
            _code = Q<TextField>("field-code");
            _status = Q<Label>("label-coop-status");
            Bind("btn-create", Create);
            Bind("btn-join", Join);
            Bind("btn-coop-back", () => Ctx.Router.ShowMain(Ctx.Router.Menu));
        }

        protected override void OnShow()
        {
            _name.value = string.IsNullOrWhiteSpace(Ctx.Settings.PlayerName) ? Strings.Get("nome_padrao") : Ctx.Settings.PlayerName;
            _status.text = "";
            _busy = false;
        }

        bool SaveName()
        {
            string n = (_name.value ?? "").Trim();
            if (n.Length == 0) { _status.text = Strings.Get("erro_nome_vazio"); return false; }
            Ctx.Settings.PlayerName = n;
            Ctx.Settings.Save();
            return true;
        }

        string DefaultSpec => Ctx.Db.Specialization(0).Def.id;

        async void Create()
        {
            if (_busy || !SaveName()) return;
            _busy = true;
            _status.text = Strings.Get("criando_sala");
            bool ok = await Ctx.Net.CreateRoom(Ctx.PlayerName, DefaultSpec);
            _busy = false;
            if (!ok) _status.text = "";
        }

        async void Join()
        {
            if (_busy || !SaveName()) return;
            if (!JoinCode.TryNormalize(_code.value, out _)) { Ctx.Router.ShowMessage(Strings.Get("erro_codigo_invalido")); return; }
            _busy = true;
            _status.text = Strings.Get("entrando_sala");
            bool ok = await Ctx.Net.JoinRoom(_code.value, Ctx.PlayerName, DefaultSpec);
            _busy = false;
            if (!ok) _status.text = "";
        }
    }

    public sealed class LobbyController : UiScreen
    {
        public override string Uxml => "Lobby";
        public override string[] Buttons => new[] { "btn-copy", "btn-lobby-easy", "btn-lobby-normal", "btn-lobby-hard", "btn-leave", "btn-start" };

        Dictionary<string, Button> _specs;
        string _signature;
        float _copiedFor;

        protected override void OnCreate()
        {
            Bind("btn-copy", () => { ES.Game.Clipboard.Copy(Ctx.Net.RoomCode); _copiedFor = 2f; });
            Bind("btn-lobby-easy", () => Ctx.Net.SetDifficulty(Difficulty.Easy));
            Bind("btn-lobby-normal", () => Ctx.Net.SetDifficulty(Difficulty.Normal));
            Bind("btn-lobby-hard", () => Ctx.Net.SetDifficulty(Difficulty.Hard));
            Bind("btn-leave", () => Ctx.Router.Leave());
            Bind("btn-start", () => Ctx.Net.StartMatch((ulong)DateTime.UtcNow.Ticks));
        }

        protected override void OnShow()
        {
            _specs = SpecCards.Build(Ctx, Q<VisualElement>("list-lobby-specs"), id => Ctx.Net.PickSpecialization(id), true);
            _signature = null;
        }

        public override void Tick(float dt)
        {
            var net = Ctx.Net;
            var lobby = net.Lobby;
            if (lobby == null) return;
            bool host = net.Mode == ES.Net.NetMode.Host;
            Q<Label>("label-code").text = Strings.Format("lobby_codigo", net.RoomCode ?? "...");
            _copiedFor -= dt;
            Q<Label>("label-copied").text = _copiedFor > 0 ? Strings.Get("copiado") : "";
            Q<Label>("label-count").text = Strings.Format("lobby_jogadores", lobby.Players.Count);
            Q<Button>("btn-start").style.display = host ? DisplayStyle.Flex : DisplayStyle.None;
            Q<Label>("label-lobby-wait").style.display = host ? DisplayStyle.None : DisplayStyle.Flex;
            Q<VisualElement>("row-difficulty").SetEnabled(host);
            string diff = lobby.Difficulty == Difficulty.Easy ? "easy" : lobby.Difficulty == Difficulty.Hard ? "hard" : "normal";
            foreach (var k in new[] { "easy", "normal", "hard" }) Q<Button>("btn-lobby-" + k).EnableInClassList("selected", k == diff);
            Q<Label>("label-lobby-diff").text = Strings.Get("dificuldade_desc_" + diff) + (host ? "" : "\n" + Strings.Get("lobby_dificuldade_host"));

            int local = net.LocalSlot;
            string mine = null;
            var sig = new System.Text.StringBuilder();
            foreach (var p in lobby.Players)
            {
                sig.Append(p.Slot).Append(p.Name).Append(p.SpecializationId).Append('|');
                if (p.Slot == local) mine = p.SpecializationId;
            }
            if (mine != null) SpecCards.Select(_specs, mine);
            if (sig.ToString() == _signature) return;
            _signature = sig.ToString();
            var list = Q<VisualElement>("list-players");
            list.Clear();
            foreach (var p in lobby.Players)
            {
                string tag = (p.Slot == 0 ? " " + Strings.Get("lobby_anfitriao") : "") + (p.Slot == local ? " " + Strings.Get("lobby_voce") : "");
                var l = UiText.Line(list, $"{p.Name}{tag} — {Ctx.SpecName(p.SpecializationId)}");
                if (p.Slot >= 0 && p.Slot < ES.Game.Layers.PlayerColors.Length) l.style.color = ES.Game.Layers.PlayerColors[p.Slot];
            }
        }
    }

    public sealed class CreditsController : UiScreen
    {
        public override string Uxml => "Credits";
        public override string[] Buttons => new[] { "btn-credits-back" };
        protected override void OnCreate() => Bind("btn-credits-back", () => Ctx.Router.ShowMain(Ctx.Router.Menu));
    }

    public sealed class MessageController : UiScreen
    {
        public override string Uxml => "Message";
        public override string[] Buttons => new[] { "btn-message-ok" };
        Action _onOk;

        protected override void OnCreate() => Bind("btn-message-ok", () => { Hide(); var a = _onOk; _onOk = null; a?.Invoke(); });

        public void Show(string text, Action onOk)
        {
            Q<Label>("label-message").text = text;
            _onOk = onOk;
            Show();
        }
    }
}
