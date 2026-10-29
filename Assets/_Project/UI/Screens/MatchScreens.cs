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
        Label _hp, _level, _time, _name, _synergy, _banner, _warning, _specialKey, _bossNext, _bossName, _bossPhase, _bossMore;
        VisualElement _bossBar, _bossFill, _eventList, _eventToast;
        float _toastUntil, _moodUntil;
        string _moodOverride;
        readonly List<int> _eventIds = new List<int>(8);
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
            _bossNext = Q<Label>("label-boss-next");
            _bossName = Q<Label>("label-boss-name");
            _bossPhase = Q<Label>("label-boss-phase");
            _bossMore = Q<Label>("label-boss-more");
            _bossBar = Q<VisualElement>("boss-bar");
            _bossFill = Q<VisualElement>("bar-boss-fill");
            _eventList = Q<VisualElement>("list-events");
            _eventToast = Q<VisualElement>("event-toast");
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
            if (e.Kind == RuleEventKind.BossIncoming) Banner(Strings.Format(BossFinal(e.A) ? "boss_aviso_final" : "boss_aviso", BossName(e.A)), 5f);
            if (e.Kind == RuleEventKind.BossSpawned) Banner(Strings.Format("boss_chegou", BossName(e.A)), 3f);
            if (e.Kind == RuleEventKind.BossBravo) Banner(Strings.Format("boss_bravo", BossName(e.A)), 2.5f);
            if (e.Kind == RuleEventKind.BossDefeated) Banner(Strings.Format(e.C == 1 ? "boss_derrotado_final" : "boss_derrotado", BossName(e.A)), 4f);
            if (e.Kind == RuleEventKind.MatchContinued) Banner(Strings.Get("modo_infinito_inicio"), 4f);
            if (e.Kind == RuleEventKind.EventStarted) OnEventStarted(e, local);
            if (e.Kind == RuleEventKind.EventEnded) OnEventEnded(e, local);
            if (e.Kind == RuleEventKind.EventBonus) OnEventBonus(e, local);
            if (e.Kind == RuleEventKind.SynergyChanged && e.A == local && e.C != 0 && e.B >= 0 && e.B < Ctx.Db.SynergyCount)
                Banner(Strings.Format("hud_sinergia_ativa", Ctx.Db.Synergy(e.B).Def.name), 2.5f);
        }

        string Who(int target, int local)
        {
            if (target < 0) return Strings.Get("evento_todos");
            if (target == local) return Strings.Get("evento_voce");
            var cfg = Ctx.Net.Config;
            return cfg != null && target < cfg.Roster.Length ? cfg.Roster[target].Name : "?";
        }

        void OnEventStarted(RuleEvent e, int local)
        {
            if (e.A < 0 || e.A >= Ctx.Db.EventCount) return;
            var ev = Ctx.Db.Event(e.A);
            int target = e.B == 255 ? -1 : e.B;
            UiText.Image(Q<VisualElement>("img-event-toast"), Ctx.Assets.Sprite("icon_evt_" + ev.Def.id));
            var title = Q<Label>("label-event-title");
            title.text = ev.Def.name.ToUpperInvariant() + (Ctx.Net.Mode != NetMode.Solo || target < 0 ? "  ·  " + Who(target, local) : "");
            foreach (var k in new[] { "event-positivo", "event-negativo", "event-especial", "event-neutro" }) title.RemoveFromClassList(k);
            title.AddToClassList("event-" + ev.Def.kind);
            Q<Label>("label-event-banner").text = "“" + ev.Def.banner + "”";
            string warn = e.Y > 0.05f ? "  " + Strings.Format("evento_aviso", e.Y) : "";
            Q<Label>("label-event-desc").text = ev.Def.description + warn;
            _eventToast.style.display = DisplayStyle.Flex;
            _toastUntil = _now + 6f;
            if (target == local || target < 0) Mood(ev.Def.behavior, 3.5f);
        }

        void OnEventEnded(RuleEvent e, int local)
        {
            if (e.C < 0 || e.C >= Ctx.Db.EventCount) return;
            var ev = Ctx.Db.Event(e.C);
            int target = (int)e.X;
            if (target >= 0 && target != local && Ctx.Net.Mode == NetMode.Solo) return;
            string key = e.B == EventOutcome.Success ? "evento_concluido" : e.B == EventOutcome.Failed ? "evento_falhou" : "evento_terminou";
            Banner(Strings.Format(key, ev.Def.name), 2.5f);
        }

        void OnEventBonus(RuleEvent e, int local)
        {
            int slot = e.A == 255 ? -1 : e.A;
            if (slot >= 0 && slot != local) return;
            string key;
            switch (e.B)
            {
                case EventOutcome.BonusReflex: key = "bonus_reflexo"; break;
                case EventOutcome.BonusSupreme: key = "bonus_suprema"; Mood("coxinha", 2f); break;
                case EventOutcome.BonusCoxinha: key = "bonus_coxinha"; Mood("coxinha", 2f); break;
                case EventOutcome.BonusBubblePop: key = "bonus_bolha"; break;
                case EventOutcome.BonusBubbleGoal: key = "bonus_bolha_meta"; break;
                case EventOutcome.BonusInsultBeaten: key = "bonus_burro"; Mood("grade", 2f); break;
                case EventOutcome.BonusZeroRecovered: key = "bonus_zero"; Mood("grade", 2f); break;
                case EventOutcome.BonusUpgrade: key = "bonus_melhoria"; break;
                case EventOutcome.BonusJalapenoEnd: key = "bonus_jalapeno"; Mood("coxinha", 1.5f); break;
                default: return;
            }
            Banner(Strings.Get(key), 2.5f);
        }

        /// <summary>Expressão do retrato que reage ao evento (emoções já existentes).</summary>
        void Mood(string behavior, float seconds)
        {
            string m;
            switch (behavior)
            {
                case "grade": m = "vitoria"; break;
                case "zero": m = "desespero"; break;
                case "insult": m = "raiva"; break;
                case "lab": m = "medo"; break;
                case "coxinha": m = "alivio"; break;
                case "jalapeno": m = "raiva"; break;
                case "bubble": m = "determinacao"; break;
                case "moto": m = "medo"; break;
                default: return;
            }
            _moodOverride = m;
            _moodUntil = _now + seconds;
        }

        /// <summary>Eventos em andamento: ícone, nome, para quem, tempo restante e meta.</summary>
        void UpdateEvents(MatchFeed feed, int local)
        {
            if (_now > _toastUntil) _eventToast.style.display = DisplayStyle.None;
            _eventIds.Clear();
            foreach (var kv in feed.Events) _eventIds.Add(kv.Key);
            _eventIds.Sort();
            while (_eventList.childCount > _eventIds.Count) _eventList.RemoveAt(_eventList.childCount - 1);
            for (int i = 0; i < _eventIds.Count; i++)
            {
                var info = feed.Events[_eventIds[i]];
                if (info.Event < 0 || info.Event >= Ctx.Db.EventCount) continue;
                var ev = Ctx.Db.Event(info.Event);
                VisualElement row;
                if (i >= _eventList.childCount)
                {
                    row = new VisualElement();
                    row.AddToClassList("event-row");
                    row.pickingMode = PickingMode.Ignore;
                    var icon = new VisualElement { name = "icon" };
                    icon.AddToClassList("event-row-icon");
                    row.Add(icon);
                    var col = new VisualElement();
                    col.AddToClassList("event-row-col");
                    UiText.Line(col, "", "event-row-name").name = "name";
                    var bar = new VisualElement();
                    bar.AddToClassList("bar");
                    bar.AddToClassList("event-row-bar");
                    var fill = new VisualElement { name = "fill" };
                    fill.AddToClassList("bar-fill");
                    fill.AddToClassList("event-row-fill");
                    bar.Add(fill);
                    col.Add(bar);
                    UiText.Line(col, "", "event-row-info").name = "info";
                    row.Add(col);
                    _eventList.Add(row);
                }
                row = _eventList[i];
                UiText.Image(row.Q<VisualElement>("icon"), Ctx.Assets.Sprite("icon_evt_" + ev.Def.id));
                row.Q<Label>("name").text = ev.Def.name + (info.Target != local || info.Target < 0 ? " · " + Who(info.Target, local) : "");
                bool warning = feed.Elapsed < info.StartsAt;
                float total = Mathf.Max(0.1f, (warning ? info.StartsAt : info.EndsAt) - (warning ? info.StartedAt : info.StartsAt));
                float left = Mathf.Max(0, (warning ? info.StartsAt : info.EndsAt) - feed.Elapsed);
                var fillEl = row.Q<VisualElement>("fill");
                fillEl.style.width = Length.Percent(Mathf.Clamp01(left / total) * 100f);
                fillEl.EnableInClassList("event-fill-warning", warning);
                fillEl.EnableInClassList("event-fill-" + ev.Def.kind, !warning);
                string info2 = warning ? Strings.Format("evento_em", left) : Strings.Format("evento_restam", left);
                if (info.Goal > 0) info2 += "  " + Strings.Format("evento_meta", Mathf.Min(info.Progress, info.Goal), info.Goal);
                row.Q<Label>("info").text = info2;
            }
        }

        string BossName(int boss) => boss == 255 ? Strings.Get("boss_dueto") : boss >= 0 && boss < Ctx.Db.BossCount ? Ctx.Db.Boss(boss).Def.name : "?";
        bool BossFinal(int boss) => boss >= 0 && boss < Ctx.Db.BossCount && Ctx.Db.Boss(boss).Def.final;

        /// <summary>Barra do boss ativo (o de menor vida quando há dois) e contagem até o próximo.</summary>
        void UpdateBoss(MatchFeed feed)
        {
            BossInfo shown = null;
            float shownHp = 2f;
            int alive = 0;
            foreach (var kv in feed.Bosses)
            {
                if (!feed.Tracks.TryGetEnemy(kv.Key, out _)) continue;
                alive++;
                // vida pelo bloco do boss no snapshot (igual no host e nos clientes)
                if (kv.Value.Hp < shownHp) { shownHp = kv.Value.Hp; shown = kv.Value; }
            }
            _bossBar.style.display = shown != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (shown != null)
            {
                _bossName.text = BossName(shown.Boss) + (shown.Cycle > 0 ? " " + Strings.Format("boss_ciclo", shown.Cycle) : "");
                _bossPhase.text = Strings.Get(shown.Invulnerable ? "boss_fase_entrada" : shown.Bravo ? "boss_fase_bravo" : "boss_fase_normal");
                _bossPhase.EnableInClassList("boss-phase-bravo", shown.Bravo);
                _bossFill.style.width = Length.Percent(Mathf.Clamp01(shownHp) * 100f);
                _bossFill.EnableInClassList("boss-fill-bravo", shown.Bravo);
                _bossMore.text = alive > 1 ? Strings.Format("boss_mais", alive - 1) : "";
            }
            float left = feed.NextBossAt - feed.Elapsed;
            if (feed.NextBoss >= 0 && left > 0) _bossNext.text = Strings.Format("boss_proximo", BossName(feed.NextBoss), UiText.Clock(left));
            else if (shown == null && feed.Infinite) _bossNext.text = Strings.Get("modo_infinito");
            else _bossNext.text = shown == null || !BossFinal(shown.Boss) || feed.Elapsed < Ctx.Db.Def.rules.matchSeconds ? "" : Strings.Get("boss_tempo_estendido");
            _time.EnableInClassList("hud-time-extended", !feed.Infinite && feed.Elapsed >= Ctx.Db.Def.rules.matchSeconds);
        }

        void Banner(string text, float seconds)
        {
            _banner.text = text;
            _bannerUntil = _now + seconds;
        }

        public string PortraitId => _portraitId;
        /// <summary>Fração da recarga do especial mostrada (0 = pronto).</summary>
        public float SpecialFraction => _radial.Fraction;

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
            UpdateBoss(feed);
            UpdateEvents(feed, local);
            if (_now > _bannerUntil) _banner.text = "";

            string warning = feed.HostBackgrounded ? Strings.Get("aviso_host_minimizou") : me.Status == PlayerStatus.Downed ? Strings.Get("hud_caido") : "";
            if (warning == "" && feed.CurrentOffer == null)
                for (int s = 0; s < PlayerStore.MaxSlots; s++)
                    if (s != local && feed.Players[s].Present && (feed.Players[s].Flags & PlayerSnap.FlagHasOffer) != 0)
                    {
                        warning = Strings.Format("hud_aguardando_escolha", cfg != null && s < cfg.Roster.Length ? cfg.Roster[s].Name : "?");
                        break;
                    }
            _warning.text = warning;

            // especial
            int specIndex = Ctx.Db.SpecializationIndex(spec);
            if (specIndex >= 0)
            {
                var special = Ctx.Db.Special(Ctx.Db.Specialization(specIndex).Special);
                UiText.Image(Q<VisualElement>("img-special"), Ctx.Assets.Sprite("icon_" + special.Def.id));
                _radial.Fraction = special.Def.cooldown > 0 ? me.SpecialCooldown / special.Def.cooldown : 0f;
                _radial.SetSeconds(me.SpecialCooldown);
                _specialKey.text = Ctx.Input.SpecialDisplayName();
            }

            bool allyDowned = false;
            for (int s = 0; s < PlayerStore.MaxSlots; s++)
                if (s != local && feed.Players[s].Present && feed.Players[s].Status == PlayerStatus.Downed) allyDowned = true;
            string mood = PortraitMood.Pick(hpFrac, me.Status, feed.Phase, _now - _hurtAt, _now - _healAt, _now - _evolutionAt, allyDowned);
            // eventos e estados (Tequila, Jalapeño) mudam a expressão enquanto não houver algo mais urgente
            if (me.Status == PlayerStatus.Active && feed.Phase == MatchPhase.Running && hpFrac >= 0.3f && _now - _hurtAt > 0.8f)
            {
                if (_now < _moodUntil && _moodOverride != null) mood = _moodOverride;
                else if ((me.Look & (PlayerLook.Euphoria | PlayerLook.Hangover)) != 0) mood = (me.Look & PlayerLook.Hangover) != 0 ? "exaustao" : "alivio";
                else if ((me.Look & PlayerLook.Jalapeno) != 0) mood = "raiva";
                else if ((me.Look & PlayerLook.Zero) != 0) mood = "desespero";
                else if ((me.Look & PlayerLook.Insulted) != 0) mood = "raiva";
            }
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
            // só as 3 de nível mais alto (empate: a mais antiga primeiro)
            var top = new List<OwnedAbility>(l.Abilities);
            top.Sort((x, y) => y.Level != x.Level ? y.Level.CompareTo(x.Level) : l.Abilities.IndexOf(x).CompareTo(l.Abilities.IndexOf(y)));
            if (top.Count > 3) top.RemoveRange(3, top.Count - 3);
            foreach (var a in top)
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
        public override string[] Buttons => new[] { "card-0", "card-1", "card-2", "btn-reroll", "btn-ban", "btn-skip" };

        short _offerId = -1;
        float _shownAt, _now;
        bool _chosen, _banMode;
        readonly Button[] _cards = new Button[3];

        public CardInfo[] Cards { get; } = new CardInfo[3];

        protected override void OnCreate()
        {
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                _cards[i] = Q<Button>("card-" + i);
                Bind("card-" + i, () => { if (_banMode) Ban(k); else Choose(k); });
            }
            Bind("btn-reroll", Reroll);
            Bind("btn-skip", Skip);
            Bind("btn-ban", () => { if (Ctx.Net.Feed?.CurrentOffer?.BansLeft > 0 && !_chosen) SetBanMode(!_banMode); });
        }

        void Send(int code)
        {
            var offer = Ctx.Net.Feed?.CurrentOffer;
            if (offer == null || _chosen) return;
            _chosen = true;
            SetBanMode(false);
            Ctx.Net.Choose(offer.OfferId, code);
            foreach (var c in _cards) c.SetEnabled(false);
        }

        public void Reroll() { if (Ctx.Net.Feed?.CurrentOffer?.RerollsLeft > 0) Send(ProgressionSystem.ActionReroll); }
        public void Skip() { if (Ctx.Net.Feed?.CurrentOffer?.SkipsLeft > 0) Send(ProgressionSystem.ActionSkip); }

        public void Ban(int index)
        {
            var offer = Ctx.Net.Feed?.CurrentOffer;
            if (offer == null || index < 0 || index >= offer.Options.Length) return;
            var kind = offer.Options[index].Kind;
            if (kind == OptionKind.Evolution || kind == OptionKind.Survival || offer.BansLeft == 0) return;
            Send(ProgressionSystem.ActionBanFirst - index);
        }

        void SetBanMode(bool on)
        {
            _banMode = on;
            foreach (var c in _cards) c.EnableInClassList("ban-target", on);
            Q<Button>("btn-ban").EnableInClassList("selected", on);
            Q<Label>("label-ban-hint").style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
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

            // no modo banir, 1/2/3 banem a carta (como o clique)
            for (int k = 0; k < 3; k++)
            {
                var key = k == 0 ? Ctx.Input.Choice1 : k == 1 ? Ctx.Input.Choice2 : Ctx.Input.Choice3;
                if (!key.WasPressedThisFrame()) continue;
                if (_banMode) Ban(k); else Choose(k);
            }

            bool coop = Ctx.Net.Mode != NetMode.Solo;
            // No coop a partida continua: sem escurecer a tela.
            Q<VisualElement>("screen-levelup").EnableInClassList("dim-light", !coop);
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
            if (offer.OfferId == _offerId) return;
            bool sameLevel = _offerId >= 0;
            _offerId = offer.OfferId;
            if (!sameLevel) _shownAt = _now;
            _chosen = false;
            SetBanMode(false);
            var reroll = Q<Button>("btn-reroll");
            reroll.text = Strings.Format("nivel_atualizar", offer.RerollsLeft);
            reroll.SetEnabled(offer.RerollsLeft > 0);
            var ban = Q<Button>("btn-ban");
            ban.text = Strings.Format("nivel_banir", offer.BansLeft);
            ban.SetEnabled(offer.BansLeft > 0);
            var skip = Q<Button>("btn-skip");
            skip.text = Strings.Format("nivel_pular", offer.SkipsLeft);
            skip.SetEnabled(offer.SkipsLeft > 0);
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
                rarity.RemoveFromClassList("rarity-reliquia");
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
        public override string[] Buttons => new[] { "btn-result-infinite", "btn-result-menu" };

        protected override void OnCreate()
        {
            Bind("btn-result-menu", () => Ctx.Router.Leave());
            Bind("btn-result-infinite", () => { if (Ctx.Net.ContinueInfinite()) Q<Button>("btn-result-infinite").SetEnabled(false); });
        }

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
            Q<Label>("label-result-bosses").text = Strings.Format("resultado_bosses", summary?.BossesDefeated ?? feed?.BossesDefeated ?? 0);
            // Modo Infinito: só depois de vencer o boss final; quem decide é o anfitrião (ou o solo)
            bool canContinue = !disconnected && phase == MatchPhase.Victory && !(summary?.Infinite ?? false) && Ctx.Db.FinalBoss >= 0;
            var infinite = Q<Button>("btn-result-infinite");
            infinite.style.display = canContinue && Ctx.Net.CanDecideInfinite ? DisplayStyle.Flex : DisplayStyle.None;
            infinite.SetEnabled(true);
            Q<Label>("label-result-wait").style.display = canContinue && !Ctx.Net.CanDecideInfinite ? DisplayStyle.Flex : DisplayStyle.None;
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
