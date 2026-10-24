namespace ES.Sim
{
    public static class EnemyFlags
    {
        public const byte Elite = 1 << 0;
        public const byte Stunned = 1 << 1;
        public const byte HitFlash = 1 << 2;
        public const byte Telegraphing = 1 << 3;
        /// <summary>Morto neste tick; removido no fim do combate.</summary>
        public const byte Dead = 1 << 4;
    }

    /// <summary>Inimigos em estrutura de arrays; remoção por troca com o último.</summary>
    public sealed class EnemyStore
    {
        public readonly int Capacity;
        public int Count;

        public readonly ushort[] Id;
        public readonly byte[] Type;
        public readonly byte[] Flags;
        public readonly Vec2[] Pos;
        public readonly Vec2[] Vel;
        public readonly Vec2[] Knock;
        public readonly float[] Hp, MaxHp, Radius, Speed, ContactDamage, XpValue;
        public readonly float[] StunUntil, NextContact, NextFire, TelegraphUntil, SlowUntil, SlowAmount, FlashUntil;

        readonly IdAllocator _ids = new IdAllocator();

        public EnemyStore(int capacity)
        {
            Capacity = capacity;
            Id = new ushort[capacity];
            Type = new byte[capacity];
            Flags = new byte[capacity];
            Pos = new Vec2[capacity];
            Vel = new Vec2[capacity];
            Knock = new Vec2[capacity];
            Hp = new float[capacity]; MaxHp = new float[capacity]; Radius = new float[capacity];
            Speed = new float[capacity]; ContactDamage = new float[capacity]; XpValue = new float[capacity];
            StunUntil = new float[capacity]; NextContact = new float[capacity]; NextFire = new float[capacity];
            TelegraphUntil = new float[capacity]; SlowUntil = new float[capacity]; SlowAmount = new float[capacity];
            FlashUntil = new float[capacity];
        }

        /// <summary>Cria com campos zerados; retorna o índice ou -1 se cheio.</summary>
        public int Spawn(int type, Vec2 pos)
        {
            if (Count >= Capacity) return -1;
            int i = Count++;
            Id[i] = _ids.Next();
            Type[i] = (byte)type;
            Flags[i] = 0;
            Pos[i] = pos;
            Vel[i] = Vec2.Zero;
            Knock[i] = Vec2.Zero;
            Hp[i] = MaxHp[i] = Radius[i] = Speed[i] = ContactDamage[i] = XpValue[i] = 0;
            StunUntil[i] = NextContact[i] = NextFire[i] = TelegraphUntil[i] = SlowUntil[i] = SlowAmount[i] = FlashUntil[i] = 0;
            return i;
        }

        public void RemoveAt(int i)
        {
            _ids.Release(Id[i]);
            int last = --Count;
            if (i == last) return;
            Id[i] = Id[last]; Type[i] = Type[last]; Flags[i] = Flags[last];
            Pos[i] = Pos[last]; Vel[i] = Vel[last]; Knock[i] = Knock[last];
            Hp[i] = Hp[last]; MaxHp[i] = MaxHp[last]; Radius[i] = Radius[last]; Speed[i] = Speed[last];
            ContactDamage[i] = ContactDamage[last]; XpValue[i] = XpValue[last];
            StunUntil[i] = StunUntil[last]; NextContact[i] = NextContact[last]; NextFire[i] = NextFire[last];
            TelegraphUntil[i] = TelegraphUntil[last]; SlowUntil[i] = SlowUntil[last]; SlowAmount[i] = SlowAmount[last];
            FlashUntil[i] = FlashUntil[last];
        }
    }

    /// <summary>IDs ushort únicos entre entidades vivas (0 reservado).</summary>
    public sealed class IdAllocator
    {
        readonly bool[] _used = new bool[65536];
        ushort _next = 1;

        public ushort Next()
        {
            for (int guard = 0; guard < 65535; guard++)
            {
                ushort id = _next;
                _next = (ushort)(_next == ushort.MaxValue ? 1 : _next + 1);
                if (_used[id]) continue;
                _used[id] = true;
                return id;
            }
            throw new System.InvalidOperationException("IDs esgotados");
        }

        public void Release(ushort id) => _used[id] = false;
    }
}
