namespace ES.Sim
{
    public enum PickupKind : byte { Xp, Heal }

    /// <summary>Gemas de XP e curas no chão.</summary>
    public sealed class GemStore
    {
        public readonly int Capacity;
        public int Count;

        public readonly ushort[] Id;
        public readonly PickupKind[] Kind;
        public readonly Vec2[] Pos;
        public readonly float[] Value;
        public readonly int[] SpawnTick;

        readonly IdAllocator _ids = new IdAllocator();

        public GemStore(int capacity)
        {
            Capacity = capacity;
            Id = new ushort[capacity];
            Kind = new PickupKind[capacity];
            Pos = new Vec2[capacity];
            Value = new float[capacity];
            SpawnTick = new int[capacity];
        }

        public int Spawn(PickupKind kind, Vec2 pos, float value, int tick)
        {
            if (Count >= Capacity) return -1;
            int i = Count++;
            Id[i] = _ids.Next();
            Kind[i] = kind;
            Pos[i] = pos;
            Value[i] = value;
            SpawnTick[i] = tick;
            return i;
        }

        public void RemoveAt(int i)
        {
            _ids.Release(Id[i]);
            int last = --Count;
            if (i == last) return;
            Id[i] = Id[last]; Kind[i] = Kind[last]; Pos[i] = Pos[last]; Value[i] = Value[last]; SpawnTick[i] = SpawnTick[last];
        }
    }
}
