using System.Collections.Generic;
using ES.Sim;
using UnityEngine;

namespace ES.Game
{
    /// <summary>
    /// Bosses: animação por estado (andar em 8 direções, preparação, ataque, especial, fúria, dano),
    /// NORMAL/BRAVO, morte e desaparecimento no lugar da derrota, e as áreas sinalizadas no chão.
    /// </summary>
    public sealed class BossViews
    {
        static readonly string[] Dir8 = { "down", "up", "left", "right", "downleft", "downright", "upleft", "upright" };

        readonly GameAssets _assets;
        readonly GameDatabase _db;
        readonly StampLayer _stamps;
        readonly Transform _root;
        readonly int[] _bossOfType;
        readonly Dictionary<string, Sprite[]> _frames = new Dictionary<string, Sprite[]>();
        readonly Dictionary<ushort, SpriteRenderer> _live = new Dictionary<ushort, SpriteRenderer>(4);
        readonly HashSet<ushort> _seen = new HashSet<ushort>();
        readonly List<ushort> _gone = new List<ushort>(4);
        readonly List<Corpse> _corpses = new List<Corpse>(4);
        readonly Sprite _px;

        struct Corpse { public SpriteRenderer R; public string Boss; public Vector2 Pos; public float Start; }

        const float DeathFps = 5f, VanishFps = 4f;

        public BossViews(Transform parent, GameAssets assets, GameDatabase db)
        {
            _assets = assets;
            _db = db;
            _root = new GameObject("Bosses").transform;
            _root.SetParent(parent, false);
            _stamps = new StampLayer(parent, "BossHazards", 1500);
            _px = assets.Sprite("fx_px");
            _bossOfType = new int[db.EnemyCount];
            for (int t = 0; t < db.EnemyCount; t++) _bossOfType[t] = db.BossOfEnemy(t);
        }

        public bool IsBoss(int type) => type >= 0 && type < _bossOfType.Length && _bossOfType[type] >= 0;

        Sprite[] Frames(string boss, string anim, string dir)
        {
            string key = boss + "|" + anim + "|" + dir;
            if (_frames.TryGetValue(key, out var f)) return f;
            var list = new List<Sprite>();
            for (int i = 0; i < 8; i++)
            {
                var s = _assets.Sprite($"boss_{boss}_{anim}_{dir}_{i}");
                if (s == null) break;
                list.Add(s);
            }
            f = list.Count == 0 && dir != "down" ? Frames(boss, anim, "down") : list.ToArray();
            _frames[key] = f;
            return f;
        }

        static Sprite Pick(Sprite[] f, float t, float fps, bool loop = true)
        {
            if (f == null || f.Length == 0) return null;
            int i = Mathf.FloorToInt(t * fps);
            return f[loop ? ((i % f.Length) + f.Length) % f.Length : Mathf.Clamp(i, 0, f.Length - 1)];
        }

        static bool Special(BossAttack a) =>
            a == BossAttack.Concerto || a == BossAttack.Refrao || a == BossAttack.Sobrecarga || a == BossAttack.Tempestade || a == BossAttack.Orbes;

        public void Sync(MatchFeed feed, RenderFrame frame, float now)
        {
            _seen.Clear();
            foreach (var e in frame.Enemies)
            {
                if (!IsBoss(e.Type)) continue;
                _seen.Add(e.Id);
                if (!_live.TryGetValue(e.Id, out var r))
                {
                    var go = new GameObject("boss");
                    go.transform.SetParent(_root, false);
                    r = go.AddComponent<SpriteRenderer>();
                    _live[e.Id] = r;
                }
                string boss = _db.Boss(_bossOfType[e.Type]).Def.id;
                feed.Bosses.TryGetValue(e.Id, out var info);
                bool bravo = info != null && info.Bravo;
                string mood = bravo ? "_bravo" : "";
                string dir = Dir8[info != null ? Mathf.Clamp(info.Dir, 0, 7) : 0];
                var state = info?.State ?? BossState.Chase;
                float since = info != null ? feed.Elapsed - info.ActionAt : now;
                Sprite s;
                var color = Color.white;
                switch (state)
                {
                    case BossState.Entering:
                    {
                        // entrada: aparece de baixo para cima (quadros do desaparecimento ao contrário) e pisca
                        float k = info != null ? Mathf.Clamp01((feed.Elapsed - info.SpawnedAt) / 1.6f) : 1f;
                        var vanish = Frames(boss, "vanish", "down");
                        s = k < 0.75f && vanish.Length > 0 ? vanish[Mathf.Clamp(vanish.Length - 1 - Mathf.FloorToInt(k / 0.75f * vanish.Length), 0, vanish.Length - 1)] : Pick(Frames(boss, "idle", "down"), now, 4f);
                        color = ((int)(now * 14) & 1) == 0 ? new Color(0.7f, 0.9f, 1f) : Color.white;
                        break;
                    }
                    case BossState.Rage:
                        s = info != null && feed.Elapsed - info.BravoAt < 0.35f ? Pick(Frames(boss, "heavy", "down"), now, 8f) : Pick(Frames(boss, "rage", "down"), now, 6f);
                        break;
                    case BossState.Windup:
                        s = info != null && (info.Attack == BossAttack.Sobrecarga || info.Attack == BossAttack.Cadeia || info.Attack == BossAttack.Raio || info.Attack == BossAttack.Ressonancia)
                            ? Pick(Frames(boss, "charge", "down"), now, 6f)
                            : Pick(Frames(boss, "attack" + mood, "down"), 0f, 1f);
                        break;
                    case BossState.Attack:
                        s = Pick(Frames(boss, (info != null && Special(info.Attack) ? "special" : "attack") + mood, "down"), since, 10f);
                        break;
                    case BossState.Recover:
                        s = Pick(Frames(boss, "idle" + mood, "down"), now, 3f);
                        break;
                    default:
                        s = Pick(Frames(boss, "walk" + mood, dir), now + e.Id * 0.1f, 7f);
                        break;
                }
                if ((e.Flags & EnemyFlags.HitFlash) != 0 && state != BossState.Entering && ((int)(now * 20) & 3) == 0)
                    s = Pick(Frames(boss, "hurt", "down"), 0f, 1f) ?? s;
                r.sprite = s;
                r.color = color;
                var p = Layers.V(e.Pos);
                r.transform.localPosition = Layers.Snap(p + new Vector2(0, -0.7f));
                r.sortingOrder = Layers.Actor(p.y);
            }
            _gone.Clear();
            foreach (var kv in _live) if (!_seen.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var id in _gone)
            {
                Object.Destroy(_live[id].gameObject);
                _live.Remove(id);
            }

            for (int i = _corpses.Count - 1; i >= 0; i--)
            {
                var c = _corpses[i];
                float age = now - c.Start;
                var death = Frames(c.Boss, "death", "down");
                var vanish = Frames(c.Boss, "vanish", "down");
                float deathTime = death.Length / DeathFps, vanishTime = vanish.Length / VanishFps;
                if (age > deathTime + 0.8f + vanishTime)
                {
                    Object.Destroy(c.R.gameObject);
                    _corpses.RemoveAt(i);
                    continue;
                }
                c.R.sprite = age < deathTime ? Pick(death, age, DeathFps, false)
                    : age < deathTime + 0.8f ? death[death.Length - 1]
                    : Pick(vanish, age - deathTime - 0.8f, VanishFps, false);
            }

            DrawHazards(feed, now);
        }

        /// <summary>Morte exclusiva no lugar da derrota, depois o desaparecimento.</summary>
        public void OnDefeated(int boss, Vector2 pos, float now)
        {
            if (boss < 0 || boss >= _db.BossCount) return;
            var go = new GameObject("boss-dead");
            go.transform.SetParent(_root, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.transform.localPosition = Layers.Snap(pos + new Vector2(0, -0.7f));
            r.sortingOrder = Layers.Actor(pos.y);
            _corpses.Add(new Corpse { R = r, Boss = _db.Boss(boss).Def.id, Pos = pos, Start = now });
        }

        // ---------------- áreas sinalizadas ----------------

        void DrawHazards(MatchFeed feed, float now)
        {
            _stamps.Begin();
            var list = feed.Hazards;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var h = list[i];
                if (feed.Elapsed > h.FireAt + 0.05f) { list.RemoveAt(i); continue; }
                float k = h.FireAt > h.CreatedAt ? Mathf.Clamp01((feed.Elapsed - h.CreatedAt) / (h.FireAt - h.CreatedAt)) : 1f;
                var warn = new Color(1f, 0.3f, 0.3f, ((int)(now * (k > 0.7f ? 18 : 9)) & 1) == 0 ? 0.95f : 0.55f);
                var col = FxSystem.StyleColor((int)h.Style);
                col.a = 0.85f;
                var c = Layers.V(h.Pos);
                switch (h.Shape)
                {
                    case HazardShape.Circle:
                        Ring(c, h.Size, warn, 3);
                        Ring(c, h.Size * k, col, 4);
                        if (h.Size > 1.5f) Ring(c, h.Size * 0.5f, new Color(warn.r, warn.g, warn.b, 0.3f), 8);
                        break;
                    case HazardShape.Ring:
                        Ring(c, h.Size, warn, 3);
                        Ring(c, h.Extra, warn, 3);
                        Ring(c, Mathf.Lerp(h.Extra, h.Size, k), col, 4);
                        break;
                    case HazardShape.Line:
                    {
                        var d = new Vector2(Mathf.Cos(h.Extra), Mathf.Sin(h.Extra));
                        var n = new Vector2(-d.y, d.x) * (h.Width * 0.5f);
                        Line(c + n, c + n + d * h.Size, warn, 3);
                        Line(c - n, c - n + d * h.Size, warn, 3);
                        Line(c, c + d * (h.Size * k), col, 2);
                        break;
                    }
                }
            }
            _stamps.End();
        }

        void Ring(Vector2 center, float radius, Color color, int spacingPx)
        {
            if (radius <= 0.05f) return;
            int n = Mathf.Clamp(Mathf.RoundToInt(Mathf.PI * 2f * radius * 32f / spacingPx), 8, 360);
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * 2f * i / n;
                _stamps.Add(_px, Layers.Snap(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius), color, Layers.Shadow + 6);
            }
        }

        void Line(Vector2 a, Vector2 b, Color color, int spacingPx)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt((b - a).magnitude * 32f / spacingPx), 2, 400);
            for (int i = 0; i <= n; i++) _stamps.Add(_px, Layers.Snap(Vector2.Lerp(a, b, (float)i / n)), color, Layers.Shadow + 6);
        }

        public void Clear()
        {
            foreach (var kv in _live) Object.Destroy(kv.Value.gameObject);
            _live.Clear();
            foreach (var c in _corpses) Object.Destroy(c.R.gameObject);
            _corpses.Clear();
        }
    }
}
