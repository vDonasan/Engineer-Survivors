using System;
using System.Collections.Generic;
using ES.Game;
using ES.Net;
using ES.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES.UI
{
    /// <summary>UXML, tema e painel da interface (gerado pelo editor).</summary>
    [CreateAssetMenu(menuName = "Engineering Survivors/Recursos da interface")]
    public sealed class UiAssets : ScriptableObject
    {
        public VisualTreeAsset[] screens = new VisualTreeAsset[0];
        public StyleSheet theme;
        public PanelSettings panel;

        public VisualTreeAsset Screen(string name)
        {
            foreach (var s in screens) if (s != null && s.name == name) return s;
            return null;
        }
    }

    /// <summary>Dependências compartilhadas pelos controladores de tela.</summary>
    public sealed class UiContext
    {
        public NetGame Net;
        public GameDatabase Db;
        public GameAssets Assets;
        public UiAssets Ui;
        public SettingsStore Settings;
        public GameInput Input;
        public MatchView Match;
        public PerfOverlay Perf;
        public UiRouter Router;

        public void Click() => SfxPlayer.Instance?.Play("clique_ui", AudioBus.Ui, 0.5f, 0f);

        public string PlayerName => string.IsNullOrWhiteSpace(Settings.PlayerName) ? Strings.Get("nome_padrao") : Settings.PlayerName.Trim();

        public string SpecName(string id)
        {
            int i = Db.SpecializationIndex(id);
            return i >= 0 ? Db.Specialization(i).Def.name : id;
        }

        public static string TagName(Tag t)
        {
            string key = "tag_" + t;
            return Strings.Has(key) ? Strings.Get(key) : t.ToString();
        }
    }

    /// <summary>Uma tela (UXML + controlador). Cada botão do UXML precisa estar em <see cref="Buttons"/>.</summary>
    public abstract class UiScreen
    {
        public VisualElement Root { get; private set; }
        protected UiContext Ctx;
        readonly HashSet<string> _bound = new HashSet<string>();

        public abstract string Uxml { get; }
        /// <summary>Nomes dos botões do UXML tratados por este controlador (conferido por teste).</summary>
        public abstract string[] Buttons { get; }
        public bool Visible => Root != null && Root.style.display != DisplayStyle.None;
        public IReadOnlyCollection<string> Bound => _bound;

        public void Create(UiContext ctx, VisualElement parent)
        {
            Ctx = ctx;
            var tree = ctx.Ui.Screen(Uxml);
            if (tree == null) throw new InvalidOperationException("UXML ausente: " + Uxml);
            Root = tree.Instantiate();
            Root.name = "tpl-" + Uxml;
            Root.style.position = Position.Absolute;
            Root.style.left = Root.style.top = Root.style.right = Root.style.bottom = 0;
            parent.Add(Root);
            UiText.Localize(Root);
            OnCreate();
            foreach (var b in Buttons)
                if (!_bound.Contains(b)) Debug.LogError($"[ES] Botão sem função em {Uxml}: {b}");
            Root.style.display = DisplayStyle.None;
        }

        protected void Bind(string name, Action action)
        {
            var b = Root.Q<Button>(name);
            if (b == null) { Debug.LogError($"[ES] Botão {name} não existe em {Uxml}"); return; }
            b.clicked += () => { Ctx.Click(); action(); };
            _bound.Add(name);
        }

        protected T Q<T>(string name) where T : VisualElement => Root.Q<T>(name);

        public void Show()
        {
            Root.style.display = DisplayStyle.Flex;
            Root.BringToFront();
            OnShow();
        }

        public void Hide()
        {
            if (!Visible) return;
            Root.style.display = DisplayStyle.None;
            OnHide();
        }

        protected abstract void OnCreate();
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
        public virtual void Tick(float dt) { }
    }

    public static class UiText
    {
        /// <summary>Troca textos "#chave" pela tabela de textos.</summary>
        public static void Localize(VisualElement root)
        {
            root.Query<VisualElement>().ForEach(e =>
            {
                if (e is TextElement t && t.text != null && t.text.StartsWith("#")) t.text = Strings.Get(t.text.Substring(1));
                switch (e)
                {
                    case Slider s when s.label != null && s.label.StartsWith("#"): s.label = Strings.Get(s.label.Substring(1)); break;
                    case Toggle g when g.label != null && g.label.StartsWith("#"): g.label = Strings.Get(g.label.Substring(1)); break;
                    case TextField f when f.label != null && f.label.StartsWith("#"): f.label = Strings.Get(f.label.Substring(1)); break;
                }
            });
        }

        public static void Image(VisualElement e, Sprite s)
        {
            if (e == null) return;
            e.style.backgroundImage = s != null ? new StyleBackground(s) : new StyleBackground(StyleKeyword.None);
        }

        public static Label Line(VisualElement parent, string text, string cls = null)
        {
            var l = new Label(text);
            if (cls != null) l.AddToClassList(cls);
            parent.Add(l);
            return l;
        }

        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return Strings.Format("hud_tempo", s / 60, s % 60);
        }
    }

    /// <summary>Escurece um quadrante circular (recarga do especial).</summary>
    public sealed class RadialCooldown : VisualElement
    {
        float _fraction;

        public RadialCooldown()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = 0;
            generateVisualContent += Draw;
        }

        public float Fraction
        {
            get => _fraction;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Abs(value - _fraction) < 0.004f && !(value == 0 && _fraction != 0)) return;
                _fraction = value;
                MarkDirtyRepaint();
            }
        }

        void Draw(MeshGenerationContext mgc)
        {
            if (_fraction <= 0f) return;
            var r = contentRect;
            var c = r.center;
            float radius = Mathf.Max(r.width, r.height) * 0.75f;
            var p = mgc.painter2D;
            p.fillColor = new Color(0.08f, 0.09f, 0.1f, 0.72f);
            p.BeginPath();
            p.MoveTo(c);
            p.Arc(c, radius, Angle.Degrees(-90f), Angle.Degrees(-90f + 360f * _fraction));
            p.ClosePath();
            p.Fill();
        }
    }
}
