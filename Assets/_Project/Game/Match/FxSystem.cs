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

        const int KindArc = 0, KindExplosion = 1, KindSpark = 2, KindNumber = 3, KindBlackout = 4, KindTelegraph = 5, KindTurbine = 6, KindLaser = 7;

        readonly StampLayer _stamps;
        readonly GameAssets _assets;
        readonly GameDatabase _db;
        readonly List<Timed> _timed = new List<Timed>(512);
        readonly Sprite _px, _dot, _spark, _bolt, _gem, _gemBig, _heal, _arm, _laser, _smoke;
        readonly Sprite[] _explosion = new Sprite[4], _digits = new Sprite[10];
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
            _laser = assets.Sprite("fx_laser");
            _smoke = assets.Sprite("fx_smoke");
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
                case CosmeticKind.ProjectileFired:
                    Add(new Timed { Kind = KindLaser, A = a, B = b, Start = now, Life = Mathf.Clamp(c.Value, 0.05f, 3f), Slot = c.A });
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
                var sprite = g.Kind == PickupKind.Heal ? _heal : g.Value >= 10 ? _gemBig : _gem;
                float bob = ((int)(now * 3 + kv.Key) & 1) * Layers.Pixel;
                _stamps.Add(sprite, Layers.Snap(new Vector2(g.Pos.X, g.Pos.Y + bob)), Color.white, Layers.Gem);
            }

            foreach (var h in frame.Hostile) _stamps.Add(_bolt, Layers.Snap(Layers.V(h.Pos)), Color.white, Layers.Hostile);

            DrawAbilities(feed, players, renderSeconds, now);

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
                    case KindLaser:
                    {
                        var p = t.A + t.B * age;
                        bool horizontal = Mathf.Abs(t.B.x) >= Mathf.Abs(t.B.y);
                        _stamps.Add(_laser, Layers.Snap(p), Color.white, order + 2, horizontal ? 0f : 90f);
                        break;
                    }
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

        void DrawArc(in Timed t, float now)
        {
            var color = t.Value > 0 ? ArcCharged : ArcColor;
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
