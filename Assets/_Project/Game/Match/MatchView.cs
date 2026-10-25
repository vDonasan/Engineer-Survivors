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
        BossViews _bosses;
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
        public BossViews Bosses => _bosses;

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
            _players = new PlayerViews(_root, _assets, cfg, db);
            _fx = new FxSystem(_root, _assets, db);
            _allies = new AllyViews(_root, _assets);
            _bosses = new BossViews(_root, _assets, db);
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
            _bosses = null;
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
            _bosses.Sync(feed, _frame, _now);
            bool lab = LabActive(feed);
            _players.Sync(feed, _frame, local, localPos, _now, lab);
            // Laboratório Incompleto: luzes piscando
            if (_light != null && lab && Time.unscaledTime - _blackoutAt > BlackoutSeconds)
                _light.intensity = _lightBase * (((int)(Time.unscaledTime * 4f) % 5) == 0 ? 0.55f : 1f);
            float tickSeconds = 1f / Mathf.Max(1, _net.Db.Def.rules.tickRate);
            _fx.Tick(_now, feed, _frame, _players, feed.RenderTick * tickSeconds);
            if (_motoSoundAt > 0 && _now >= _motoSoundAt) { _motoSoundAt = -1; SfxPlayer.Instance?.Play("moto", AudioBus.Sfx, 0.9f, 0f); }
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
                    case RuleEventKind.BossIncoming:
                        sfx?.Play("boss_aviso", AudioBus.Sfx, 0.9f, 0f);
                        break;
                    case RuleEventKind.BossSpawned:
                    {
                        bool mago = e.A >= 0 && e.A < _net.Db.BossCount && _net.Db.Boss(e.A).Def.id == "mago";
                        sfx?.Play(mago ? "boss_mago_entrada" : "boss_guirotto_entrada", AudioBus.Sfx, 1f, 0f);
                        _rig.AddShake(0.25f);
                        break;
                    }
                    case RuleEventKind.BossBravo:
                        sfx?.Play("boss_furia", AudioBus.Sfx, 1f, 0f);
                        _rig.AddShake(0.35f);
                        break;
                    case RuleEventKind.BossAction:
                    {
                        string s = BossSound((BossAttack)e.B, (BossState)e.C);
                        if (s != null) sfx?.Play(s, AudioBus.Sfx, 0.75f);
                        break;
                    }
                    case RuleEventKind.BossDefeated:
                        _bosses.OnDefeated(e.A, new Vector2(e.X, e.Y), _now);
                        sfx?.Play("boss_morte", AudioBus.Sfx, 1f, 0f);
                        _rig.AddShake(0.4f);
                        break;
                    case RuleEventKind.EventStarted:
                        OnEventStarted(e, local);
                        break;
                    case RuleEventKind.EventEnded:
                        if (e.B == EventOutcome.Success) sfx?.Play("evento_sucesso", AudioBus.Sfx, 0.8f, 0f);
                        if (IsLab(e.C) && _light != null) _light.intensity = _lightBase;
                        break;
                    case RuleEventKind.EventMoto:
                        sfx?.Play("moto_aviso", AudioBus.Sfx, 0.9f, 0f);
                        _motoSoundAt = _now + e.C / 1000f;
                        break;
                    case RuleEventKind.EventBonus:
                        OnEventBonus(e);
                        break;
                    case RuleEventKind.MatchContinued:
                        sfx?.Play("modo_infinito", AudioBus.Sfx, 1f, 0f);
                        SfxPlayer.Instance?.PlayMusic("loop_tensao");
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

        float _motoSoundAt = -1;

        bool IsLab(int ev) => ev >= 0 && ev < _net.Db.EventCount && _net.Db.Event(ev).Def.behavior == "lab";

        bool LabActive(MatchFeed feed)
        {
            foreach (var kv in feed.Events) if (IsLab(kv.Value.Event) && feed.Elapsed >= kv.Value.StartsAt) return true;
            return false;
        }

        void OnEventStarted(in RuleEvent e, int local)
        {
            var sfx = SfxPlayer.Instance;
            if (e.A < 0 || e.A >= _net.Db.EventCount) return;
            var ev = _net.Db.Event(e.A);
            sfx?.Play(ev.Kind == EventKind.Positivo ? "evento_positivo" : ev.Kind == EventKind.Negativo ? "evento_negativo" : "evento_especial", AudioBus.Ui, 0.8f, 0f);
            int target = e.B == 255 ? -1 : e.B;
            switch (ev.Def.behavior)
            {
                case "grade":
                    sfx?.Play("carimbo", AudioBus.Sfx, 0.9f, 0f);
                    for (int s = 0; s < PlayerStore.MaxSlots; s++) _players.Emote(s, "dez", _now, 3f);
                    break;
                case "zero":
                    sfx?.Play("papel_amassado", AudioBus.Sfx, 0.8f, 0f);
                    break;
                case "insult":
                    sfx?.Play("buzina_burro", AudioBus.Sfx, 0.8f, 0f);
                    break;
                case "lab":
                    sfx?.Play("alarme_surto", AudioBus.Sfx, 0.7f);
                    break;
                case "jalapeno":
                    sfx?.Play("jalapeno", AudioBus.Sfx, 0.8f, 0f);
                    _players.Emote(target, "fogo", _now, 1.5f);
                    break;
                case "bubble":
                    sfx?.Play("notificacao", AudioBus.Sfx, 0.8f, 0f);
                    break;
            }
        }

        void OnEventBonus(in RuleEvent e)
        {
            var sfx = SfxPlayer.Instance;
            int slot = e.A == 255 ? -1 : e.A;
            var at = new Vector2(e.X, e.Y);
            switch (e.B)
            {
                case EventOutcome.BonusCoxinha: sfx?.Play("coxinha", AudioBus.Sfx, 0.9f, 0f); _players.Emote(slot, "coxinha", _now, 2f); break;
                case EventOutcome.BonusSupreme: sfx?.Play("coxinha_suprema", AudioBus.Sfx, 1f, 0f); _players.Emote(slot, "coxinha", _now, 2.5f); _rig.AddShake(0.2f); break;
                case EventOutcome.BonusReflex: sfx?.Play("reflexo", AudioBus.Sfx, 0.9f, 0f); break;
                case EventOutcome.BonusBubblePop: sfx?.Play("notificacao", AudioBus.Sfx, 1f, 0f); _players.Emote(slot, "coracao", _now, 1.5f); break;
                case EventOutcome.BonusBubbleGoal: sfx?.Play("evento_sucesso", AudioBus.Sfx, 0.7f, 0f); _players.Emote(slot, "notificacao", _now, 1.5f); break;
                case EventOutcome.BonusInsultBeaten: sfx?.Play("evento_sucesso", AudioBus.Sfx, 0.8f, 0f); break;
                case EventOutcome.BonusZeroRecovered: sfx?.Play("carimbo", AudioBus.Sfx, 0.8f, 0f); _players.Emote(slot, "dez", _now, 2f); break;
                case EventOutcome.BonusJalapenoEnd: sfx?.Play("alivio", AudioBus.Sfx, 0.8f, 0f); _rig.AddShake(0.12f); break;
                case EventOutcome.BonusUpgrade: sfx?.Play("evolucao", AudioBus.Sfx, 0.6f, 0f); break;
            }
        }

        /// <summary>Som da preparação ou da execução de um ataque de boss.</summary>
        static string BossSound(BossAttack a, BossState s)
        {
            if (s == BossState.Windup)
                return a == BossAttack.Sobrecarga ? "carga" : a == BossAttack.Raio ? "raio_mira" : a == BossAttack.Ressonancia || a == BossAttack.Concerto ? "coaxar" : null;
            switch (a)
            {
                case BossAttack.Acorde: return "onda_sonora";
                case BossAttack.Ressonancia: return "ressonancia";
                case BossAttack.ChuvaNotas: return "notas";
                case BossAttack.Concerto: case BossAttack.Refrao: return "banjo";
                case BossAttack.Raio: case BossAttack.Cadeia: return "raio";
                case BossAttack.Tempestade: return "tempestade";
                case BossAttack.Orbes: return "osciloscopio";
                case BossAttack.Sobrecarga: return "sobrecarga";
                case BossAttack.Teleporte: return "teleporte";
                default: return null;
            }
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
