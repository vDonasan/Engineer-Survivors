using System;
using ES.Net;
using ES.Sim;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using PlayerInput = ES.Sim.PlayerInput;

namespace ES.Game
{
    /// <summary>
    /// Desenha a partida a partir do MatchFeed (igual no host e no cliente), lê a entrada
    /// local e repassa os eventos de regra para a interface.
    /// </summary>
    public sealed class MatchView : MonoBehaviour
    {
        GameAssets _assets;
        NetGame _net;
        SettingsStore _settings;
        GameInput _input;
        Camera _cam;
        CameraRig _rig;
        Transform _root;
        EnemyViews _enemies;
        PlayerViews _players;
        FxSystem _fx;
        AllyViews _allies;
        readonly RenderFrame _frame = new RenderFrame();
        float _now;
        int _surges;
        int _prensa = -1;
        Light2D _light;
        float _lightBase = 1f, _blackoutAt = -9f;
        const float BlackoutSeconds = 0.6f;

        /// <summary>Eventos de regra deste quadro (subida de nível, evolução, fim...).</summary>
        public event Action<DecodedRuleEvent> RuleEvent;
        /// <summary>O jogador local levou dano (valor final).</summary>
        public event Action<float> LocalHit;
        /// <summary>Começou um surto (a cada 60 s).</summary>
        public event Action Surge;

        /// <summary>A interface bloqueia movimento/especial (menu aberto, rebind).</summary>
        public bool InputBlocked { get; set; }
        public bool Active => _root != null;
        public int EnemyCount => _enemies?.Count ?? 0;
        public int HostileCount => _frame.Hostile.Count;
        public int GemCount => _net?.Feed?.Gems.Count ?? 0;
        public int StampCount => (_fx?.StampCount ?? 0) + (_allies?.Count ?? 0);
        public int AllyCount => _frame.Allies.Count;
        public GameInput Input => _input;

        public void Init(GameAssets assets, NetGame net, SettingsStore settings, GameInput input, Camera cam)
        {
            _assets = assets;
            _net = net;
            _settings = settings;
            _input = input;
            _cam = cam;
            _rig = new CameraRig(cam);
        }

        void Begin()
        {
            var db = _net.Db;
            var cfg = _net.Config;
            _root = new GameObject("Match").transform;
            var arena = db.Arena(cfg?.ArenaId ?? "laboratorio");
            ArenaBuilder.Build(arena, _assets, _root);
            _rig.ArenaSize = new Vector2(arena.width, arena.height);
            _rig.Reset();
            _enemies = new EnemyViews(_root, _assets, db);
            _players = new PlayerViews(_root, _assets, cfg);
            _fx = new FxSystem(_root, _assets, db);
            _allies = new AllyViews(_root, _assets);
            _prensa = db.EnemyIndex("prensa_ambulante");
            _light = FindFirstObjectByType<Light2D>();
            if (_light != null) _lightBase = _light.intensity;
            _now = 0;
            _surges = 0;
            SfxPlayer.Instance?.PlayMusic("loop_tensao");
        }

        void End()
        {
            if (_root != null) Destroy(_root.gameObject);
            _root = null;
            if (_light != null) _light.intensity = _lightBase;
            _enemies = null;
            _players = null;
            _fx = null;
            _allies = null;
            SfxPlayer.Instance?.StopMusic();
        }

        void Update()
        {
            if (_net == null) return;
            bool started = _net.Started && _net.Feed != null && _net.Config != null;
            if (started && !Active) Begin();
            if (!started)
            {
                if (Active) End();
                return;
            }

            var feed = _net.Feed;
            int local = _net.LocalSlot;
            ReadInput(feed);

            bool frozen = _net.Mode == NetMode.Solo && (_net.Paused || feed.CurrentOffer != null);
            if (!frozen && feed.Phase == MatchPhase.Running) _now += Time.unscaledDeltaTime;
            else if (feed.Phase != MatchPhase.Running) _now += Time.unscaledDeltaTime;

            _fx.Effects = _settings.Effects;
            _rig.ShakeScale = _settings.Shake;
            feed.Tracks.Fill(feed.RenderTick, _frame);
            DrainEvents(feed, local);

            int newSurges = Mathf.FloorToInt(feed.Elapsed / 60f);
            if (newSurges > _surges && feed.Phase == MatchPhase.Running)
            {
                _surges = newSurges;
                SfxPlayer.Instance?.Play("alarme_surto", AudioBus.Sfx, 0.6f);
                Surge?.Invoke();
            }

            Vec2? localPos = _net.Client?.Prediction != null ? _net.Client.Prediction.RenderPosition : (Vec2?)null;
            _enemies.Sync(_frame.Enemies, _now);
            _players.Sync(feed, _frame, local, localPos, _now);
            float tickSeconds = 1f / Mathf.Max(1, _net.Db.Def.rules.tickRate);
            _fx.Tick(_now, feed, _frame, _players, feed.RenderTick * tickSeconds);
            _allies.Tick(_frame, _now, _settings.Effects);

            var target = CameraTarget(feed, local);
            if (target.HasValue) _rig.Follow(target.Value, Time.unscaledDeltaTime);
            UpdateLight();
        }

        /// <summary>Luz 2D global: o Apagão escurece o laboratório por um instante (efeitos média/alta).</summary>
        void UpdateLight()
        {
            if (_light == null) return;
            float k = (_now - _blackoutAt) / BlackoutSeconds;
            bool dim = _settings.Effects != EffectsLevel.Low && k >= 0f && k < 1f;
            _light.intensity = dim ? _lightBase * Mathf.Lerp(0.35f, 1f, k * k) : _lightBase;
        }

        Vector2? CameraTarget(MatchFeed feed, int local)
        {
            if (local >= 0 && feed.Players[local].Present && feed.Players[local].Status == PlayerStatus.Active) return _players.PositionOf(local);
            for (int s = 0; s < PlayerStore.MaxSlots; s++)
                if (feed.Players[s].Present && feed.Players[s].Status == PlayerStatus.Active) return _players.PositionOf(s);
            return local >= 0 ? _players.PositionOf(local) : null;
        }

        void ReadInput(MatchFeed feed)
        {
            var input = PlayerInput.None;
            if (!InputBlocked && _input != null && feed.Phase == MatchPhase.Running)
            {
                var m = _input.MoveValue;
                input.MoveX = Quant.Axis(m.x);
                input.MoveY = Quant.Axis(m.y);
                if (_input.Special.WasPressedThisFrame()) _net.RequestSpecial();
                if (_input.DebugSpawn.WasPressedThisFrame()) DebugSpawn(50);
            }
            _net.SetLocalInput(input);
        }

        /// <summary>Comando de depuração (F4, só no solo): gera inimigos perto do jogador para medir desempenho.</summary>
        public int DebugSpawn(int count)
        {
            var w = _net.Host?.World;
            if (_net.Mode != NetMode.Solo || w == null) return 0;
            var p = w.Players[0];
            if (p == null) return 0;
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                float ang = UnityEngine.Random.value * Mathf.PI * 2f, r = UnityEngine.Random.Range(8f, 12f);
                var pos = new Vec2(Mathf.Clamp(p.Pos.X + Mathf.Cos(ang) * r, 1f, w.Arena.width - 1f), Mathf.Clamp(p.Pos.Y + Mathf.Sin(ang) * r, 1f, w.Arena.height - 1f));
                if (SpawnDirector.SpawnEnemy(w, 0, pos, false) >= 0) spawned++;
            }
            return spawned;
        }

        void DrainEvents(MatchFeed feed, int local)
        {
            var sfx = SfxPlayer.Instance;
            foreach (var c in feed.Cosmetics)
            {
                _fx.OnCosmetic(c, _now);
                switch (c.Kind)
                {
                    case CosmeticKind.Hit:
                        _players.OnHit(c.A, _now);
                        if (c.A == local)
                        {
                            sfx?.Play("dano_jogador", AudioBus.Sfx, 0.7f);
                            _rig.AddShake(0.25f);
                            LocalHit?.Invoke(c.Value);
                        }
                        break;
                    case CosmeticKind.ChainArc:
                        _players.OnAttack(c.A, _now);
                        if (c.B == 0) sfx?.Play("descarga", AudioBus.Sfx, 0.45f);
                        else sfx?.Play("arco", AudioBus.Sfx, 0.25f);
                        break;
                    case CosmeticKind.ProjectileFired:
                        _players.OnAttack(c.A, _now);
                        sfx?.Play(FireSound(c.B, (int)c.Value), AudioBus.Sfx, c.A == local ? 0.4f : 0.22f);
                        break;
                    case CosmeticKind.Swing:
                        _players.OnAttack(c.A, _now);
                        break;
                }
            }
            feed.Cosmetics.Clear();

            foreach (var d in feed.RuleEvents)
            {
                var e = d.Event;
                switch (e.Kind)
                {
                    case RuleEventKind.EnemyDied:
                        _fx.OnEnemyDied(new Vector2(e.X, e.Y), e.B, _now);
                        if (e.B == _prensa) { sfx?.Play("explosao", AudioBus.Sfx, 0.6f); _rig.AddShake(0.08f); }
                        else sfx?.Play("impacto_metal", AudioBus.Sfx, 0.25f);
                        break;
                    case RuleEventKind.GemCollected:
                        if (e.B == local) sfx?.Play("gema", AudioBus.Sfx, 0.35f, 0.1f);
                        break;
                    case RuleEventKind.LevelUpOffer:
                        if (e.A == local) sfx?.Play("nivel", AudioBus.Ui, 0.8f, 0f);
                        break;
                    case RuleEventKind.EvolutionApplied:
                        sfx?.Play("evolucao", AudioBus.Sfx, 0.9f, 0f);
                        if (e.C >= 0 && e.C < _net.Db.AbilityCount && _net.Db.Ability(e.C).Id == "cabazord_master")
                        {
                            var at = _players.PositionOf(e.A);
                            if (at.HasValue) _fx.Assemble(at.Value, _now);
                            sfx?.Play("montagem", AudioBus.Sfx, 0.9f, 0f);
                        }
                        break;
                    case RuleEventKind.SpecialActivated:
                        _players.OnSpecial(e.A, _now);
                        var sp = e.B >= 0 && e.B < _net.Db.SpecialCount ? _net.Db.Special(e.B) : null;
                        if (sp != null) sfx?.Play(sp.Kind == SpecialKind.Blackout ? "apagao" : sp.Kind == SpecialKind.Overclock ? "overclock" : "turbina", AudioBus.Sfx, 0.9f, 0f);
                        if (sp != null && sp.Kind == SpecialKind.Blackout) _blackoutAt = _now;
                        if (e.A == local) _rig.AddShake(0.3f);
                        break;
                    case RuleEventKind.PlayerDowned:
                        sfx?.Play("dano_jogador", AudioBus.Sfx, 1f, 0f);
                        break;
                    case RuleEventKind.MatchEnded:
                        SfxPlayer.Instance?.StopMusic();
                        sfx?.Play((MatchPhase)e.A == MatchPhase.Victory ? "vitoria" : "derrota", AudioBus.Sfx, 1f, 0f);
                        break;
                }
                RuleEvent?.Invoke(d);
            }
            feed.RuleEvents.Clear();
        }

        /// <summary>Som de disparo: "sfx"[variante] da habilidade; o laser antigo não tem lista.</summary>
        string FireSound(int ability, int variant)
        {
            var db = _net.Db;
            if (ability < 0 || ability >= db.AbilityCount) return "laser";
            var list = db.Ability(ability).Def.sfx;
            if (list == null || list.Length == 0) return "laser";
            return list[Mathf.Clamp(variant, 0, list.Length - 1)];
        }

        void OnDestroy() => End();
    }
}
