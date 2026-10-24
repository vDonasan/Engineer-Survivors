namespace ES.Sim
{
    /// <summary>Conjunto de tags como campo de bits.</summary>
    public struct TagSet
    {
        public ulong Bits;

        public TagSet(ulong bits) { Bits = bits; }

        public static TagSet Of(params Tag[] tags)
        {
            ulong bits = 0;
            foreach (var t in tags) bits |= 1UL << (int)t;
            return new TagSet(bits);
        }

        public bool Has(Tag tag) => (Bits & (1UL << (int)tag)) != 0;
        public bool Overlaps(TagSet other) => (Bits & other.Bits) != 0;
        public bool IsEmpty => Bits == 0;

        public TagSet With(Tag tag) => new TagSet(Bits | (1UL << (int)tag));

        public int Count
        {
            get
            {
                ulong v = Bits;
                int c = 0;
                while (v != 0) { v &= v - 1; c++; }
                return c;
            }
        }
    }
}
