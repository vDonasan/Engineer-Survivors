using System;
using System.Collections.Generic;
using ES.Game;
using ES.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES.UI
{
    /// <summary>Sprite de personagem animado (troca de quadros) para cartões, menu e ficha.</summary>
    public sealed class HeroSprite : VisualElement
    {
        Sprite[] _frames = new Sprite[0];
        int _frame;
        IVisualElementScheduledItem _timer;

        public HeroSprite(float size)
        {
            pickingMode = PickingMode.Ignore;
            style.width = size;
            style.height = size;
            style.flexShrink = 0;
        }

        /// <summary>Quadros "char_&lt;id&gt;_&lt;anim&gt;_&lt;dir&gt;_n" (os que existirem).</summary>
        public void Play(GameAssets assets, string id, string anim, string dir, int fps, int offset = 0)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < 8; i++)
            {
                var s = assets.Sprite($"char_{id}_{anim}_{dir}_{i}");
                if (s == null) break;
                list.Add(s);
            }
            _frames = list.ToArray();
            _frame = offset;
            Show();
            _timer?.Pause();
            if (_frames.Length > 1) _timer = schedule.Execute(() => { _frame++; Show(); }).Every(1000 / Math.Max(1, fps));
        }

        void Show() => UiText.Image(this, _frames.Length > 0 ? _frames[_frame % _frames.Length] : null);
    }

    /// <summary>Cartões de personagem: retrato, nome e engenharia.</summary>
    public static class SpecCards
    {
        public static Dictionary<string, Button> Build(UiContext ctx, VisualElement list, Action<string> pick, bool compact)
        {
            list.Clear();
            var map = new Dictionary<string, Button>();
            for (int i = 0; i < ctx.Db.SpecializationCount; i++)
            {
                var spec = ctx.Db.Specialization(i);
                if (spec.Def.hidden) continue;
                string id = spec.Def.id;
                var b = new Button(() => { ctx.Click(); pick(id); }) { name = "spec-" + id };
                b.AddToClassList("btn");
                b.AddToClassList(compact ? "hero-card-small" : "hero-card");
                var portrait = new VisualElement();
                portrait.AddToClassList("portrait");
                portrait.pickingMode = PickingMode.Ignore;
                UiText.Image(portrait, SelectPortrait(ctx.Assets, id));
                b.Add(portrait);
                UiText.Line(b, spec.Def.name, "spec-name").pickingMode = PickingMode.Ignore;
                if (!compact) UiText.Line(b, ShortEngineering(spec.Def.engineering), "spec-eng").pickingMode = PickingMode.Ignore;
                list.Add(b);
                map[id] = b;
            }
            return map;
        }

        /// <summary>Retrato da tela de escolha; sem imagem própria, usa o retrato do jogo.</summary>
        public static Sprite SelectPortrait(GameAssets assets, string id) => assets.Sprite($"select_{id}") ?? assets.Sprite($"portrait_{id}_determinacao");

        public static string ShortEngineering(string e) => string.IsNullOrEmpty(e) ? "" : e.Replace("Engenharia da ", "").Replace("Engenharia ", "");

        public static void Select(Dictionary<string, Button> map, string id)
        {
            foreach (var kv in map) kv.Value.EnableInClassList("selected", kv.Key == id);
        }
    }

    /// <summary>
    /// Ficha do personagem: retrato, sprite andando, apresentação e tudo o que a arma faz —
    /// níveis, evolução (com o componente pedido), melhoria avançada e especial.
    /// </summary>
    public sealed class HeroSheet
    {
        readonly UiContext _ctx;
        readonly VisualElement _overlay, _portrait, _body;
        readonly HeroSprite _walk;
        readonly Label _name, _eng, _bio;
        readonly Button _choose;
        Action _onChoose;

        public bool Visible => _overlay.style.display == DisplayStyle.Flex;

        public HeroSheet(UiContext ctx, VisualElement host)
        {
            _ctx = ctx;
            _overlay = new VisualElement { name = "hero-sheet" };
            _overlay.AddToClassList("sheet-overlay");
            _overlay.RegisterCallback<ClickEvent>(e => { if (e.target == _overlay) Hide(); });
            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.AddToClassList("sheet-panel");
            _overlay.Add(panel);

            var top = new VisualElement();
            top.AddToClassList("sheet-columns");
            panel.Add(top);

            var left = new VisualElement();
            left.AddToClassList("sheet-left");
            top.Add(left);
            _portrait = new VisualElement();
            _portrait.AddToClassList("sheet-portrait");
            left.Add(_portrait);
            var stage = new VisualElement();
            stage.AddToClassList("sheet-stage");
            _walk = new HeroSprite(112);
            stage.Add(_walk);
            left.Add(stage);
            _name = UiText.Line(left, "", "sheet-name");
            _eng = UiText.Line(left, "", "sheet-eng");

            var right = new ScrollView(ScrollViewMode.Vertical);
            right.AddToClassList("sheet-right");
            top.Add(right);
            _bio = UiText.Line(right, "", "sheet-bio");
            _body = new VisualElement();
            right.Add(_body);

            var buttons = new VisualElement();
            buttons.AddToClassList("row");
            buttons.AddToClassList("sheet-buttons");
            panel.Add(buttons);
            var close = new Button(() => { _ctx.Click(); Hide(); }) { name = "btn-sheet-close", text = Strings.Get("ficha_fechar") };
            close.AddToClassList("btn");
            close.AddToClassList("big");
            buttons.Add(close);
            _choose = new Button(() => { _ctx.Click(); var a = _onChoose; Hide(); a?.Invoke(); }) { name = "btn-sheet-choose" };
            _choose.AddToClassList("btn");
            _choose.AddToClassList("big");
            _choose.AddToClassList("primary");
            buttons.Add(_choose);

            _overlay.style.display = DisplayStyle.None;
            host.Add(_overlay);
        }

        public void Hide() => _overlay.style.display = DisplayStyle.None;

        /// <param name="onChoose">null esconde o botão de escolher.</param>
        public void Show(string id, Action onChoose)
        {
            var db = _ctx.Db;
            int i = db.SpecializationIndex(id);
            if (i < 0) return;
            var spec = db.Specialization(i);
            _onChoose = onChoose;
            _choose.style.display = onChoose != null ? DisplayStyle.Flex : DisplayStyle.None;
            _choose.text = Strings.Format("ficha_escolher", spec.Def.name);
            UiText.Image(_portrait, SpecCards.SelectPortrait(_ctx.Assets, id));
            _walk.Play(_ctx.Assets, id, "walk", "down", 8);
            _name.text = spec.Def.name;
            _eng.text = spec.Def.engineering ?? "";
            _bio.text = spec.Def.bio ?? "";
            Fill(spec);
            _overlay.style.display = DisplayStyle.Flex;
            _overlay.BringToFront();
        }

        void Fill(ResolvedSpecialization spec)
        {
            var db = _ctx.Db;
            _body.Clear();
            var weapon = db.Ability(spec.StartingAbility);
            Section(Strings.Get("ficha_arma"), weapon.Def.name, weapon.Def.description, $"icon_{weapon.Id}");
            var levels = new VisualElement();
            levels.AddToClassList("sheet-list");
            for (int l = 0; l < weapon.Levels.Length; l++)
                Bullet(levels, Strings.Format("ficha_nivel", l + 2), WithStats(weapon.Levels[l].Text, weapon, l + 1, l + 2), l + 2 == 3 || l + 2 == 5);
            _body.Add(levels);

            for (int e = 0; e < db.EvolutionCount; e++)
            {
                var evo = db.Evolution(e);
                if (evo.From != spec.StartingAbility) continue;
                var result = db.Ability(evo.Result);
                Section(Strings.Get("ficha_evolucao"), result.Def.name, result.Def.description, $"icon_{result.Id}");
                var req = new VisualElement();
                req.AddToClassList("row");
                req.AddToClassList("sheet-req");
                UiText.Line(req, Strings.Get("ficha_requisito"), "sheet-label");
                foreach (var r in evo.Def.requires)
                {
                    int idx = db.AbilityIndex(r.target);
                    if (idx < 0) continue;
                    var chip = new VisualElement();
                    chip.AddToClassList("chip");
                    var ic = new VisualElement();
                    ic.AddToClassList("chip-icon");
                    UiText.Image(ic, _ctx.Assets.Sprite($"icon_{r.target}"));
                    chip.Add(ic);
                    UiText.Line(chip, Strings.Format("ficha_chip", db.Ability(idx).Def.name, (int)r.value));
                    req.Add(chip);
                }
                _body.Add(req);
                var evoLevels = new VisualElement();
                evoLevels.AddToClassList("sheet-list");
                for (int l = 0; l < result.Levels.Length; l++)
                    Bullet(evoLevels, Strings.Format("ficha_nivel", l + 2), WithStats(result.Levels[l].Text, result, l + 1, l + 2), l + 2 == result.MaxLevel);
                _body.Add(evoLevels);
            }

            var special = db.Special(spec.Special);
            Section(Strings.Format("ficha_especial", _ctx.Input.SpecialDisplayName()), special.Def.name, special.Def.description, $"icon_{special.Def.id}");
        }

        void Section(string kind, string name, string desc, string icon)
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            row.AddToClassList("sheet-section");
            var ic = new VisualElement();
            ic.AddToClassList("sheet-icon");
            UiText.Image(ic, _ctx.Assets.Sprite(icon));
            row.Add(ic);
            var col = new VisualElement();
            col.style.flexShrink = 1;
            col.style.flexGrow = 1;
            UiText.Line(col, kind, "sheet-kind");
            UiText.Line(col, name, "sheet-title");
            UiText.Line(col, desc, "sheet-desc");
            row.Add(col);
            _body.Add(row);
        }

        static string WithStats(string text, ResolvedAbility a, int from, int to)
        {
            string stats = StatText.Lines(a, from, to).Replace("\n", " · ");
            return string.IsNullOrEmpty(stats) ? text : text + "  (" + stats + ")";
        }

        static void Bullet(VisualElement parent, string label, string text, bool highlight)
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            row.AddToClassList("sheet-bullet");
            UiText.Line(row, label, highlight ? "sheet-level-hi" : "sheet-level");
            UiText.Line(row, text, "sheet-desc");
            parent.Add(row);
        }
    }
}
