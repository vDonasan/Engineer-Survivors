using System;

namespace ES.Sim
{
    /// <summary>Área persistente (campo elétrico, tempestade): fere e desacelera em intervalos.</summary>
    public struct Zone
    {
        public ushort Id;
        public byte Owner;
        public short Ability;
        public AllySprite Sprite;
        public Vec2 Pos;
        public float Radius, Life, Interval, NextTick, Damage, Slow;
        /// <summary>Segue este jogador (−1 = parada).</summary>
        public sbyte Follow;
    }

    /// <summary>Golpe atrasado: aviso no chão e explosão depois de Delay (equação, TV caindo).</summary>
    public struct Strike
    {
        public byte Owner;
        public short Ability;
        public byte Variant, Depth;
        public Vec2 Pos;
        public float Delay, Radius, Damage, Knockback, Stun;
        public FxStyle Style;
    }

    /// <summary>Companheiro (Cabazords, carro, pneus orbitais). O comportamento dono o move.</summary>
    public struct Companion
    {
        public bool Active;
        public ushort Id;
        public byte Owner;
        public short Ability;
        public AllySprite Sprite;
        public Vec2 Pos, Vel;
        /// <summary>Ângulo/estado enviado à apresentação (0–255).</summary>
        public byte Param;
    }

    public sealed class ZoneStore
    {
        public readonly Zone[] Items;
        public int Count;
        readonly IdAllocator _ids;
        public ZoneStore(int capacity, IdAllocator ids) { Items = new Zone[capacity]; _ids = ids; }

        public int Spawn(in Zone z)
        {
            if (Count >= Items.Length) return -1;
            Items[Count] = z;
            Items[Count].Id = _ids.Next();
            return Count++;
        }

        public void RemoveAt(int i)
        {
            _ids.Release(Items[i].Id);
            Items[i] = Items[--Count];
        }
    }

    public sealed class StrikeStore
    {
        public readonly Strike[] Items;
        public int Count;
        public StrikeStore(int capacity) { Items = new Strike[capacity]; }

        public bool Spawn(World w, in Strike s)
        {
            if (Count >= Items.Length) return false;
            Items[Count++] = s;
            w.Events.AddCosmetic(new CosmeticEvent
            {
                Kind = CosmeticKind.AreaMark, A = s.Owner, B = (int)s.Style, X = s.Pos.X, Y = s.Pos.Y, X2 = s.Radius, Value = s.Delay,
            });
            return true;
        }

        public void RemoveAt(int i) => Items[i] = Items[--Count];
    }

    /// <summary>Vagas fixas: o comportamento guarda o índice da vaga.</summary>
    public sealed class CompanionStore
    {
        public readonly Companion[] Items;
        readonly IdAllocator _ids;
        public CompanionStore(int capacity, IdAllocator ids) { Items = new Companion[capacity]; _ids = ids; }

        public int ActiveCount
        {
            get { int n = 0; foreach (var c in Items) if (c.Active) n++; return n; }
        }

        public int Spawn(int owner, int ability, AllySprite sprite, Vec2 pos)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i].Active) continue;
                Items[i] = new Companion { Active = true, Id = _ids.Next(), Owner = (byte)owner, Ability = (short)ability, Sprite = sprite, Pos = pos };
                return i;
            }
            return -1;
        }

        public void Remove(int i)
        {
            if (i < 0 || i >= Items.Length || !Items[i].Active) return;
            _ids.Release(Items[i].Id);
            Items[i].Active = false;
        }

        /// <summary>Remove os companheiros de uma habilidade de um jogador (evolução, saída).</summary>
        public void RemoveOwned(int owner, int ability)
        {
            for (int i = 0; i < Items.Length; i++)
                if (Items[i].Active && Items[i].Owner == owner && (ability < 0 || Items[i].Ability == ability)) Remove(i);
        }
    }

    public static class FieldSystem
    {
        static readonly int[] Hits = new int[256];

        public static void Step(World w, float dt)
        {
            var zs = w.Zones;
            var e = w.Enemies;
            for (int i = zs.Count - 1; i >= 0; i--)
            {
                ref var z = ref zs.Items[i];
                z.Life -= dt;
                if (z.Follow >= 0)
                {
                    var p = w.Players.Slots[z.Follow];
                    if (p == null || !p.IsActive) z.Life = 0;
                    else z.Pos = p.Pos;
                }
                if (z.Life <= 0) { zs.RemoveAt(i); continue; }
                if (w.Elapsed < z.NextTick || (z.Damage <= 0 && z.Slow <= 0)) continue;
                z.NextTick = w.Elapsed + Math.Max(0.1f, z.Interval);
                int n = AbilitySystem.Overlapping(w, z.Pos, z.Radius, Hits);
                for (int h = 0; h < n; h++)
                {
                    int k = Hits[h];
                    if (z.Slow > 0) Area.ApplySlow(w, k, z.Slow, z.Interval + 0.15f);
                    if (z.Damage > 0) CombatSystem.DamageEnemy(w, k, z.Damage, Vec2.Zero, 0f, z.Owner, z.Ability);
                }
            }

            var ss = w.Strikes;
            for (int i = ss.Count - 1; i >= 0; i--)
            {
                ref var s = ref ss.Items[i];
                s.Delay -= dt;
                if (s.Delay > 0) continue;
                var strike = s;
                ss.RemoveAt(i);
                Area.Burst(w, strike.Owner, strike.Ability, strike.Pos, strike.Radius, strike.Damage, strike.Knockback, strike.Stun, strike.Style);
                ShotSystem.Handler(w, strike.Ability)?.OnStrike(w, strike);
            }
        }
    }
}
