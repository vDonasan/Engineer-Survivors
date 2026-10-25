using System.Collections.Generic;
using ES.Sim;
using UnityEngine;

namespace ES.Game
{
    /// <summary>Inimigos por ID com pool: 2 quadros, elite, piscar branco ao levar dano, atordoado, aviso de disparo.</summary>
    public sealed class EnemyViews
    {
        // [tipo][variante: 0 normal, 1 elite, 2 piscar, 3 elite piscar][quadro]
        readonly Sprite[][][] _sprites;
        readonly float[] _feet;
        readonly Dictionary<ushort, SpriteRenderer> _live = new Dictionary<ushort, SpriteRenderer>(512);
        readonly HashSet<ushort> _seen = new HashSet<ushort>();
        readonly List<ushort> _gone = new List<ushort>(64);
        readonly ViewPool<SpriteRenderer> _pool;
        static readonly Color StunTint = new Color(0.55f, 0.75f, 1f);
        static readonly Color WarnTint = new Color(1f, 0.55f, 0.55f);

        public int Count => _live.Count;

        public EnemyViews(Transform parent, GameAssets assets, GameDatabase db)
        {
            var root = new GameObject("Enemies").transform;
            root.SetParent(parent, false);
            _pool = new ViewPool<SpriteRenderer>(() =>
            {
                var go = new GameObject("e");
                go.transform.SetParent(root, false);
                return go.AddComponent<SpriteRenderer>();
            });
            _sprites = new Sprite[db.EnemyCount][][];
            _feet = new float[db.EnemyCount];
            string[] variants = { "", "elite_", "flash_", "elite_flash_" };
            for (int t = 0; t < db.EnemyCount; t++)
            {
                string id = db.Enemy(t).Def.id;
                _sprites[t] = new Sprite[4][];
                for (int v = 0; v < 4; v++)
                    _sprites[t][v] = new[] { assets.Sprite($"enemy_{id}_{variants[v]}0"), assets.Sprite($"enemy_{id}_{variants[v]}1") };
                var s = _sprites[t][0][0];
                _feet[t] = s != null ? -s.bounds.size.y * 0.42f : -0.3f;
            }
        }

        public void Sync(List<EnemyRender> enemies, float now)
        {
            _seen.Clear();
            foreach (var e in enemies)
            {
                _seen.Add(e.Id);
                if (!_live.TryGetValue(e.Id, out var r))
                {
                    r = _pool.Get();
                    _live[e.Id] = r;
                }
                int type = e.Type < _sprites.Length ? e.Type : 0;
                bool elite = (e.Flags & EnemyFlags.Elite) != 0;
                bool flash = (e.Flags & EnemyFlags.HitFlash) != 0;
                int variant = (elite ? 1 : 0) + (flash ? 2 : 0);
                int frame = ((int)(now * 5f) + e.Id) & 1;
                r.sprite = _sprites[type][variant][frame];
                if ((e.Flags & EnemyFlags.Stunned) != 0) r.color = ((int)(now * 10) & 1) == 0 ? StunTint : Color.white;
                else if ((e.Flags & EnemyFlags.Telegraphing) != 0) r.color = ((int)(now * 12) & 1) == 0 ? WarnTint : Color.white;
                else r.color = Color.white;
                var p = Layers.V(e.Pos);
                r.transform.localPosition = Layers.Snap(p + new Vector2(0, _feet[type]));
                r.sortingOrder = Layers.Actor(p.y);
            }
            _gone.Clear();
            foreach (var kv in _live) if (!_seen.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var id in _gone)
            {
                _pool.Release(_live[id]);
                _live.Remove(id);
            }
        }
    }
}
