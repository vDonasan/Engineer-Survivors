using ES.Sim;
using UnityEngine;

namespace ES.Game
{
    /// <summary>Personagens: animação por estado (parado, andando, ataque, especial, dano, caído, vitória).</summary>
    public sealed class PlayerViews
    {
        const float FeetOffset = -0.42f;

        sealed class View
        {
            public SpriteRenderer Body, Shadow, Marker;
            public CharacterSprites Sprites;
            public int Dir;
            public float HurtAt = -9, SpecialAt = -9, AttackAt = -9, DownedAt = -9;
            public PlayerStatus LastStatus;
            public Vector2 LastPos;
            public bool HasPos;
        }

        readonly View[] _views = new View[PlayerStore.MaxSlots];
        readonly GameAssets _assets;

        public PlayerViews(Transform parent, GameAssets assets, MatchConfig cfg)
        {
            _assets = assets;
            for (int s = 0; s < PlayerStore.MaxSlots; s++)
            {
                if (cfg?.Roster == null || s >= cfg.Roster.Length) continue;
                var root = new GameObject("Player" + s).transform;
                root.SetParent(parent, false);
                var v = new View { Sprites = CharacterSprites.For(assets, cfg.Roster[s].SpecializationId) };
                v.Shadow = NewRenderer(root, "shadow", assets.Sprite("fx_shadow"), Layers.Shadow);
                v.Shadow.color = new Color(1, 1, 1, 0.6f);
                v.Body = NewRenderer(root, "body", null, 0);
                v.Marker = NewRenderer(root, "marker", assets.Sprite("fx_dot"), Layers.Fx);
                v.Marker.color = Layers.PlayerColors[s];
                _views[s] = v;
            }
        }

        static SpriteRenderer NewRenderer(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        public void OnHit(int slot, float now) { if (Valid(slot)) _views[slot].HurtAt = now; }
        public void OnSpecial(int slot, float now) { if (Valid(slot)) _views[slot].SpecialAt = now; }
        public void OnAttack(int slot, float now) { if (Valid(slot)) _views[slot].AttackAt = now; }

        bool Valid(int slot) => slot >= 0 && slot < _views.Length && _views[slot] != null;

        /// <summary>Posição desenhada (a do jogador local vem da previsão).</summary>
        public void Sync(MatchFeed feed, RenderFrame frame, int localSlot, Vec2? localPos, float now)
        {
            for (int s = 0; s < _views.Length; s++)
            {
                var v = _views[s];
                if (v == null) continue;
                var snap = feed.Players[s];
                bool show = snap.Present && snap.Status != PlayerStatus.Inactive;
                v.Body.enabled = v.Shadow.enabled = show;
                v.Marker.enabled = show && snap.Status == PlayerStatus.Active;
                if (!show) continue;

                var pos = Layers.V(s == localSlot && localPos.HasValue ? localPos.Value : frame.PlayerPositions[s]);
                var vel = v.HasPos ? (pos - v.LastPos) / Mathf.Max(1e-3f, Time.deltaTime) : Vector2.zero;
                v.LastPos = pos;
                v.HasPos = true;
                bool moving = vel.sqrMagnitude > 0.25f || Layers.V(snap.Vel).sqrMagnitude > 0.25f;
                v.Dir = Layers.Dir(moving ? (vel.sqrMagnitude > 0.25f ? vel : Layers.V(snap.Vel)) : Layers.V(snap.Facing), v.Dir);

                if (snap.Status == PlayerStatus.Downed && v.LastStatus != PlayerStatus.Downed) v.DownedAt = now;
                v.LastStatus = snap.Status;

                Sprite sprite;
                var c = v.Sprites;
                if (snap.Status == PlayerStatus.Downed) sprite = CharacterSprites.Once(c.Downed, now - v.DownedAt, 8);
                else if (feed.Phase == MatchPhase.Victory) sprite = CharacterSprites.Loop(c.Victory, now, 3);
                else if (now - v.HurtAt < 0.25f) sprite = CharacterSprites.Loop(c.Hurt, now - v.HurtAt, 8);
                else if (now - v.SpecialAt < 0.45f) sprite = CharacterSprites.Once(c.Special, now - v.SpecialAt, 7);
                else if (moving) sprite = CharacterSprites.Loop(c.Walk[v.Dir], now, 8);
                else if (now - v.AttackAt < 0.3f) sprite = CharacterSprites.Once(c.Attack, now - v.AttackAt, 10);
                else sprite = CharacterSprites.Loop(c.Idle[v.Dir], now, 2);
                v.Body.sprite = sprite;

                bool invulnBlink = (snap.Flags & PlayerSnap.FlagInvulnerable) != 0 && snap.Status == PlayerStatus.Active && ((int)(now * 12) & 1) == 0;
                v.Body.color = invulnBlink ? new Color(1, 1, 1, 0.55f) : (snap.Flags & PlayerSnap.FlagSpecialActive) != 0 ? new Color(1f, 0.92f, 0.75f) : Color.white;

                var feet = Layers.Snap(pos + new Vector2(0, FeetOffset));
                int order = Layers.Actor(pos.y);
                v.Body.transform.localPosition = feet;
                v.Body.sortingOrder = order;
                v.Shadow.transform.localPosition = feet + new Vector2(0, Layers.Pixel * 2);
                v.Marker.transform.localPosition = Layers.Snap(pos + new Vector2(0, 0.72f));
            }
        }

        public Vector2? PositionOf(int slot) => Valid(slot) && _views[slot].HasPos ? _views[slot].LastPos : (Vector2?)null;
    }
}
