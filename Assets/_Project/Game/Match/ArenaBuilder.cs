using ES.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ES.Game
{
    /// <summary>Monta o chão (Tilemap 32×32), as paredes e os obstáculos da arena.</summary>
    public static class ArenaBuilder
    {
        public static Transform Build(ArenaDef arena, GameAssets assets, Transform parent)
        {
            var root = new GameObject("Arena");
            root.transform.SetParent(parent, false);
            var grid = root.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            int w = Mathf.CeilToInt(arena.width), h = Mathf.CeilToInt(arena.height);
            var floor = NewTilemap(root.transform, "Floor", Layers.Floor);
            var floorTiles = new Tile[4];
            for (int i = 0; i < 4; i++) floorTiles[i] = TileOf(assets.Sprite("tile_floor_" + i));
            var hazard = TileOf(assets.Sprite("tile_hazard_edge"));
            var wall = TileOf(assets.Sprite("tile_wall"));
            var cables = TileOf(assets.Sprite("decal_cables"));

            var rng = new System.Random(64);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    int r = rng.Next(100);
                    var tile = edge ? hazard : floorTiles[r < 70 ? 0 : r < 85 ? 1 : r < 95 ? 2 : 3];
                    floor.SetTile(new Vector3Int(x, y, 0), tile);
                }

            var walls = NewTilemap(root.transform, "Walls", Layers.Obstacle);
            for (int x = -2; x < w + 2; x++)
                for (int k = 1; k <= 2; k++)
                {
                    walls.SetTile(new Vector3Int(x, -k, 0), wall);
                    walls.SetTile(new Vector3Int(x, h - 1 + k, 0), wall);
                }
            for (int y = 0; y < h; y++)
                for (int k = 1; k <= 2; k++)
                {
                    walls.SetTile(new Vector3Int(-k, y, 0), wall);
                    walls.SetTile(new Vector3Int(w - 1 + k, y, 0), wall);
                }

            var decals = NewTilemap(root.transform, "Decals", Layers.Decal);
            for (int i = 0; i < 40; i++) decals.SetTile(new Vector3Int(2 + rng.Next(w - 4), 2 + rng.Next(h - 4), 0), cables);

            foreach (var o in arena.obstacles)
            {
                string id = o.w >= o.h ? (o.h >= 2 ? "obstacle_bancada" : "obstacle_armario_h") : "obstacle_armario_v";
                var go = new GameObject(id);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(o.x + o.w / 2f, o.y + o.h / 2f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = assets.Sprite(id);
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(o.w, o.h);
                sr.sortingOrder = Layers.Actor(o.y + 0.2f);
            }
            return root.transform;
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
}
