using System;

namespace ES.Sim
{
    /// <summary>
    /// Validação no host das entradas recebidas dos clientes: sequência, taxa,
    /// vetor de movimento e contador de especial. Nunca confia no cliente.
    /// </summary>
    public sealed class InputValidator
    {
        readonly int _maxPerSecond;
        readonly int _kickAfterMalformed;
        readonly SlotState[] _slots = new SlotState[PlayerStore.MaxSlots];

        struct SlotState
        {
            public bool HasSeq;
            public ushort LastSeq;
            public bool HasSpecial;
            public byte MaxSpecial;
            public long Window;
            public int InWindow;
            public int Malformed;
        }

        public InputValidator(int maxPerSecond = 60, int kickAfterMalformed = 50)
        {
            _maxPerSecond = maxPerSecond;
            _kickAfterMalformed = kickAfterMalformed;
        }

        public bool Accept(int slot, in PlayerInput input, double nowSeconds, out PlayerInput sanitized)
        {
            sanitized = PlayerInput.None;
            if (slot < 0 || slot >= _slots.Length) return false;
            ref var s = ref _slots[slot];

            long window = (long)Math.Floor(nowSeconds);
            if (window != s.Window) { s.Window = window; s.InWindow = 0; }
            if (s.InWindow >= _maxPerSecond) return false;

            if (s.HasSeq)
            {
                ushort delta = (ushort)(input.Seq - s.LastSeq);
                if (delta == 0 || delta > 32767) return false;
            }

            s.InWindow++;
            s.HasSeq = true;
            s.LastSeq = input.Seq;

            sanitized = input;

            int x = input.MoveX, y = input.MoveY;
            int sq = x * x + y * y;
            if (sq > 127 * 127)
            {
                float scale = 127f / (float)Math.Sqrt(sq);
                // Trunca em direção a zero: arredondar poderia passar de comprimento 1.
                sanitized.MoveX = (sbyte)(x * scale);
                sanitized.MoveY = (sbyte)(y * scale);
            }

            if (!s.HasSpecial) { s.HasSpecial = true; s.MaxSpecial = input.SpecialCounter; }
            else
            {
                byte d = (byte)(input.SpecialCounter - s.MaxSpecial);
                if (d > 0 && d < 128) s.MaxSpecial = input.SpecialCounter;
            }
            sanitized.SpecialCounter = s.MaxSpecial;
            return true;
        }

        public void CountMalformed(int slot)
        {
            if (slot >= 0 && slot < _slots.Length) _slots[slot].Malformed++;
        }

        public bool ShouldKick(int slot) => slot >= 0 && slot < _slots.Length && _slots[slot].Malformed >= _kickAfterMalformed;

        public void Reset(int slot)
        {
            if (slot >= 0 && slot < _slots.Length) _slots[slot] = default;
        }
    }
}
