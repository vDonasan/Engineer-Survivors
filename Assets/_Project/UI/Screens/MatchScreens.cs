using System.Collections.Generic;
using System.Text;
using ES.Game;
using ES.Net;
using ES.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES.UI
{
    public sealed class HudController : UiScreen
    {
        public override string Uxml => "Hud";
        public override string[] Buttons => new[] { "btn-hud-menu" };

        VisualElement _hpFill, _xpFill, _portrait, _special, _abilities, _evolutions, _allies;
        Label _hp, _level, _time, _name, _synergy, _banner, _warning, _specialKey;
        RadialCooldown _radial;
        string _loadoutSig, _portraitId, _alliesSig;
        float _now, _hurtAt = -9, _healAt = -9, _evolutionAt = -9, _bannerUntil;
        float _lastHp = -1;
        readonly List<(VisualElement fill, Label label, int slot)> _allyRows = new List<(VisualElement, Label, int)>();

        protected override void OnCreate()
        {
            _hpFill = Q<VisualElement>("bar-hp-fill");
            _xpFill = Q<VisualElement>("bar-xp-fill");
            _portrait = Q<VisualElement>("img-portrait");
            _special = Q<VisualElement>("special");
            _special.style.overflow = Overflow.Hidden;
            _radial = new RadialCooldown();
            _special.Add(_radial);
            _abilities = Q<VisualElement>("list-abilities");
            _evolutions = Q<VisualElement>("list-evolutions");
            _allies = Q<VisualElement>("list-allies");
            _hp = Q<Label>("label-hp");
            _level = Q<Label>("label-level");
            _time = Q<Label>("label-time");
            _name = Q<Label>("label-name");
            _synergy = Q<Label>("label-synergy");
            _banner = Q<Label>("label-banner");
            _warning = Q<Label>("label-warning");
            _specialKey = Q<Label>("label-special-key");
            Bind("btn-hud-menu", () => Ctx.Router.OpenPause());
            Ctx.Match.LocalHit += _ => _hurtAt = _now;
            Ctx.Match.Surge += () => Banner(Strings.Get("hud_surto"), 3f);
            Ctx.Match.RuleEvent += OnRule;
        }

        protected override void OnShow()
        {
            _loadoutSig = _portraitId = _alliesSig = null;
            _lastHp = -1;
            _banner.text = "";
        }

        void OnRule(DecodedRuleEvent d)
        {
            var e = d.Event;
            int local = Ctx.Net.LocalSlot;
            if (e.Kind == RuleEventKind.EvolutionApplied && e.A == local)
            {
                _evolutionAt = _now;
                if (e.B >= 0 && e.B < Ctx.Db.EvolutionCount) Banner(Strings.Format("carta_dica_evolucao", Ctx.Db.Evolution(e.B).Def.name), 3f);
            }
            if (e.Kind == RuleEventKind.SynergyChanged && e.A == local && e.C != 0 && e.B >= 0 && e.B < Ctx.Db.SynergyCount)
                Banner(Strings.Format("hud_sinergia_ativa", Ctx.Db.Synergy(e.B).Def.name), 2.5f);
        }

        void Banner(string text, float seconds)
        {
            _banner.text = text;
            _bannerUntil = _now + seconds;
        }

        public string PortraitId => _portraitId;

        public override void Tick(float dt)
        {
            _now += dt;
            var net = Ctx.Net;
            var feed = net.Feed;
            if (feed == null) return;
            int local = net.LocalSlot;
            if (local < 0 || local >= PlayerStore.MaxSlots) return;
            var me = feed.Players[local];
            var cfg = net.Config;
            string spec = cfg != null && local < cfg.Roster.Length ? cfg.Roster[local].SpecializationId : "eletrica";
            _name.text = cfg != null && local < cfg.Roster.Length ? cfg.Roster[local].Name : "";

            float hpFrac = me.MaxHp > 0 ? Mathf.Clamp01(me.Hp / me.MaxHp) : 0f;
            if (_lastHp >= 0 && me.Hp > _lastHp + 1f) _healAt = _now;
            _lastHp = me.Hp;
            _hpFill.style.width = Length.Percent(hpFrac * 100f);
            _hp.text = $"{Mathf.CeilToInt(me.Hp)} / {Mathf.CeilToInt(me.MaxHp)}";
            _xpFill.style.width = Length.Percent(Mathf.Clamp01(me.XpProgress) * 100f);
            _level.text = Strings.Format("hud_nivel", me.Level);
            _time.text = UiText.Clock(feed.Elapsed);
            if (_now > _bannerUntil) _banner.text = "";

            string warning = feed.HostBackgrounded ? Strings.Get("aviso_host_minimizou") : me.Status == PlayerStatus.Downed ? Strings.Get("hud_caido") : "";
            _warning.text = warning;

            // especial
            int specIndex = Ctx.Db.SpecializationIndex(spec);
            if (specIndex >= 0)
            {
                var special = Ctx.Db.Special(Ctx.Db.Specialization(specIndex).Special);
                UiText.Image(Q<VisualElement>("img-special"), Ctx.Assets.Sprite("icon_" + special.Def.id));
                _radial.Fraction = special.Def.cooldown > 0 ? me.SpecialCooldown / special.Def.cooldown : 0f;
                _specialKey.text = Ctx.Input.SpecialDisplayName();
            }

            bool allyDowned = false;
            for (int s = 0; s < PlayerStore.MaxSlots; s++)
                if (s != local && feed.Players[s].Present && feed.Players[s].Status == PlayerStatus.Downed) allyDowned = true;
            string mood = PortraitMood.Pick(hpFrac, me.Status, feed.Phase, _now - _hurtAt, _now - _healAt, _now - _evolutionAt, allyDowned);
            string portrait = $"portrait_{spec}_{mood}";
            if (portrait != _portraitId)
            {
                _portraitId = portrait;
                UiText.Image(_portrait, Ctx.Assets.Sprite(portrait));
            }

            RefreshLoadout(feed, local);
            RefreshAllies(feed, local, cfg);
        }

        void RefreshLoadout(MatchFeed feed, int local)
        {
            var l = feed.Loadouts?.Slots[local];
            if (l == null) return;
            var sig = new StringBuilder();
            foreach (var a in l.Abilities) sig.Append(a.Ability).Append(':').Append(a.Level).Append(',');
            foreach (var s in l.ActiveSynergies) sig.Append('s').Append(s);
            foreach (var d in feed.Loadouts.Discovered) sig.Append('d').Append(d);
            if (sig.ToString() == _loadoutSig) return;
            _loadoutSig = sig.ToString();

            _abilities.Clear();
            foreach (var a in l.Abilities)
            {
                var def = Ctx.Db.Ability(a.Ability).Def;
                var box = new VisualElement();
                box.AddToClassList("ability");
                box.tooltip = def.name;
                var icon = new VisualElement();
                icon.AddToClassList("ability-icon");
                UiText.Image(icon, Ctx.Assets.Sprite("icon_" + def.id));
                box.Add(icon);
                var lv = new Label(a.Level.ToString());
                lv.AddToClassList("ability-level");
                box.Add(lv);
                _abilities.Add(box);
            }

            var names = new List<string>();
            foreach (var s in l.ActiveSynergies) if (s >= 0 && s < Ctx.Db.SynergyCount) names.Add(Ctx.Db.Synergy(s).Def.name);
            _synergy.text = names.Count > 0 ? Strings.Format("hud_sinergia_ativa", string.Join(", ", names)) : "";

            _evolutions.Clear();
            for (int i = 0; i < Ctx.Db.EvolutionCount; i++)
            {
                var e = Ctx.Db.Evolution(i);
                if (l.Evolved.Contains(i) || l.LevelOf(e.From) <= 0) continue;
                var p = feed.Loadouts.Progress(local, i);
                bool hidden = e.Discoverable && !feed.Loadouts.Discovered.Contains(i);
                string name = hidden ? Strings.Get("hud_evolucao_oculta") : e.Def.name;
                var line = UiText.Line(_evolutions, $"{name}  {p.Met}/{p.Total}", "small");
                if (p.Done) line.AddToClassList("ok");
            }
        }

        void RefreshAllies(MatchFeed feed, int local, MatchConfig cfg)
        {
            var sig = new StringBuilder();
            for (int s = 0; s < PlayerStore.MaxSlots; s++) if (s != local && feed.Players[s].Present) sig.Append(s);
            if (sig.ToString() != _alliesSig)
            {
                _alliesSig = sig.ToString();
                _allies.Clear();
                _allyRows.Clear();
                for (int s = 0; s < PlayerStore.MaxSlots; s++)
                {
                    if (s == local || !feed.Players[s].Present) continue;
                    var row = new VisualElement();
                    row.AddToClassList("ally");
                    var label = UiText.Line(row, cfg != null && s < cfg.Roster.Length ? cfg.Roster[s].Name : "?", "small");
                    label.style.color = Layers.PlayerColors[s];
                    var bar = new VisualElement();
                    bar.AddToClassList("bar");
                    var fill = new VisualElement();
                    fill.AddToClassList("bar-fill");
                    fill.AddToClassList("ally-fill");
                    bar.Add(fill);
                    row.Add(bar);
                    _allies.Add(row);
                    _allyRows.Add((fill, label, s));
                }
            }
            foreach (var (fill, label, s) in _allyRows)
            {
                var p = feed.Players[s];
                fill.style.width = Length.Percent(p.MaxHp > 0 ? Mathf.Clamp01(p.Hp / p.MaxHp) * 100f : 0f);
                fill.style.backgroundColor = p.Status == PlayerStatus.Active ? new Color(0.3f, 1f, 0.53f) : new Color(0.49f, 0.53f, 0.58f);
            }
        }
    }

    public sealed class LevelUpController : UiScreen
    {
        public override string Uxml => "LevelUp";
        public override string[] Buttons => new[] { "card-0", "card-1", "card-2" };

        short _offerId = -1;
        float _shownAt, _now;
        bool _chosen;
        readonly Button[] _cards = new Button[3];

        public CardInfo[] Cards { get; } = new CardInfo[3];

        protected override void OnCreate()
        {
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                _cards[i] = Q<Button>("card-" + i);
                Bind("card-" + i, () => Choose(k));
            }
        }

        public void Choose(int index)
        {
            var offer = Ctx.Net.Feed?.CurrentOffer;
            if (offer == null || _chosen || index < 0 || index >= offer.Options.Length) return;
            _chosen = true;
            Ctx.Net.Choose(offer.OfferId, index);
            for (int i = 0; i < 3; i++) _cards[i].SetEnabled(i == index);
        }

        public override void Tick(float dt)
        {
            _now += dt;
            var feed = Ctx.Net.Feed;
            var offer = feed?.CurrentOffer;
            if (offer == null) return;
            if (offer.OfferId != _offerId) Fill(feed, offer);

            if (Ctx.Input.Choice1.WasPressedThisFrame()) Choose(0);
            if (Ctx.Input.Choice2.WasPressedThisFrame()) Choose(1);
            if (Ctx.Input.Choice3.WasPressedThisFrame()) Choose(2);

            bool coop = Ctx.Net.Mode != NetMode.Solo;
            Q<VisualElement>("row-choice-timer").style.display = coop ? DisplayStyle.Flex : DisplayStyle.None;
            if (coop)
            {
                float total = Ctx.Db.Def.rules.coopChoiceSeconds;
                float left = Mathf.Max(0, total - (_now - _shownAt));
                Q<VisualElement>("bar-choice-fill").style.width = Length.Percent(total > 0 ? left / total * 100f : 0f);
                Q<Label>("label-choice-timer").text = Strings.Format("nivel_tempo", left);
            }
        }

        void Fill(MatchFeed feed, LevelUpOffer offer)
        {
            _offerId = offer.OfferId;
            _shownAt = _now;
            _chosen = false;
            int local = Ctx.Net.LocalSlot;
            for (int i = 0; i < 3; i++)
            {
                var card = _cards[i];
                bool has = i < offer.Options.Length;
                card.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
                card.SetEnabled(has);
                if (!has) { Cards[i] = default; continue; }
                var info = LevelUpCards.Describe(Ctx.Db, feed.Loadouts, local, offer.Options[i]);
                Cards[i] = info;
                card.Q<Label>("card-kind").text = $"[{i + 1}] {info.Kind}";
                card.Q<Label>("card-name").text = info.Name;
                var rarity = card.Q<Label>("card-rarity");
                rarity.text = info.Rarity;
                rarity.RemoveFromClassList("rarity-comum");
                rarity.RemoveFromClassList("rarity-incomum");
                rarity.RemoveFromClassList("rarity-raro");
                rarity.AddToClassList(info.RarityClass);
                card.Q<Label>("card-desc").text = info.Description;
                card.Q<Label>("card-change").text = info.Change;
                card.Q<Label>("card-tags").text = info.Tags;
                card.Q<Label>("card-hint").text = info.Hint;
                UiText.Image(card.Q<VisualElement>("card-icon"), Ctx.Assets.Sprite(info.IconId));
            }
        }

        protected override void OnHide() => _offerId = -1;
    }

    public sealed class PauseController : UiScreen
    {
        public override string Uxml => "Pause";
        public override string[] Buttons => new[] { "btn-resume", "btn-pause-settings", "btn-quit" };

        protected override void OnCreate()
        {
            Bind("btn-resume", () => Ctx.Router.ClosePause());
            Bind("btn-pause-settings", () => Ctx.Router.OpenSettings(this));
            Bind("btn-quit", () => Ctx.Router.Leave());
        }

        protected override void OnShow()
        {
            Q<Label>("label-pause-title").text = Strings.Get(Ctx.Net.Mode == NetMode.Solo ? "pausa_titulo" : "menu_coop_titulo");
        }
    }

    public sealed class ResultController : UiScreen
    {
        public override string Uxml => "Result";
        public override string[] Buttons => new[] { "btn-result-menu" };

        protected override void OnCreate() => Bind("btn-result-menu", () => Ctx.Router.Leave());

        /// <summary>Preenche com o resumo do host; sem resumo (conexão perdida), usa o que o cliente sabe.</summary>
        public void Fill(MatchSummary summary, MatchFeed feed, int local, bool disconnected)
        {
            var phase = summary?.Phase ?? feed?.Phase ?? MatchPhase.Running;
            string title = disconnected && summary == null ? "resultado_desconectado" : phase == MatchPhase.Victory ? "resultado_vitoria" : "resultado_derrota";
            Q<Label>("label-result-title").text = Strings.Get(title);
            float elapsed = summary?.Elapsed ?? feed?.Elapsed ?? 0f;
            int s = Mathf.FloorToInt(elapsed);
            Q<Label>("label-result-time").text = Strings.Format("resultado_tempo", s / 60, s % 60);
            Q<Label>("label-result-kills").text = Strings.Format("resultado_abates", summary?.TeamKills ?? feed?.TeamKills ?? 0);
            var mine = summary != null && local >= 0 && local < summary.Players.Length ? summary.Players[local] : null;
            Q<Label>("label-result-mykills").text = mine != null ? Strings.Format("resultado_seus_abates", mine.Kills) : "";
            int level = mine?.Level ?? (feed != null && local >= 0 && local < PlayerStore.MaxSlots ? feed.Players[local].Level : 1);
            Q<Label>("label-result-level").text = Strings.Format("resultado_nivel", level);
            var list = Q<VisualElement>("list-damage");
            list.Clear();
            if (mine == null || mine.Damage.Count == 0) { UiText.Line(list, Strings.Get("resultado_sem_dano"), "small"); return; }
            foreach (var d in mine.Damage)
            {
                string name = d.Ability < 0 ? Strings.Get("resultado_especial") : d.Ability < Ctx.Db.AbilityCount ? Ctx.Db.Ability(d.Ability).Def.name : "?";
                UiText.Line(list, $"{name}: {Mathf.RoundToInt(d.Amount)}");
            }
        }
    }

    public sealed class SettingsController : UiScreen
    {
        public override string Uxml => "Settings";
        public override string[] Buttons => new[] { "btn-fx-low", "btn-fx-medium", "btn-fx-high", "btn-rebind", "btn-settings-defaults", "btn-settings-back" };

        UiScreen _returnTo;
        bool _filling;

        protected override void OnCreate()
        {
            Slider("slider-master", v => Ctx.Settings.Master = v);
            Slider("slider-music", v => Ctx.Settings.Music = v);
            Slider("slider-sfx", v => Ctx.Settings.Sfx = v);
            Slider("slider-shake", v => Ctx.Settings.Shake = v);
            Slider("slider-uiscale", v => Ctx.Settings.UiScale = v);
            Q<Toggle>("toggle-fullscreen").RegisterValueChangedCallback(e =>
            {
                if (_filling) return;
                Ctx.Settings.Fullscreen = e.newValue;
                Screen.fullScreen = e.newValue;
                Ctx.Settings.Save();
            });
            Bind("btn-fx-low", () => SetEffects(EffectsLevel.Low));
            Bind("btn-fx-medium", () => SetEffects(EffectsLevel.Medium));
            Bind("btn-fx-high", () => SetEffects(EffectsLevel.High));
            Bind("btn-rebind", Rebind);
            Bind("btn-settings-defaults", Defaults);
            Bind("btn-settings-back", Back);
        }

        void Slider(string name, System.Action<float> set)
        {
            Q<Slider>(name).RegisterValueChangedCallback(e =>
            {
                if (_filling) return;
                set(e.newValue);
                Ctx.Settings.Save();
            });
        }

        public void Open(UiScreen returnTo)
        {
            _returnTo = returnTo;
            Show();
        }

        protected override void OnShow() => Fill();

        void Fill()
        {
            _filling = true;
            var s = Ctx.Settings;
            Q<Slider>("slider-master").value = s.Master;
            Q<Slider>("slider-music").value = s.Music;
            Q<Slider>("slider-sfx").value = s.Sfx;
            Q<Slider>("slider-shake").value = s.Shake;
            Q<Slider>("slider-uiscale").value = s.UiScale;
            Q<Toggle>("toggle-fullscreen").value = s.Fullscreen;
            Q<Button>("btn-fx-low").EnableInClassList("selected", s.Effects == EffectsLevel.Low);
            Q<Button>("btn-fx-medium").EnableInClassList("selected", s.Effects == EffectsLevel.Medium);
            Q<Button>("btn-fx-high").EnableInClassList("selected", s.Effects == EffectsLevel.High);
            Q<Button>("btn-rebind").text = Ctx.Input.Rebinding ? Strings.Get("config_pressione_tecla") : Strings.Format("config_tecla_especial", Ctx.Input.SpecialDisplayName());
            _filling = false;
        }

        void SetEffects(EffectsLevel level)
        {
            Ctx.Settings.Effects = level;
            Ctx.Settings.Save();
            Fill();
        }

        public void Rebind()
        {
            if (Ctx.Input.Rebinding) return;
            Ctx.Input.RebindSpecial(path =>
            {
                if (path != null)
                {
                    Ctx.Settings.SpecialBinding = path;
                    Ctx.Settings.Save();
                }
                if (Visible) Fill();
            });
            Fill();
        }

        void Defaults()
        {
            var d = new SettingsStore { PlayerName = Ctx.Settings.PlayerName };
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(d), Ctx.Settings);
            Ctx.Input.SetSpecialBinding(Ctx.Settings.SpecialBinding);
            Screen.fullScreen = false;
            Ctx.Settings.Save();
            Fill();
        }

        public void Back()
        {
            Ctx.Input.CancelRebind();
            Hide();
            if (_returnTo != null && !_returnTo.Visible) _returnTo.Show();
        }
    }
}
