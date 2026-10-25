using System.Collections.Generic;
using UnityEngine;

namespace ES.Game
{
    /// <summary>
    /// Carimbos de sprite redesenhados a cada quadro (arcos, anéis, números, faíscas).
    /// Mantém um conjunto fixo de SpriteRenderers e só liga/desliga os que mudam de uso.
    /// </summary>
    public sealed class StampLayer
    {
        readonly Transform _root;
        readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>(1024);
        readonly int _max;
        int _used, _usedLastFrame;

        public int Count => _used;
        public int Max => _max;

        public StampLayer(Transform parent, string name, int max)
        {
            _root = new GameObject(name).transform;
            _root.SetParent(parent, false);
            _max = max;
        }

        public void Begin() => _used = 0;

        public bool Add(Sprite sprite, Vector2 pos, Color color, int order, float rotation = 0f, bool flipX = false)
        {
            if (sprite == null || _used >= _max) return false;
            SpriteRenderer r;
            if (_used < _renderers.Count) r = _renderers[_used];
            else
            {
                var go = new GameObject("s");
                go.transform.SetParent(_root, false);
                r = go.AddComponent<SpriteRenderer>();
                _renderers.Add(r);
            }
            if (_used >= _usedLastFrame) r.gameObject.SetActive(true);
            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = order;
            r.flipX = flipX;
            var t = r.transform;
            t.localPosition = new Vector3(pos.x, pos.y, 0f);
            t.localRotation = rotation == 0f ? Quaternion.identity : Quaternion.Euler(0, 0, rotation);
            _used++;
            return true;
        }

        public void End()
        {
            for (int i = _used; i < _usedLastFrame && i < _renderers.Count; i++) _renderers[i].gameObject.SetActive(false);
            _usedLastFrame = _used;
        }
    }
}
