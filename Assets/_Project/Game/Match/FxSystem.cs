using System.Collections.Generic;
using ES.Sim;
using UnityEngine;

namespace ES.Game
{
    /// <summary>
    /// Efeitos em pixel art desenhados como carimbos: gemas, projéteis, arcos elétricos,
    /// braços giratórios, campo, ondas dos especiais, avisos de disparo, explosões e números de dano.
    /// </summary>
    public sealed class FxSystem
    {
        struct Timed { public Vector2 A, B; public float Start, Life, Value; public int Slot, Kind; }

        const int KindArc = 0, KindExplosion = 1, KindSpark = 2, KindNumber = 3, KindBlackout = 4, KindTelegraph = 5, KindTurbine = 6;
        const int KindBurst = 8, KindAreaMark = 9, KindSwing = 10, KindParticle = 11, KindAssemble = 12;

        readonly StampLayer _stamps;
        readonly GameAssets _assets;
        readonly GameDatabase _db;
        readonly List<Timed> _timed = new List<Timed>(512);
        readonly Sprite _px, _dot, _spark, _bolt, _gem, _gemBig, _heal, _arm, _smoke, _tv, _note, _shard, _coin, _plus, _bot0, _bot1, _bot2;
        readonly Sprite[] _explosion = new Sprite[4], _digits = new Sprite[10];
        readonly Sprite _bossNote, _bossWave, _bossOrb0, _bossOrb1, _bossSpark, _coxinha, _coxinhaSuprema;
        readonly Sprite[] _motoLeft = new Sprite[2], _motoRight = new Sprite[2];
        readonly int[] _orbitAbilities, _auraAbilities;
        int _numbers;

        public EffectsLevel Effects = EffectsLevel.High;
        public int StampCount => _stamps.Count;

        static readonly Color ArcColor = new Color32(0xb0, 0xf3, 0xff, 0xff), ArcCharged = new Color32(0xff, 0xe2, 0x7a, 0xff);
        static readonly Color AuraColor = new Color(0.25f, 0.88f, 1f, 0.55f), WarnColor = new Color(1f, 0.35f, 0.35f, 0.8f);

        public FxSystem(Transform parent, GameAssets assets, GameDatabase db)
        {
            _assets = assets;
            _db = db;
            _stamps = new StampLayer(parent, "Fx", 4000);
            _px = assets.Sprite("fx_px");
            _dot = assets.Sprite("fx_dot");
            _spark = assets.Sprite("fx_spark");
            _bolt = assets.Sprite("fx_hostile_bolt");
            _gem = assets.Sprite("fx_gem_xp");
            _gemBig = assets.Sprite("fx_gem_xp_big");
            _heal = assets.Sprite("fx_heal");
            _arm = assets.Sprite("weapon_orbit_arm");
            _tv = assets.Sprite("fx_tv");
            _note = assets.Sprite("fx_note");
            _shard = assets.Sprite("fx_shard");
            _coin = assets.Sprite("ally_coin_0");
            _plus = assets.Sprite("fx_plus");
            _bot0 = assets.Sprite("ally_botshooter_0");
            _bot1 = assets.Sprite("ally_botbrawler_0");
            _bot2 = assets.Sprite("ally_botdasher_0");
            _smoke = assets.Sprite("fx_smoke");
            _bossNote = assets.Sprite("fx_boss_note");
            _bossWave = assets.Sprite("fx_boss_wave");
            _bossOrb0 = assets.Sprite("fx_boss_orb_0");
            _bossOrb1 = assets.Sprite("fx_boss_orb_1");
            _bossSpark = assets.Sprite("fx_boss_spark");
            _coxinha = assets.Sprite("fx_coxinha");
            _coxinhaSuprema = assets.Sprite("fx_coxinha_suprema");
            for (int i = 0; i < 2; i++) { _motoLeft[i] = assets.Sprite("fx_moto_left_" + i); _motoRight[i] = assets.Sprite("fx_moto_right_" + i); }
            for (int i = 0; i < 4; i++) _explosion[i] = assets.Sprite("fx_explosion_" + i);
            for (int i = 0; i < 10; i++) _digits[i] = assets.Sprite("fx_digit_" + i);
            var orbit = new List<int>();
            var aura = new List<int>();
            for (int i = 0; i < db.AbilityCount; i++)
            {
                if (db.Ability(i).Def.behavior == "orbit") orbit.Add(i);
                if (db.Ability(i).Def.behavior == "aura") aura.Add(i);
            }
            _orbitAbilities = orbit.ToArray();
            _auraAbilities = aura.ToArray();
        }

        int MaxNumbers => Effects == EffectsLevel.High ? 64 : Effects == EffectsLevel.Medium ? 32 : 12;

        void Add(Timed t) { if (_timed.Count < 1500) _timed.Add(t); }

        public void OnCosmetic(in CosmeticEvent c, float now)
        {
            var a = new Vector2(c.X, c.Y);
            var b = new Vector2(c.X2, c.Y2);
            switch (c.Kind)
            {
                case CosmeticKind.ChainArc:
                    Add(new Timed { Kind = KindArc, A = a, B = b, Start = now, Life = 0.14f, Value = c.Value, Slot = c.A });
                    break;
                case CosmeticKind.Burst:
                    Add(new Timed { Kind = KindBurst, A = a, Start = now, Life = 0.32f, Value = c.Value, Slot = c.B });
                    if (Effects != EffectsLevel.Low) Particles(a, c.Value, (FxStyle)c.B, now);
                    break;
                case CosmeticKind.AreaMark:
                    Add(new Timed { Kind = KindAreaMark, A = a, B = new Vector2(c.X2, 0), Start = now, Life = Mathf.Clamp(c.Value, 0.05f, 3f), Slot = c.B });
                    break;
                case CosmeticKind.Swing:
                    Add(new Timed { Kind = KindSwing, A = a, B = b, Start = now, Life = 0.16f, Value = c.Value, Slot = c.B });
                    break;
                case CosmeticKind.Damage:
                    if (Effects != EffectsLevel.Low) Add(new Timed { Kind = KindSpark, A = a, Start = now, Life = 0.1f });
                    if (_numbers < MaxNumbers && c.Value >= 1f)
                    {
                        _numbers++;
                        Add(new Timed { Kind = KindNumber, A = a + new Vector2(Random.Range(-0.2f, 0.2f), 0.3f), Start = now, Life = 0.6f, Value = c.Value });
                    }
                    break;
                case CosmeticKind.EnemyTelegraph:
                    Add(new Timed { Kind = KindTelegraph, A = a, B = b, Start = now, Life = Mathf.Clamp(c.Value, 0.1f, 2f) });
                    break;
                case CosmeticKind.WaveStarted:
                    var sp = c.B >= 0 && c.B < _db.SpecialCount ? _db.Special(c.B) : null;
                    if (sp == null) break;
                    if (sp.Kind == SpecialKind.Blackout)
                        Add(new Timed { Kind = KindBlackout, A = a, Start = now, Life = Mathf.Max(0.2f, sp.Def.expandSeconds) + 0.25f, Value = c.Value, B = new Vector2(sp.Def.expandSeconds, 0) });
                    else
                        Add(new Timed { Kind = KindTurbine, A = a, Start = now, Life = Mathf.Clamp(c.Value, 0.5f, 20f), Slot = c.A });
                    break;
            }
        }

        /// <summary>Moto: aviso com a trajetória (piscando) e depois a travessia com poeira.</summary>
        void DrawMotos(MatchFeed feed, float now)
        {
            var list = feed.Motos;
            float t = feed.Elapsed;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var m = list[i];
                float travel = m.Speed > 0 ? m.Length / m.Speed : 0;
                if (t > m.GoAt + travel + 0.3f) { list.RemoveAt(i); continue; }
                var start = Layers.V(m.Start);
                var dir = new Vector2(Mathf.Cos(m.Angle), Mathf.Sin(m.Angle));
                if (t < m.GoAt)
                {
                    bool on = ((int)(now * 10) & 1) == 0;
                    var col = new Color(1f, 0.3f, 0.3f, on ? 0.9f : 0.45f);
                    var side = new Vector2(-dir.y, dir.x) * 0.9f;
                    int n = Mathf.RoundToInt(m.Length * 32f / 6f);
                    for (int k = 0; k <= n; k++)
                    {
                        var p = start + dir * (m.Length * k / n);
                        _stamps.Add(_px, Layers.Snap(p + side), col, Layers.Shadow + 6);
                        _stamps.Add(_px, Layers.Snap(p - side), col, Layers.Shadow + 6);
                    }
                    // setas indicando o sentido
                    for (int k = 1; k < 6; k++)
                    {
                        var p = start + dir * (m.Length * k / 6f + Mathf.Repeat(now * 3f, 1f));
                        _stamps.Add(_px, Layers.Snap(p + side * 0.5f - dir * 0.3f), col, Layers.Shadow + 6);
                        _stamps.Add(_px, Layers.Snap(p), col, Layers.Shadow + 6);
                        _stamps.Add(_px, Layers.Snap(p - side * 0.5f - dir * 0.3f), col, Layers.Shadow + 6);
                    }
                    continue;
                }
                float d = Mathf.Min(m.Length, (t - m.GoAt) * m.Speed);
                var at = start + dir * d;
                var frames = dir.x < 0 ? _motoLeft : _motoRight;
                _stamps.Add(frames[(int)(now * 14) & 1], Layers.Snap(at + new Vector2(0, 0.2f)), Color.white, Layers.Actor(at.y) + 2);
                // rastro de velocidade e poeira
                for (int k = 1; k <= 6; k++)
                {
                    var p = at - dir * (k * 0.35f);
                    _stamps.Add(k < 3 ? _px : _smoke, Layers.Snap(p + new Vector2(0, Random.Range(-0.15f, 0.15f))), new Color(0.85f, 0.8f, 0.7f, 0.8f - k * 0.12f), Layers.Fx);
                }
            }
        }

        Sprite HostileSprite(in HostileRender h, float now)
        {
            switch (h.Type)
            {
                case HostileKind.Note: return _bossNote ?? _bolt;
                case HostileKind.Wave: return _bossWave ?? _bolt;
                case HostileKind.Orb: return (((int)(now * 8) + h.Id) & 1) == 0 ? _bossOrb0 ?? _bolt : _bossOrb1 ?? _bolt;
                case HostileKind.Spark: return _bossSpark ?? _bolt;
                default: return _bolt;
            }
        }

        public void OnEnemyDied(Vector2 pos, int type, float now)
        {
            Add(new Timed { Kind = KindExplosion, A = pos, Start = now, Life = 0.25f, Value = type });
        }

        public void Tick(float now, MatchFeed feed, RenderFrame frame, PlayerViews players, float renderSeconds)
        {
            _stamps.Begin();
            int order = Layers.Fx;

            foreach (var kv in feed.Gems)
            {
                var g = kv.Value;
                if (g.Kind == PickupKind.Coxinha || g.Kind == PickupKind.CoxinhaSuprema)
                {
                    // coxinha flutuando com brilho
                    float fl = Mathf.Round(Mathf.Sin(now * 4f + kv.Key) * 2f) * Layers.Pixel;
                    bool supreme = g.Kind == PickupKind.CoxinhaSuprema;
                    _stamps.Add(supreme ? _coxinhaSuprema : _coxinha, Layers.Snap(new Vector2(g.Pos.X, g.Pos.Y + 0.15f)) + new Vector2(0, fl), Color.white, Layers.Gem + 2);
                    if (((int)(now * 5) + kv.Key) % 3 == 0)
                        _stamps.Add(_spark, Layers.Snap(new Vector2(g.Pos.X + Mathf.Sin(now * 7f) * 0.35f, g.Pos.Y + 0.45f)), supreme ? new Color(1f, 0.85f, 0.2f) : Color.white, Layers.Gem + 3);
                    continue;
                }
                var sprite = g.Kind == PickupKind.Heal ? _heal : g.Value >= 10 ? _gemBig : _gem;
                float bob = ((int)(now * 3 + kv.Key) & 1) * Layers.Pixel;
                _stamps.Add(sprite, Layers.Snap(new Vector2(g.Pos.X, g.Pos.Y + bob)), Color.white, Layers.Gem);
            }

            foreach (var h in frame.Hostile) _stamps.Add(HostileSprite(h, now), Layers.Snap(Layers.V(h.Pos)), Color.white, Layers.Hostile);

            DrawAbilities(feed, players, renderSeconds, now);
            DrawMotos(feed, now);

            _numbers = 0;
            for (int i = _timed.Count - 1; i >= 0; i--)
            {
                var t = _timed[i];
                float age = now - t.Start;
                if (age > t.Life || age < -1f) { _timed.RemoveAt(i); continue; }
                float k = age / t.Life;
                switch (t.Kind)
                {
                    case KindArc: DrawArc(t, now); break;
                    case KindBurst: DrawBurst(t, k); break;
                    case KindAreaMark: DrawAreaMark(t, k, now); break;
                    case KindSwing: DrawSwing(t, k); break;
                    case KindParticle:
                    {
                        var p = t.A + t.B * age + new Vector2(0, -1.5f * age * age);
                        var sprite = t.Value == 1 ? _coin : t.Value == 2 ? _note : t.Value == 3 ? _shard : t.Value == 4 ? _plus : _px;
                        _stamps.Add(sprite, Layers.Snap(p), new Color(1, 1, 1, 1f - k * 0.6f), order + 3);
                        break;
                    }
                    case KindAssemble: DrawAssemble(t, k); break;
                    case KindSpark: _stamps.Add(_spark, Layers.Snap(t.A), Color.white, order + 3); break;
                    case KindExplosion:
                        _stamps.Add(_explosion[Mathf.Clamp((int)(k * 4), 0, 3)], Layers.Snap(t.A), Color.white, order + 1);
                        break;
                    case KindNumber:
                        _numbers++;
                        DrawNumber(Mathf.RoundToInt(t.Value), t.A + new Vector2(0, 0.6f * Mathf.Min(1f, k * 1.6f)), k > 0.7f ? 1f - (k - 0.7f) / 0.3f : 1f, t.Value >= 30f);
                        break;
                    case KindBlackout: DrawBlackout(t, age); break;
                    case KindTelegraph: DrawDotted(t.A, t.B, WarnColor, 6, ((int)(now * 10) & 1) == 0); break;
                    case KindTurbine:
                    {
                        var pos = players.PositionOf(t.Slot);
                        if (pos.HasValue && Effects != EffectsLevel.Low && ((int)(now * 20) & 3) == 0)
                            Add(new Timed { Kind = KindSpark, A = pos.Value + new Vector2(Random.Range(-0.4f, 0.4f), -0.3f), Start = now, Life = 0.12f });
                        if (pos.HasValue) _stamps.Add(_smoke, Layers.Snap(pos.Value + new Vector2(0, -0.35f)), new Color(1, 1, 1, 0.5f), Layers.Shadow + 1);
                        break;
                    }
                }
            }
            _stamps.End();
        }

        void DrawAbilities(MatchFeed feed, PlayerViews players, float t, float now)
        {
            var loadouts = feed.Loadouts;
            if (loadouts == null) return;
            for (int s = 0; s < PlayerStore.MaxSlots; s++)
            {
                var l = loadouts.Slots[s];
                var snap = feed.Players[s];
                if (l == null || !snap.Present || snap.Status != PlayerStatus.Active) continue;
                var pos = players.PositionOf(s);
                if (!pos.HasValue) continue;
                if ((snap.Flags & PlayerSnap.FlagShield) != 0) DrawRing(pos.Value + new Vector2(0, 0.1f), 0.7f, new Color(0.25f, 0.88f, 1f, 0.85f), 3, now * 2f, Layers.Fx + 2);
                foreach (int ab in _orbitAbilities)
                {
                    int level = l.LevelOf(ab);
                    if (level <= 0) continue;
                    var a = _db.Ability(ab);
                    float period = Mathf.Max(0.1f, AbilityLevels.Value(a, level, StatId.Duration));
                    float radius = AbilityLevels.Value(a, level, StatId.Range);
                    int arms = Mathf.Max(1, (int)AbilityLevels.Value(a, level, StatId.Count));
                    float mult = a.Param("turbineArmMultiplier", 0f);
                    if (mult > 0 && (snap.Flags & PlayerSnap.FlagSpecialActive) != 0) arms = (int)(arms * mult);
                    arms = Mathf.Min(arms, 12);
                    float angle = Mathf.PI * 2f * t / period;
                    float spin = ((int)(now * 16) & 3) * 90f;
                    for (int k = 0; k < arms; k++)
                    {
                        float ang = angle + Mathf.PI * 2f * k / arms;
                        var p = pos.Value + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
                        _stamps.Add(_arm, Layers.Snap(p), Color.white, Layers.Actor(p.y) + 1, spin);
                    }
                }
                foreach (int ab in _auraAbilities)
                {
                    int level = l.LevelOf(ab);
                    if (level <= 0) continue;
                    float radius = AbilityLevels.Value(_db.Ability(ab), level, StatId.Range);
                    DrawRing(pos.Value, radius, AuraColor, 5, Effects == EffectsLevel.Low ? 0f : now * 1.5f, Layers.Shadow + 2);
                }
            }
        }

        /// <summary>Cor de cada estilo de efeito das armas (FxStyle).</summary>
        public static Color StyleColor(int style)
        {
            switch ((FxStyle)style)
            {
                case FxStyle.Charged: return ArcCharged;
                case FxStyle.Pop: return new Color32(0xb0, 0xf3, 0xff, 0xff);
                case FxStyle.Coins: return new Color32(0xff, 0xd2, 0x3f, 0xff);
                case FxStyle.Math: return new Color32(0xff, 0x7e, 0xb6, 0xff);
                case FxStyle.Food: return new Color32(0xff, 0xb0, 0x66, 0xff);
                case FxStyle.Impact: return new Color32(0xee, 0xf1, 0xf4, 0xff);
                case FxStyle.Energy: return new Color32(0xff, 0x3d, 0xf0, 0xff);
                case FxStyle.Paper: return new Color32(0xff, 0xe2, 0x7a, 0xff);
                case FxStyle.Tech: return new Color32(0x93, 0xc6, 0xe6, 0xff);
                case FxStyle.Sound: return new Color32(0xff, 0xe2, 0x7a, 0xff);
                case FxStyle.Glass: return new Color32(0x7f, 0xd9, 0x8f, 0xff);
                case FxStyle.Insulin: return new Color32(0xff, 0x7a, 0x1a, 0xff);
                case FxStyle.Robot: return new Color32(0x3f, 0xe0, 0xff, 0xff);
                case FxStyle.Drift: return new Color32(0xc9, 0xcd, 0xd2, 0xff);
                case FxStyle.Circuit: return new Color32(0xb8, 0xe0, 0x2c, 0xff);
                case FxStyle.Storm: return new Color32(0xa0, 0x8c, 0xff, 0xff);
                case FxStyle.Stink: return new Color32(0x9a, 0xd0, 0x3a, 0xff);
                case FxStyle.Heal: return new Color32(0x4d, 0xff, 0x88, 0xff);
                case FxStyle.Shield: return new Color32(0x3f, 0xe0, 0xff, 0xff);
                case FxStyle.Dodge: return new Color32(0xc8, 0xf0, 0xff, 0xff);
                case FxStyle.Fiesta: return new Color32(0xff, 0xd2, 0x3f, 0xff);
                case FxStyle.Heat: return new Color32(0xff, 0x7a, 0x1a, 0xff);
                default: return ArcColor;
            }
        }

        void Particles(Vector2 at, float radius, FxStyle style, float now)
        {
            int kind = style == FxStyle.Coins ? 1 : style == FxStyle.Sound ? 2 : style == FxStyle.Glass ? 3 : style == FxStyle.Math || style == FxStyle.Fiesta || style == FxStyle.Heal ? 4 : 0;
            int n = kind == 0 ? 0 : Effects == EffectsLevel.High ? 6 : 3;
            for (int i = 0; i < n; i++)
            {
                float ang = Random.value * Mathf.PI * 2f, sp = Random.Range(0.8f, 1.6f) * Mathf.Max(1f, radius);
                Add(new Timed { Kind = KindParticle, A = at, B = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) + 0.6f) * sp, Start = now, Life = 0.5f, Value = kind });
            }
        }

        /// <summary>Anel que se expande até o raio real do dano (alcance sempre visível).</summary>
        void DrawBurst(in Timed t, float k)
        {
            var col = StyleColor(t.Slot);
            float r = t.Value * Mathf.Lerp(0.35f, 1f, Mathf.Sqrt(k));
            col.a = 1f - k * 0.7f;
            DrawRing(t.A, r, col, 3, 0f, Layers.Fx + 5);
            if (t.Value > 1f && Effects != EffectsLevel.Low) DrawRing(t.A, r * 0.7f, new Color(col.r, col.g, col.b, col.a * 0.5f), 6, 0.2f, Layers.Fx + 5);
            if ((FxStyle)t.Slot == FxStyle.Sound && r > 0.8f) DrawRing(t.A, r - 0.35f, new Color(1, 1, 1, col.a * 0.6f), 5, 0.4f, Layers.Fx + 5);
        }

        /// <summary>Aviso no chão: círculo piscando do tamanho da área e anel interno que fecha até o impacto.</summary>
        void DrawAreaMark(in Timed t, float k, float now)
        {
            float r = t.B.x;
            var warn = new Color(1f, 0.35f, 0.35f, ((int)(now * 12) & 1) == 0 ? 0.9f : 0.5f);
            DrawRing(t.A, r, warn, 4, 0f, Layers.Shadow + 4);
            var col = StyleColor(t.Slot);
            DrawRing(t.A, r * (1f - k), new Color(col.r, col.g, col.b, 0.8f), 5, 0f, Layers.Shadow + 4);
            if ((FxStyle)t.Slot == FxStyle.Tech && _tv != null)
            {
                // a TV cai do alto e chega no centro no momento do impacto
                var p = t.A + new Vector2(0, 6f * (1f - k) * (1f - k));
                _stamps.Add(_tv, Layers.Snap(p), Color.white, Layers.Fx + 6);
            }
        }

        /// <summary>Golpe em cone: arcos de pixels na abertura e no alcance reais.</summary>
        void DrawSwing(in Timed t, float k)
        {
            float reach = t.B.magnitude;
            if (reach < 1e-3f) return;
            float baseAng = Mathf.Atan2(t.B.y, t.B.x), half = t.Value * Mathf.Deg2Rad * 0.5f;
            var col = StyleColor(t.Slot);
            col.a = 1f - k;
            int n = Mathf.Clamp(Mathf.RoundToInt(half * 2f * reach * 32f / 3f), 4, 80);
            for (int ring = 0; ring < 3; ring++)
            {
                float rr = reach * (0.55f + 0.2f * ring) * Mathf.Lerp(0.8f, 1f, k);
                for (int i = 0; i <= n; i++)
                {
                    float a = baseAng - half + 2f * half * i / n;
                    _stamps.Add(_px, Layers.Snap(t.A + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr), ring == 2 ? col : new Color(1, 1, 1, col.a), Layers.Fx + 4);
                }
            }
        }

        /// <summary>Montagem do Cabazord Master: os três robôs giram e se juntam no Badotti.</summary>
        public void Assemble(Vector2 at, float now) => Add(new Timed { Kind = KindAssemble, A = at, Start = now, Life = 1.1f });

        void DrawAssemble(in Timed t, float k)
        {
            var bots = new[] { _bot0, _bot1, _bot2 };
            float r = 2.2f * (1f - k);
            for (int i = 0; i < 3; i++)
            {
                float a = k * 10f + i * Mathf.PI * 2f / 3f;
                _stamps.Add(bots[i], Layers.Snap(t.A + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r + new Vector2(0, 0.6f)), Color.white, Layers.Fx + 6);
            }
            if (k > 0.75f) DrawRing(t.A, (k - 0.75f) * 12f, new Color(0.25f, 0.88f, 1f, 1f - k), 3, 0f, Layers.Fx + 6);
        }

        void DrawArc(in Timed t, float now)
        {
            var color = StyleColor((int)t.Value);
            var d = t.B - t.A;
            float len = d.magnitude;
            if (len < 1e-3f) return;
            var n = new Vector2(-d.y, d.x) / len;
            int steps = Mathf.Clamp(Mathf.CeilToInt(len * 16f), 2, 160);
            var rng = new System.Random((int)(t.Start * 1000) ^ (int)(now * 30));
            float off = 0f;
            for (int i = 0; i <= steps; i++)
            {
                float u = (float)i / steps;
                float edge = Mathf.Sin(u * Mathf.PI);
                off = Mathf.Clamp(off + (float)(rng.NextDouble() - 0.5) * 0.18f, -0.35f, 0.35f);
                var p = t.A + d * u + n * off * edge;
                _stamps.Add(_px, Layers.Snap(p), color, Layers.Fx + 4);
            }
        }

        void DrawRing(Vector2 center, float radius, Color color, int spacingPx, float phase, int order)
        {
            float circumference = Mathf.PI * 2f * radius * 32f;
            int n = Mathf.Clamp(Mathf.RoundToInt(circumference / spacingPx), 8, 400);
            for (int i = 0; i < n; i++)
            {
                float ang = phase + Mathf.PI * 2f * i / n;
                _stamps.Add(_px, Layers.Snap(center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius), color, order);
            }
        }

        void DrawBlackout(in Timed t, float age)
        {
            float expand = Mathf.Max(0.05f, t.B.x);
            float r = t.Value * Mathf.Clamp01(age / expand);
            float fade = age > expand ? 1f - (age - expand) / Mathf.Max(0.05f, t.Life - expand) : 1f;
            DrawRing(t.A, r, new Color(0.7f, 0.95f, 1f, 0.9f * fade), 3, 0f, Layers.Fx + 5);
            if (r > 0.6f) DrawRing(t.A, r - 0.25f, new Color(1f, 0.89f, 0.48f, 0.6f * fade), 6, 0.3f, Layers.Fx + 5);
        }

        void DrawDotted(Vector2 a, Vector2 b, Color color, int spacingPx, bool on)
        {
            if (!on) return;
            var d = b - a;
            int n = Mathf.Clamp(Mathf.RoundToInt(d.magnitude * 32f / spacingPx), 1, 120);
            for (int i = 1; i <= n; i++) _stamps.Add(_px, Layers.Snap(a + d * ((float)i / n)), color, Layers.Fx);
        }

        void DrawNumber(int value, Vector2 pos, float alpha, bool big)
        {
            if (value <= 0) return;
            value = Mathf.Min(value, 9999);
            int digits = value >= 1000 ? 4 : value >= 100 ? 3 : value >= 10 ? 2 : 1;
            float w = 4 * Layers.Pixel;
            var start = Layers.Snap(pos - new Vector2((digits - 1) * w / 2f, 0));
            var color = big ? new Color(1f, 0.89f, 0.48f, alpha) : new Color(1, 1, 1, alpha);
            int div = 1;
            for (int i = 1; i < digits; i++) div *= 10;
            for (int i = 0; i < digits; i++)
            {
                int d = value / div % 10;
                div /= 10;
                _stamps.Add(_digits[d], start + new Vector2(i * w, 0), color, Layers.Numbers);
            }
        }
    }
}
