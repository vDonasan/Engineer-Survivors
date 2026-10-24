using System;

namespace ES.Sim
{
    /// <summary>
    /// Grade uniforme com listas encadeadas por célula. Inserção O(1), consulta
    /// por raio sem alocação. Reconstruída a cada tick (Clear + Insert).
    /// </summary>
    public sealed class SpatialGrid
    {
        readonly float _cell;
        readonly float _inv;
        readonly int _cols, _rows;
        readonly int[] _head;
        readonly int[] _next;
        readonly int[] _item;
        readonly Vec2[] _pos;
        int _count;

        public SpatialGrid(float width, float height, float cell = 2f, int capacity = 1024)
        {
            _cell = cell;
            _inv = 1f / cell;
            _cols = Math.Max(1, (int)Math.Ceiling(width / cell));
            _rows = Math.Max(1, (int)Math.Ceiling(height / cell));
            _head = new int[_cols * _rows];
            _next = new int[capacity];
            _item = new int[capacity];
            _pos = new Vec2[capacity];
            Clear();
        }

        public void Clear()
        {
            for (int i = 0; i < _head.Length; i++) _head[i] = -1;
            _count = 0;
        }

        public void Insert(int index, Vec2 pos)
        {
            if (_count >= _next.Length) return;
            int c = CellOf(pos);
            int slot = _count++;
            _item[slot] = index;
            _pos[slot] = pos;
            _next[slot] = _head[c];
            _head[c] = slot;
        }

        /// <summary>Índices a até <paramref name="radius"/> do centro; retorna a quantidade.</summary>
        public int Query(Vec2 center, float radius, int[] results)
        {
            int x0 = ClampCol((int)Math.Floor((center.X - radius) * _inv));
            int x1 = ClampCol((int)Math.Floor((center.X + radius) * _inv));
            int y0 = ClampRow((int)Math.Floor((center.Y - radius) * _inv));
            int y1 = ClampRow((int)Math.Floor((center.Y + radius) * _inv));
            float r2 = radius * radius;
            int n = 0;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    for (int s = _head[y * _cols + x]; s >= 0; s = _next[s])
                    {
                        if (Vec2.DistanceSq(_pos[s], center) > r2) continue;
                        if (n >= results.Length) return n;
                        results[n++] = _item[s];
                    }
                }
            }
            return n;
        }

        int CellOf(Vec2 p) => ClampRow((int)Math.Floor(p.Y * _inv)) * _cols + ClampCol((int)Math.Floor(p.X * _inv));
        int ClampCol(int c) => c < 0 ? 0 : c >= _cols ? _cols - 1 : c;
        int ClampRow(int r) => r < 0 ? 0 : r >= _rows ? _rows - 1 : r;
    }
}
