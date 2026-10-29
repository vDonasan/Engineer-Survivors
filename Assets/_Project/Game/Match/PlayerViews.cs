using ES.Sim;
using UnityEngine;

namespace ES.Game
{
    /// <summary>
    /// Personagens: animação por estado (parado, andando, ataque, especial, dano, caído, vitória) e as camadas visuais
    /// dos itens e eventos — acessórios presos ao ponto da cabeça de cada quadro (seguem todas as direções e animações),
    /// estados temporários (Tequila, Jalapeño, Iogurte, Bolha...) e emotes sobre a cabeça.
    /// </summary>
    public sealed class PlayerViews
    {
        const float FeetOffset = -0.42f;
        const float Px = 1f / 32f;

        enum Acc { Hat, Shirt, Board, Carrot, Blush, Count }
        static readonly string[] AccIds = { "chapeu", "camiseta", "protoboard", "cenoura", "rubor" };

        sealed class View
        {
            public SpriteRenderer Body, Shadow, Marker;
            public readonly SpriteRenderer[] Acc = new SpriteRenderer[(int)PlayerViews.Acc.Count];
            public CharacterSprites Sprites;
            public int Dir;
            public float HurtAt = -9, SpecialAt = -9, AttackAt = -9, DownedAt = -9;
            public PlayerStatus LastStatus;
            public Vector2 LastPos;
            public bool HasPos;
            public byte LastLook;
            public float EuphoriaAt = -9, StepAt;
            public string Emote;
            public float EmoteUntil;
            public readonly Vector2[] Trail = new Vector2[4];
            public int TrailHead;
            public float TrailAt;
        }

        readonly View[] _views = new View[PlayerStore.MaxSlots];
        readonly GameAssets _assets;
        readonly StampLayer _fx;
        readonly Sprite[][] _acc = new Sprite[(int)Acc.Count][];
        readonly Sprite[] _boardEvo = new Sprite[4];
        readonly Sprite _star, _spiral, _gear, _flame, _msg, _heart, _symbol, _bottle, _steam, _px, _notif;
        readonly int _hat1, _hat2, _shirt1, _shirt2, _board1, _board2, _carrot1, _carrot2, _motor1, _motor2;

        public PlayerViews(Transform parent, GameAssets assets, MatchConfig cfg, GameDatabase db)
        {
            _assets = assets;
            _fx = new StampLayer(parent, "PlayerFx", 600);
            for (int a = 0; a < (int)Acc.Count; a++)
            {
                _acc[a] = new Sprite[4];
                for (int d = 0; d < 4; d++) _acc[a][d] = assets.Sprite($"acc_{AccIds[a]}_{Layers.DirNames[d]}");
            }
            for (int d = 0; d < 4; d++) _boardEvo[d] = assets.Sprite($"acc_protoboard_evo_{Layers.DirNames[d]}");
            _star = assets.Sprite("fx_estrela"); _spiral = assets.Sprite("fx_espiral"); _gear = assets.Sprite("fx_engrenagem");
            _flame = assets.Sprite("fx_chama"); _msg = assets.Sprite("fx_mensagem"); _heart = assets.Sprite("fx_coracao");
            _symbol = assets.Sprite("fx_simbolo"); _bottle = assets.Sprite("fx_garrafa"); _steam = assets.Sprite("fx_vapor");
            _px = assets.Sprite("fx_px"); _notif = assets.Sprite("emote_notificacao");
            _hat1 = db?.AbilityIndex("chapeu_japones") ?? -1; _hat2 = db?.AbilityIndex("mestre_projeteis") ?? -1;
            _shirt1 = db?.AbilityIndex("camiseta_contrario") ?? -1; _shirt2 = db?.AbilityIndex("moda_incompreensivel") ?? -1;
            _board1 = db?.AbilityIndex("protoboard") ?? -1; _board2 = db?.AbilityIndex("protoboard_experimental") ?? -1;
            _carrot1 = db?.AbilityIndex("cenoura_podre") ?? -1; _carrot2 = db?.AbilityIndex("hortalica_desgraca") ?? -1;
            _motor1 = db?.AbilityIndex("motor_astra") ?? -1; _motor2 = db?.AbilityIndex("astra_alta_rotacao") ?? -1;

            for (int s = 0; s < PlayerStore.MaxSlots; s++)
            {
                if (cfg?.Roster == null || s >= cfg.Roster.Length) continue;
                var root = new GameObject("Player" + s).transform;
                root.SetParent(parent, false);
                var v = new View { Sprites = CharacterSprites.For(assets, cfg.Roster[s].SpecializationId) };
                v.Shadow = NewRenderer(root, "shadow", assets.Sprite("fx_shadow"), Layers.Shadow);
                v.Shadow.color = new Color(1, 1, 1, 0.6f);
                v.Body = NewRenderer(root, "body", null, 0);
                for (int a = 0; a < (int)Acc.Count; a++) v.Acc[a] = NewRenderer(root, AccIds[a], null, 0);
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

        MatchFeed _feed;

        public void OnHit(int slot, float now)
        {
            if (!Valid(slot)) return;
            _views[slot].HurtAt = now;
            // Camiseta ao Contrário: às vezes ajeita a roupa ao levar dano
            if (_feed != null && Owns(_feed, slot, _shirt1, _shirt2) && now >= _views[slot].EmoteUntil + 2f && Random.value < 0.35f) Emote(slot, "camiseta", now, 1f);
        }

        public void OnSpecial(int slot, float now)
        {
            if (!Valid(slot)) return;
            _views[slot].SpecialAt = now;
            // Protoboard evoluída: concentração nas ativações especiais
            if (_feed != null && Owns(_feed, slot, -1, _board2)) Emote(slot, "foco", now, 1.2f);
        }
        public void OnAttack(int slot, float now) { if (Valid(slot)) _views[slot].AttackAt = now; }

        /// <summary>Emote sobre a cabeça (sprite emote_&lt;id&gt;) por alguns segundos.</summary>
        public void Emote(int slot, string id, float now, float seconds)
        {
            if (!Valid(slot)) return;
            _views[slot].Emote = id;
            _views[slot].EmoteUntil = now + seconds;
        }

        bool Valid(int slot) => slot >= 0 && slot < _views.Length && _views[slot] != null;

        static bool Owns(MatchFeed feed, int slot, int a, int b)
        {
            var l = feed.Loadouts?.Slots[slot];
            return l != null && ((a >= 0 && l.LevelOf(a) > 0) || (b >= 0 && l.LevelOf(b) > 0));
        }

        /// <summary>Posição desenhada (a do jogador local vem da previsão).</summary>
        public void Sync(MatchFeed feed, RenderFrame frame, int localSlot, Vec2? localPos, float now, bool labAlarm)
        {
            _fx.Begin();
            _feed = feed;
            for (int s = 0; s < _views.Length; s++)
            {
                var v = _views[s];
                if (v == null) continue;
                var snap = feed.Players[s];
                bool show = snap.Present && snap.Status != PlayerStatus.Inactive;
                v.Body.enabled = v.Shadow.enabled = show;
                v.Marker.enabled = show && snap.Status == PlayerStatus.Active;
                if (!show)
                {
                    foreach (var r in v.Acc) r.enabled = false;
                    continue;
                }

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
                var tint = invulnBlink ? new Color(1, 1, 1, 0.55f) : (snap.Flags & PlayerSnap.FlagSpecialActive) != 0 ? new Color(1f, 0.92f, 0.75f) : Color.white;
                v.Body.color = tint;

                // ---------- estados temporários ----------
                byte look = snap.Look;
                bool drunk = (look & (PlayerLook.Euphoria | PlayerLook.Hangover)) != 0;
                if ((look & PlayerLook.Euphoria) != 0 && (v.LastLook & PlayerLook.Euphoria) == 0) v.EuphoriaAt = now;
                v.LastLook = look;
                // balanço só no desenho (a posição real e a colisão não mudam)
                float sway = drunk && snap.Status == PlayerStatus.Active ? Mathf.Round(Mathf.Sin(now * 5f) * 1.5f) * Px : 0f;

                var feet = Layers.Snap(pos + new Vector2(0, FeetOffset)) + new Vector2(sway, 0);
                int order = Layers.Actor(pos.y);
                v.Body.transform.localPosition = feet;
                v.Body.sortingOrder = order;
                v.Shadow.transform.localPosition = Layers.Snap(pos + new Vector2(0, FeetOffset)) + new Vector2(0, Layers.Pixel * 2);
                v.Marker.transform.localPosition = Layers.Snap(pos + new Vector2(0, 0.72f));

                // ---------- acessórios presos à cabeça ----------
                bool anchored = _assets.Anchor(sprite, out var anchor);
                var head = feet + (anchored ? new Vector2((anchor.x - 16) * Px, (31 - anchor.y) * Px) : new Vector2(0, 27 * Px));
                int dir = v.Dir;
                bool lying = snap.Status == PlayerStatus.Downed && now - v.DownedAt > 0.12f;
                SetAcc(v, Acc.Hat, Owns(feed, s, _hat1, _hat2), _acc[(int)Acc.Hat][dir], head, order + 3, tint);
                SetAcc(v, Acc.Shirt, !lying && Owns(feed, s, _shirt1, _shirt2), _acc[(int)Acc.Shirt][dir], head, order + 1, tint);
                bool evo = Owns(feed, s, -1, _board2);
                SetAcc(v, Acc.Board, !lying && Owns(feed, s, _board1, _board2), evo ? _boardEvo[dir] : _acc[(int)Acc.Board][dir], head, order + 2, tint);
                SetAcc(v, Acc.Carrot, !lying && Owns(feed, s, _carrot1, _carrot2), _acc[(int)Acc.Carrot][dir], head, order + 2, tint);
                SetAcc(v, Acc.Blush, !lying && (drunk || (look & PlayerLook.Jalapeno) != 0), _acc[(int)Acc.Blush][dir], head, order + 1, tint);
                if (snap.Status != PlayerStatus.Active) continue;

                var top = head + new Vector2(0, 4 * Px);
                // Protoboard evoluída: trilhas que piscam
                if (evo && ((int)(now * 6) + s) % 3 == 0) _fx.Add(_px, Layers.Snap(head + new Vector2((dir == 2 ? -6 : 5) * Px, -22 * Px)), new Color(0.25f, 0.88f, 1f), order + 4);
                // Motor do Astra: engrenagens em órbita
                if (Owns(feed, s, _motor1, _motor2))
                    for (int k = 0; k < 2; k++)
                    {
                        float a = now * 3f + k * Mathf.PI;
                        _fx.Add(_gear, Layers.Snap(pos + new Vector2(Mathf.Cos(a) * 0.55f, Mathf.Sin(a) * 0.3f)), Color.white, order + (Mathf.Sin(a) < 0 ? 4 : -1), a * Mathf.Rad2Deg);
                    }
                if (drunk)
                {
                    // estrelas/espirais girando na cabeça, garrafa na ativação e rastro ao andar
                    var icon = (look & PlayerLook.Hangover) != 0 ? _spiral : _star;
                    for (int k = 0; k < 3; k++)
                    {
                        float a = now * 4f + k * 2.1f;
                        _fx.Add(icon, Layers.Snap(top + new Vector2(Mathf.Cos(a) * 0.35f, Mathf.Sin(a) * 0.12f)), Color.white, order + 5);
                    }
                    if (now - v.EuphoriaAt < 0.9f) _fx.Add(_bottle, Layers.Snap(head + new Vector2(0.45f, -0.2f + (now - v.EuphoriaAt) * 0.3f)), Color.white, order + 5);
                    Trail(v, pos, feet, now, moving, sprite, order, new Color(1f, 0.7f, 0.8f, 0.35f));
                }
                if ((look & PlayerLook.Jalapeno) != 0)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        float a = now * 5f + k * 1.6f, h = Mathf.Repeat(now * 1.4f + k * 0.25f, 1f);
                        _fx.Add(_flame, Layers.Snap(feet + new Vector2(Mathf.Cos(a) * 0.4f, 0.2f + h * 0.8f)), new Color(1, 1, 1, 1f - h), order + 4);
                    }
                    if (moving) Trail(v, pos, feet, now, true, _flame, order - 1, new Color(1f, 0.6f, 0.2f, 0.7f));
                }
                if ((look & PlayerLook.IogurteBoost) != 0)
                    for (int k = 0; k < 3; k++)
                    {
                        float h = Mathf.Repeat(now * 2f + k * 0.33f, 1f);
                        _fx.Add(_steam, Layers.Snap(feet + new Vector2((k - 1) * 0.25f, h * 0.3f)), new Color(1, 1, 1, 0.7f * (1f - h)), order - 1);
                    }
                if ((look & PlayerLook.Bubble) != 0)
                {
                    // bolha translúcida com mensagens, corações e notificações flutuando
                    int n = 40;
                    for (int k = 0; k < n; k++)
                    {
                        float a = Mathf.PI * 2f * k / n;
                        _fx.Add(_px, Layers.Snap(pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.25f), new Color(0.55f, 0.8f, 1f, 0.55f), order + 4);
                    }
                    var icons = _bubbleIcons ??= new[] { _msg, _heart, _notif, _msg };
                    for (int k = 0; k < icons.Length; k++)
                    {
                        float a = now * 1.2f + k * Mathf.PI / 2f;
                        _fx.Add(icons[k], Layers.Snap(pos + new Vector2(Mathf.Cos(a) * 0.85f, 0.2f + Mathf.Sin(a) * 0.6f)), new Color(1, 1, 1, 0.9f), order + 4);
                    }
                }
                if ((look & PlayerLook.Insulted) != 0)
                {
                    ShowEmote(top + new Vector2(0, 0.25f), "burro", order);
                    for (int k = 0; k < 3; k++)
                    {
                        float a = now * 2.5f + k * 2.1f;
                        _fx.Add(_symbol, Layers.Snap(top + new Vector2(Mathf.Cos(a) * 0.5f, 0.3f + Mathf.Sin(a) * 0.15f)), Color.white, order + 5);
                    }
                }
                else if ((look & PlayerLook.Zero) != 0) ShowEmote(top + new Vector2(0, 0.25f), "zero", order);
                else if (labAlarm) ShowEmote(top + new Vector2(0, 0.25f), "relogio", order);
                else if (now < v.EmoteUntil && v.Emote != null) ShowEmote(top + new Vector2(0, 0.25f + Mathf.Abs(Mathf.Sin(now * 6f)) * 0.06f), v.Emote, order);
                if ((look & PlayerLook.Reflex) != 0 && moving) Trail(v, pos, feet, now, true, sprite, order - 1, new Color(0.6f, 0.9f, 1f, 0.4f));
            }
            _fx.End();
        }

        void SetAcc(View v, Acc a, bool on, Sprite sprite, Vector2 at, int order, Color tint)
        {
            var r = v.Acc[(int)a];
            r.enabled = on && sprite != null;
            if (!r.enabled) return;
            r.sprite = sprite;
            r.color = tint;
            r.sortingOrder = order;
            r.transform.localPosition = at;
        }

        Sprite[] _bubbleIcons;
        readonly System.Collections.Generic.Dictionary<string, Sprite> _emotes = new System.Collections.Generic.Dictionary<string, Sprite>();

        void ShowEmote(Vector2 at, string id, int order)
        {
            // cache: sem concatenar texto a cada quadro (menos coleta de lixo no WebGL)
            if (!_emotes.TryGetValue(id, out var sprite)) _emotes[id] = sprite = _assets.Sprite("emote_" + id);
            _fx.Add(sprite, Layers.Snap(at), Color.white, order + 6);
        }

        /// <summary>Rastro discreto: cópias esmaecidas nas últimas posições.</summary>
        void Trail(View v, Vector2 pos, Vector2 feet, float now, bool moving, Sprite sprite, int order, Color color)
        {
            if (moving && now - v.TrailAt > 0.07f)
            {
                v.TrailAt = now;
                v.Trail[v.TrailHead] = feet;
                v.TrailHead = (v.TrailHead + 1) % v.Trail.Length;
            }
            if (!moving) return;
            for (int k = 0; k < v.Trail.Length; k++)
            {
                var p = v.Trail[k];
                if (p == Vector2.zero || (p - feet).sqrMagnitude < 0.01f) continue;
                _fx.Add(sprite, p, new Color(color.r, color.g, color.b, color.a * (0.5f + 0.5f * k / v.Trail.Length)), order - 1);
            }
        }

        public Vector2? PositionOf(int slot) => Valid(slot) && _views[slot].HasPos ? _views[slot].LastPos : (Vector2?)null;
    }
}
