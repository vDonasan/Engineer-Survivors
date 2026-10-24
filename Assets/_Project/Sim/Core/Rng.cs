namespace ES.Sim
{
    /// <summary>
    /// Gerador PCG32 (O'Neill). Determinístico por semente; usado só no host.
    /// </summary>
    public struct Rng
    {
        const ulong Multiplier = 6364136223846793005UL;
        const ulong Increment = 1442695040888963407UL; // ímpar fixo

        ulong _state;

        public Rng(ulong seed)
        {
            _state = 0;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = old * Multiplier + Increment;
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>[0, 1)</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>[minIncl, maxExcl)</summary>
        public int Range(int minIncl, int maxExcl)
        {
            if (maxExcl <= minIncl) return minIncl;
            uint span = (uint)(maxExcl - minIncl);
            return minIncl + (int)(NextUInt() % span);
        }

        public float Range(float min, float max) => min + (max - min) * NextFloat();
    }
}
