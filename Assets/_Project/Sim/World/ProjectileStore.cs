namespace ES.Sim
{
    /// <summary>Projéteis hostis (sincronizados no snapshot).</summary>
    public sealed class ProjectileStore
    {
        public readonly int Capacity;
        public int Count;

        public readonly ushort[] Id;
        public readonly byte[] Type;
        public readonly Vec2[] Pos;
        public readonly Vec2[] Vel;
        public readonly float[] Damage, Radius, LifeLeft;

        readonly IdAllocator _ids = new IdAllocator();

        public ProjectileStore(int capacity)
        {
            Capacity = capacity;
            Id = new ushort[capacity];
            Type = new byte[capacity];
            Pos = new Vec2[capacity];
            Vel = new Vec2[capacity];
            Damage = new float[capacity];
            Radius = new float[capacity];
            LifeLeft = new float[capacity];
        }

        public int Spawn(int type, Vec2 pos, Vec2 vel, float damage, float radius, float life)
        {
            if (Count >= Capacity) return -1;
            int i = Count++;
            Id[i] = _ids.Next();
            Type[i] = (byte)type;
            Pos[i] = pos;
            Vel[i] = vel;
            Damage[i] = damage;
            Radius[i] = radius;
            LifeLeft[i] = life;
            return i;
        }

        public void RemoveAt(int i)
        {
            _ids.Release(Id[i]);
            int last = --Count;
            if (i == last) return;
            Id[i] = Id[last]; Type[i] = Type[last]; Pos[i] = Pos[last]; Vel[i] = Vel[last];
            Damage[i] = Damage[last]; Radius[i] = Radius[last]; LifeLeft[i] = LifeLeft[last];
        }
    }
}
