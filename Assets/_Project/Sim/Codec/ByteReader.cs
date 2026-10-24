using System;
using System.Text;

namespace ES.Sim
{
    /// <summary>Leitura binária little-endian que nunca lança: retorna false em dados curtos/malformados.</summary>
    public struct ByteReader
    {
        readonly byte[] _data;
        readonly int _length;
        public int Position;

        public ByteReader(byte[] data, int length)
        {
            _data = data ?? new byte[0];
            _length = Math.Max(0, Math.Min(length, _data.Length));
            Position = 0;
        }

        public int Remaining => _length - Position;

        public bool TryU8(out byte v)
        {
            if (Remaining < 1) { v = 0; return false; }
            v = _data[Position++];
            return true;
        }

        public bool TryI8(out sbyte v)
        {
            bool ok = TryU8(out byte b);
            v = (sbyte)b;
            return ok;
        }

        public bool TryBool(out bool v)
        {
            bool ok = TryU8(out byte b);
            v = b != 0;
            return ok && b <= 1;
        }

        public bool TryU16(out ushort v)
        {
            if (Remaining < 2) { v = 0; return false; }
            v = (ushort)(_data[Position] | (_data[Position + 1] << 8));
            Position += 2;
            return true;
        }

        public bool TryI16(out short v)
        {
            bool ok = TryU16(out ushort u);
            v = (short)u;
            return ok;
        }

        public bool TryU32(out uint v)
        {
            if (Remaining < 4) { v = 0; return false; }
            v = 0;
            for (int i = 0; i < 4; i++) v |= (uint)_data[Position + i] << (8 * i);
            Position += 4;
            return true;
        }

        public bool TryI32(out int v)
        {
            bool ok = TryU32(out uint u);
            v = (int)u;
            return ok;
        }

        public bool TryU64(out ulong v)
        {
            if (Remaining < 8) { v = 0; return false; }
            v = 0;
            for (int i = 0; i < 8; i++) v |= (ulong)_data[Position + i] << (8 * i);
            Position += 8;
            return true;
        }

        public bool TryF32(out float v)
        {
            bool ok = TryU32(out uint u);
            v = BitConverter.Int32BitsToSingle((int)u);
            return ok && !float.IsNaN(v) && !float.IsInfinity(v);
        }

        /// <summary>String UTF-8 com comprimento em 1 byte; falha se passar de <paramref name="maxChars"/>.</summary>
        public bool TryStr(int maxChars, out string s)
        {
            s = null;
            if (!TryU8(out byte n) || Remaining < n) return false;
            try
            {
                s = new UTF8Encoding(false, true).GetString(_data, Position, n);
            }
            catch (ArgumentException)
            {
                return false;
            }
            Position += n;
            return s.Length <= maxChars;
        }
    }
}
