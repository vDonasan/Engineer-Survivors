using System.Collections.Generic;
using ES.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ES.Game
{
    /// <summary>
    /// Monta a arena em alas com piso próprio — laboratório (noroeste), servidores (nordeste),
    /// copa (sudoeste), oficina (sudeste) — ligadas por um corredor central; paredes com janelas,
    /// cartazes, portas e racks; marcas no chão por ala; móveis nos obstáculos do conteúdo.
    /// Tudo o que bloqueia passagem vem de <see cref="ArenaDef.obstacles"/>; o resto é só chão.
    /// </summary>
    public static class ArenaBuilder
    {
        enum Wing { Lab, Servers, Lounge, Workshop, Corridor }

        const float CorridorHalf = 3f;

        public static Transform Build(ArenaDef arena, GameAssets assets, Transform parent)
        {
            var root = new GameObject("Arena");
            root.transform.SetParent(parent, false);
            var grid = root.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            int w = Mathf.CeilToInt(arena.width), h = Mathf.CeilToInt(arena.height);
            var floorSets = new Dictionary<Wing, Tile[]>
            {
                [Wing.Lab] = Tiles(assets, "tile_floor_", 4),
                [Wing.Servers] = Tiles(assets, "tile_server_", 2),
                [Wing.Lounge] = Tiles(assets, "tile_wood_", 3),
                [Wing.Workshop] = Tiles(assets, "tile_concrete_", 3),
                [Wing.Corridor] = Tiles(assets, "tile_epoxy_", 2),
            };
            var hazard = TileOf(assets.Sprite("tile_hazard_edge"));
            var rng = new System.Random(64);

            var floor = NewTilemap(root.transform, "Floor", Layers.Floor);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    var wing = WingAt(x + 0.5f, y + 0.5f, w, h);
                    floor.SetTile(new Vector3Int(x, y, 0), edge ? hazard : Pick(floorSets[wing], wing, rng.Next(100)));
                }

            Walls(root.transform, assets, w, h, rng);
            Decals(root.transform, assets, arena, w, h, rng);
            Props(root.transform, assets, arena);
            return root.transform;
        }

        static Wing WingAt(float x, float y, int w, int h)
        {
            if (Mathf.Abs(x - w * 0.5f) < CorridorHalf || Mathf.Abs(y - h * 0.5f) < CorridorHalf) return Wing.Corridor;
            bool north = y > h * 0.5f, east = x > w * 0.5f;
            return north ? (east ? Wing.Servers : Wing.Lab) : (east ? Wing.Workshop : Wing.Lounge);
        }

        /// <summary>Variantes raras (manchas, faixas) aparecem pouco; a base domina.</summary>
        static Tile Pick(Tile[] set, Wing wing, int r)
        {
            if (set.Length == 1) return set[0];
            switch (wing)
            {
                case Wing.Lab: return set[r < 70 ? 0 : r < 85 ? 1 : r < 95 ? 2 : 3];
                case Wing.Workshop: return set[r < 80 ? 0 : r < 94 ? 1 : 2];
                case Wing.Corridor: return set[r < 92 ? 0 : 1];
                case Wing.Servers: return set[r < 85 ? 0 : 1];
                default: return set[r % set.Length];
            }
        }

        static void Walls(Transform root, GameAssets assets, int w, int h, System.Random rng)
        {
            var plain = TileOf(assets.Sprite("tile_wall"));
            var window = TileOf(assets.Sprite("tile_wall_window"));
            var poster0 = TileOf(assets.Sprite("tile_wall_poster_0"));
            var poster1 = TileOf(assets.Sprite("tile_wall_poster_1"));
            var door = TileOf(assets.Sprite("tile_wall_door"));
            var rack = TileOf(assets.Sprite("tile_wall_server_0"));
            var rack2 = TileOf(assets.Sprite("tile_wall_server_1"));

            var walls = NewTilemap(root, "Walls", Layers.Obstacle);
            for (int x = -2; x < w + 2; x++)
            {
                // parede de cima: janelas na ala do laboratório, racks na dos servidores
                TileBase top = x == w / 2 ? door : x < w / 2 ? (x % 4 == 1 ? window : x % 9 == 4 ? poster1 : plain) : (x % 3 == 0 ? (x % 2 == 0 ? rack : rack2) : plain);
                walls.SetTile(new Vector3Int(x, h, 0), top);
                walls.SetTile(new Vector3Int(x, h + 1, 0), plain);
                TileBase bottom = x == w / 2 ? door : x % 7 == 3 ? poster0 : plain;
                walls.SetTile(new Vector3Int(x, -1, 0), bottom);
                walls.SetTile(new Vector3Int(x, -2, 0), plain);
            }
            for (int y = 0; y < h; y++)
                for (int k = 1; k <= 2; k++)
                {
                    TileBase t = k == 1 && y == h / 2 ? door : k == 1 && y % 11 == 5 ? poster0 : plain;
                    walls.SetTile(new Vector3Int(-k, y, 0), t);
                    walls.SetTile(new Vector3Int(w - 1 + k, y, 0), t);
                }
        }

        static readonly Dictionary<Wing, string[]> DecalsByWing = new Dictionary<Wing, string[]>
        {
            [Wing.Lab] = new[] { "decal_cables", "decal_papers", "decal_cables", "decal_puddle" },
            [Wing.Servers] = new[] { "decal_vent", "decal_cables", "decal_vent", "decal_cables" },
            [Wing.Lounge] = new[] { "decal_coffee", "decal_papers", "decal_coffee" },
            [Wing.Workshop] = new[] { "decal_oil", "decal_drain", "decal_tape", "decal_oil" },
            [Wing.Corridor] = new[] { "decal_arrow" },
        };

        static void Decals(Transform root, GameAssets assets, ArenaDef arena, int w, int h, System.Random rng)
        {
            var tiles = new Dictionary<string, Tile>();
            Tile T(string id) => tiles.TryGetValue(id, out var t) ? t : tiles[id] = TileOf(assets.Sprite(id));
            var decals = NewTilemap(root, "Decals", Layers.Decal);
            for (int i = 0; i < 90; i++)
            {
                int x = 2 + rng.Next(w - 4), y = 2 + rng.Next(h - 4);
                var wing = WingAt(x + 0.5f, y + 0.5f, w, h);
                if (wing == Wing.Corridor && rng.Next(3) > 0) continue; // corredor mais limpo
                var list = DecalsByWing[wing];
                decals.SetTile(new Vector3Int(x, y, 0), T(list[rng.Next(list.Length)]));
            }
            // tapetes da copa e logotipo no centro
            foreach (var (x, y) in new[] { (9, 15), (10, 15), (11, 15), (12, 15), (13, 15) }) decals.SetTile(new Vector3Int(x, y, 0), T("decal_rug"));
            Sprite(root, assets.Sprite("decal_logo"), new Vector2(w * 0.5f, h * 0.5f), Layers.Decal + 1);
        }

        static void Props(Transform root, GameAssets assets, ArenaDef arena)
        {
            foreach (var o in arena.obstacles)
            {
                string kind = string.IsNullOrEmpty(o.kind) ? (o.w >= o.h ? (o.h >= 2 ? "bancada" : "armario") : "armario") : o.kind;
                var center = new Vector2(o.x + o.w / 2f, o.y + o.h / 2f);
                int order = Layers.Actor(o.y + 0.2f);
                if (kind == "bancada" || kind == "armario")
                {
                    string id = kind == "bancada" ? "obstacle_bancada" : o.w >= o.h ? "obstacle_armario_h" : "obstacle_armario_v";
                    var sr = Sprite(root, assets.Sprite(id), center, order);
                    sr.drawMode = SpriteDrawMode.Sliced;
                    sr.size = new Vector2(o.w, o.h);
                    continue;
                }
                if (kind == "servidor")
                {
                    var sr = Sprite(root, assets.Sprite("prop_servidor_0"), center, order);
                    sr.gameObject.AddComponent<SpriteBlink>().Frames = new[] { assets.Sprite("prop_servidor_0"), assets.Sprite("prop_servidor_1") };
                    continue;
                }
                Sprite(root, assets.Sprite("prop_" + kind), center, order);
            }
        }

        static SpriteRenderer Sprite(Transform parent, Sprite sprite, Vector2 pos, int order)
        {
            var go = new GameObject(sprite != null ? sprite.name : "prop");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static Tile[] Tiles(GameAssets assets, string prefix, int n)
        {
            var t = new Tile[n];
            for (int i = 0; i < n; i++) t[i] = TileOf(assets.Sprite(prefix + i));
            return t;
        }

        static Tilemap NewTilemap(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            r.sortingOrder = order;
            r.mode = TilemapRenderer.Mode.Chunk;
            return tm;
        }

        static Tile TileOf(Sprite s)
        {
            var t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = s;
            return t;
        }
    }

    /// <summary>LEDs piscando nos racks de servidor.</summary>
    public sealed class SpriteBlink : MonoBehaviour
    {
        public Sprite[] Frames;
        SpriteRenderer _r;
        float _offset;

        void Awake()
        {
            _r = GetComponent<SpriteRenderer>();
            _offset = Random.value * 2f;
        }

        void Update()
        {
            if (Frames == null || Frames.Length == 0) return;
            _r.sprite = Frames[(int)((Time.time + _offset) * 2f) % Frames.Length];
        }
    }
}
