namespace ES.Sim
{
    /// <summary>
    /// Projéteis dos jogadores. Existem só no host; os clientes desenham a partir
    /// do evento ProjectileFired.
    /// </summary>
    public sealed class PlayerProjectileStore
    {
        public const int MaxHitsTracked = 8;

        public readonly int Capacity;
        public int Count;

        public readonly byte[] Owner;
        public readonly short[] Ability;
        public readonly Vec2[] Pos, Vel;
        public readonly float[] Damage, Knockback, Radius, LifeLeft;
        public readonly int[] HitsLeft;
        public readonly byte[] HitCount;
        public readonly ushort[] HitIds;

        public PlayerProjectileStore(int capacity)
        {
            Capacity = capacity;
            Owner = new byte[capacity];
            Ability = new short[capacity];
            Pos = new Vec2[capacity];
            Vel = new Vec2[capacity];
            Damage = new float[capacity];
            Knockback = new float[capacity];
            Radius = new float[capacity];
            LifeLeft = new float[capacity];
            HitsLeft = new int[capacity];
            HitCount = new byte[capacity];
            HitIds = new ushort[capacity * MaxHitsTracked];
        }

        public int Spawn(int owner, int ability, Vec2 pos, Vec2 vel, float damage, float knockback, float radius, float life, int hits)
        {
            if (Count >= Capacity) return -1;
            int i = Count++;
            Owner[i] = (byte)owner;
            Ability[i] = (short)ability;
            Pos[i] = pos;
            Vel[i] = vel;
            Damage[i] = damage;
            Knockback[i] = knockback;
            Radius[i] = radius;
            LifeLeft[i] = life;
            HitsLeft[i] = hits;
            HitCount[i] = 0;
            return i;
        }

        public bool AlreadyHit(int i, ushort id)
        {
            int baseIdx = i * MaxHitsTracked;
            for (int k = 0; k < HitCount[i]; k++) if (HitIds[baseIdx + k] == id) return true;
            return false;
        }

        public void RecordHit(int i, ushort id)
        {
            if (HitCount[i] >= MaxHitsTracked) return;
            HitIds[i * MaxHitsTracked + HitCount[i]++] = id;
        }

        public void RemoveAt(int i)
        {
            int last = --Count;
            if (i == last) return;
            Owner[i] = Owner[last]; Ability[i] = Ability[last]; Pos[i] = Pos[last]; Vel[i] = Vel[last];
            Damage[i] = Damage[last]; Knockback[i] = Knockback[last]; Radius[i] = Radius[last]; LifeLeft[i] = LifeLeft[last];
            HitsLeft[i] = HitsLeft[last]; HitCount[i] = HitCount[last];
            System.Array.Copy(HitIds, last * MaxHitsTracked, HitIds, i * MaxHitsTracked, MaxHitsTracked);
        }
    }

    public static class PlayerProjectiles
    {
        static readonly int[] Hits = new int[32];

        public static void Step(World w, float dt)
        {
            var pp = w.PlayerProjectiles;
            var e = w.Enemies;
            for (int i = pp.Count - 1; i >= 0; i--)
            {
                pp.Pos[i] += pp.Vel[i] * dt;
                pp.LifeLeft[i] -= dt;
                bool remove = pp.LifeLeft[i] <= 0 || ArenaCollision.Blocked(pp.Pos[i], pp.Radius[i], w.Arena);
                if (!remove)
                {
                    int n = AbilitySystem.Overlapping(w, pp.Pos[i], pp.Radius[i], Hits);
                    for (int h = 0; h < n && pp.HitsLeft[i] > 0; h++)
                    {
                        int k = Hits[h];
                        if (pp.AlreadyHit(i, e.Id[k])) continue;
                        pp.RecordHit(i, e.Id[k]);
                        pp.HitsLeft[i]--;
                        CombatSystem.DamageEnemy(w, k, pp.Damage[i], pp.Vel[i].Normalized(), pp.Knockback[i], pp.Owner[i], pp.Ability[i]);
                    }
                    remove = pp.HitsLeft[i] <= 0;
                }
                if (remove) pp.RemoveAt(i);
            }
        }
    }
}
