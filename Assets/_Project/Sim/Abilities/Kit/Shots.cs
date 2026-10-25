using System;

namespace ES.Sim
{
    /// <summary>Tiro de jogador (só no host; os clientes recebem posição e sprite no snapshot).</summary>
    public struct Shot
    {
        public ushort Id;
        public byte Owner;
        public short Ability;
        public AllySprite Sprite;
        public ShotMotion Motion;
        /// <summary>Subtipo para o comportamento dono (ex.: maleta x bolha).</summary>
        public byte Variant;
        /// <summary>Geração: filhos de filhos param em <see cref="Kit.MaxDepth"/>.</summary>
        public byte Depth;

        public Vec2 Pos, Vel;
        public float Damage, Knockback, Radius, Life, Age;
        public int Pierce, Ricochets;
        public float RicochetRange;

        /// <summary>Wave: amplitude; Curve/Homing: giro em rad/s; Roll: desaceleração.</summary>
        public float Curve, Frequency;
        public Vec2 Aim;
        public bool Steering;

        public float BurstRadius, BurstDamage;
        public FxStyle BurstStyle;
        public byte BurstWhen;

        public float Slow, SlowTime, Stun;
        public ShotStatus Status;
        public float StatusValue, StatusTime;

        // Onda: deslocamento lateral aplicado no tick anterior (para tirar e recolocar).
        internal float WaveOffset;
        internal byte HitCount, HitNext;
    }

    public sealed class ShotStore
    {
        public const int MaxHitsTracked = 16;

        public readonly int Capacity;
        public int Count;
        public readonly Shot[] Items;
        readonly ushort[] _hits;
        readonly IdAllocator _ids;

        public ShotStore(int capacity, IdAllocator ids)
        {
            Capacity = capacity;
            Items = new Shot[capacity];
            _hits = new ushort[capacity * MaxHitsTracked];
            _ids = ids;
        }

        /// <summary>Cria o tiro; retorna o índice ou -1 se a cota acabou.</summary>
        public int Spawn(in Shot s)
        {
            if (Count >= Capacity) return -1;
            int i = Count++;
            Items[i] = s;
            Items[i].Id = _ids.Next();
            Items[i].Age = 0;
            Items[i].HitCount = Items[i].HitNext = 0;
            Items[i].WaveOffset = 0;
            if (Items[i].Pierce < 1) Items[i].Pierce = 1;
            return i;
        }

        public bool AlreadyHit(int i, ushort id)
        {
            int b = i * MaxHitsTracked;
            for (int k = 0; k < Items[i].HitCount; k++) if (_hits[b + k] == id) return true;
            return false;
        }

        /// <summary>Anel: depois de 16 acertos o mais antigo pode ser atingido de novo.</summary>
        public void RecordHit(int i, ushort id)
        {
            ref var s = ref Items[i];
            _hits[i * MaxHitsTracked + s.HitNext] = id;
            s.HitNext = (byte)((s.HitNext + 1) % MaxHitsTracked);
            if (s.HitCount < MaxHitsTracked) s.HitCount++;
        }

        public int CopyHits(int i, ushort[] into)
        {
            int n = Math.Min(Items[i].HitCount, into.Length);
            Array.Copy(_hits, i * MaxHitsTracked, into, 0, n);
            return n;
        }

        public void RemoveAt(int i)
        {
            _ids.Release(Items[i].Id);
            int last = --Count;
            if (i == last) return;
            Items[i] = Items[last];
            Array.Copy(_hits, last * MaxHitsTracked, _hits, i * MaxHitsTracked, MaxHitsTracked);
        }

        public void Clear()
        {
            for (int i = 0; i < Count; i++) _ids.Release(Items[i].Id);
            Count = 0;
        }
    }

    public static class ShotSystem
    {
        static readonly int[] Hits = new int[64];
        static readonly int[] Target = new int[1];
        static readonly ushort[] Exclude = new ushort[ShotStore.MaxHitsTracked];

        public static void Step(World w, float dt)
        {
            var st = w.Shots;
            var e = w.Enemies;
            for (int i = st.Count - 1; i >= 0; i--)
            {
                ref var s = ref st.Items[i];
                s.Age += dt;
                bool end = Move(w, ref s, dt);
                if (!end)
                {
                    int n = AbilitySystem.Overlapping(w, s.Pos, s.Radius, Hits);
                    for (int h = 0; h < n && s.Pierce > 0; h++)
                    {
                        int k = Hits[h];
                        if ((e.Flags[k] & EnemyFlags.Dead) != 0 || st.AlreadyHit(i, e.Id[k])) continue;
                        st.RecordHit(i, e.Id[k]);
                        s.Pierce--;
                        HitEnemy(w, ref s, k);
                        if (s.Pierce <= 0 && s.Ricochets > 0 && Ricochet(w, st, i, ref s)) break;
                    }
                    end = s.Pierce <= 0;
                }
                if (end)
                {
                    Finish(w, ref s);
                    st.RemoveAt(i);
                }
            }
        }

        /// <summary>Move o tiro; true quando ele termina (tempo, parede, parado).</summary>
        static bool Move(World w, ref Shot s, float dt)
        {
            if (s.Age >= s.Life) return true;
            switch (s.Motion)
            {
                case ShotMotion.Wave:
                {
                    var dir = s.Vel.Normalized();
                    var perp = new Vec2(-dir.Y, dir.X);
                    float off = s.Curve * (float)Math.Sin(s.Age * s.Frequency * Math.PI * 2);
                    s.Pos += s.Vel * dt + perp * (off - s.WaveOffset);
                    s.WaveOffset = off;
                    break;
                }
                case ShotMotion.Curve:
                case ShotMotion.Homing:
                {
                    Vec2 goal = s.Aim;
                    bool steer = s.Steering;
                    if (s.Motion == ShotMotion.Homing && AbilitySystem.Nearest(w, s.Pos, 6f, 1, Target, null, 0) > 0)
                    {
                        goal = w.Enemies.Pos[Target[0]];
                        steer = true;
                    }
                    if (steer)
                    {
                        s.Vel = Turn(s.Vel, goal - s.Pos, s.Curve * dt);
                        if (s.Motion == ShotMotion.Curve && Vec2.DistanceSq(s.Pos, s.Aim) < 0.36f) s.Steering = false;
                    }
                    s.Pos += s.Vel * dt;
                    break;
                }
                case ShotMotion.Roll:
                {
                    float speed = s.Vel.Length();
                    speed = Math.Max(0, speed - s.Curve * dt);
                    if (speed < 0.4f) return true;
                    s.Vel = s.Vel.Normalized() * speed;
                    var next = s.Pos + s.Vel * dt;
                    if (ArenaCollision.Blocked(next, s.Radius, w.Arena))
                    {
                        // quica: inverte o eixo que bate
                        if (ArenaCollision.Blocked(new Vec2(next.X, s.Pos.Y), s.Radius, w.Arena)) s.Vel.X = -s.Vel.X;
                        if (ArenaCollision.Blocked(new Vec2(s.Pos.X, next.Y), s.Radius, w.Arena)) s.Vel.Y = -s.Vel.Y;
                        next = s.Pos + s.Vel * dt;
                        if (ArenaCollision.Blocked(next, s.Radius, w.Arena)) return true;
                    }
                    s.Pos = next;
                    return false;
                }
                default:
                    s.Pos += s.Vel * dt;
                    break;
            }
            return ArenaCollision.Blocked(s.Pos, s.Radius, w.Arena);
        }

        /// <summary>Gira <paramref name="v"/> em direção a <paramref name="to"/> no máximo <paramref name="maxRad"/>.</summary>
        public static Vec2 Turn(Vec2 v, Vec2 to, float maxRad)
        {
            float speed = v.Length();
            if (speed < 1e-4f || to.LengthSq() < 1e-6f) return v;
            double a = Math.Atan2(v.Y, v.X), b = Math.Atan2(to.Y, to.X);
            double d = b - a;
            while (d > Math.PI) d -= Math.PI * 2;
            while (d < -Math.PI) d += Math.PI * 2;
            d = Math.Max(-maxRad, Math.Min(maxRad, d));
            a += d;
            return new Vec2((float)Math.Cos(a) * speed, (float)Math.Sin(a) * speed);
        }

        static void HitEnemy(World w, ref Shot s, int k)
        {
            var e = w.Enemies;
            float now = w.Elapsed;
            var pos = e.Pos[k];
            bool marked = e.MarkUntil[k] > now;
            CombatSystem.DamageEnemy(w, k, s.Damage, s.Vel.Normalized(), s.Knockback, s.Owner, s.Ability);
            if (s.Slow > 0) Area.ApplySlow(w, k, s.Slow, s.SlowTime);
            if (s.Stun > 0) e.StunUntil[k] = Math.Max(e.StunUntil[k], now + s.Stun);
            switch (s.Status)
            {
                case ShotStatus.Mark: e.MarkUntil[k] = now + s.StatusTime; break;
                case ShotStatus.Sticky:
                    if (e.DelayAt[k] <= now)
                    {
                        e.DelayAt[k] = now + s.StatusTime;
                        e.DelayDamage[k] = s.StatusValue;
                        e.DelayOwner[k] = s.Owner;
                        e.DelayAbility[k] = s.Ability;
                    }
                    break;
                case ShotStatus.Trap:
                    Area.ApplyDot(w, k, s.StatusValue, s.StatusTime, s.Owner, s.Ability);
                    e.TrapUntil[k] = now + s.StatusTime;
                    e.StunUntil[k] = Math.Max(e.StunUntil[k], now + s.StatusTime);
                    break;
                case ShotStatus.ConsumeMark:
                    if (marked)
                    {
                        e.MarkUntil[k] = 0;
                        Area.Burst(w, s.Owner, s.Ability, pos, s.BurstRadius, s.BurstDamage, 1f, 0f, FxStyle.Math);
                    }
                    break;
            }
            if ((s.BurstWhen & ShotBurst.OnHit) != 0 && s.BurstRadius > 0)
                Area.Burst(w, s.Owner, s.Ability, pos, s.BurstRadius, s.BurstDamage, s.Knockback * 0.5f, 0f, s.BurstStyle);
            Handler(w, s.Ability)?.OnShotHit(w, ref s, k);
        }

        static bool Ricochet(World w, ShotStore st, int i, ref Shot s)
        {
            int n = st.CopyHits(i, Exclude);
            if (AbilitySystem.Nearest(w, s.Pos, s.RicochetRange, 1, Target, Exclude, n) == 0) return false;
            float speed = Math.Max(4f, s.Vel.Length());
            s.Vel = (w.Enemies.Pos[Target[0]] - s.Pos).Normalized() * speed;
            s.Ricochets--;
            s.Pierce = 1;
            s.Life = s.Age + s.RicochetRange / speed + 0.3f;
            s.Motion = s.Motion == ShotMotion.Roll ? ShotMotion.Roll : ShotMotion.Linear;
            return true;
        }

        static void Finish(World w, ref Shot s)
        {
            if ((s.BurstWhen & ShotBurst.OnEnd) != 0 && s.BurstRadius > 0)
                Area.Burst(w, s.Owner, s.Ability, s.Pos, s.BurstRadius, s.BurstDamage, s.Knockback * 0.5f, 0f, s.BurstStyle);
            Handler(w, s.Ability)?.OnShotEnd(w, ref s);
        }

        internal static IShotHandler Handler(World w, int ability) =>
            ability >= 0 && ability < w.Db.AbilityCount ? AbilitySystem.For(w.Db.Ability(ability).Def.behavior) as IShotHandler : null;
    }
}
