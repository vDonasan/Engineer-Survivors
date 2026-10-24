using System;
using System.Text;

namespace ES.Sim
{
    /// <summary>Escrita binária little-endian; cresce sob demanda e é reutilizável.</summary>
    public sealed class ByteWriter
    {
        public byte[] Buffer;
        public int Length;

        public ByteWriter(int capacity = 256) { Buffer = new byte[Math.Max(16, capacity)]; }

        public void Reset() => Length = 0;

        void Ensure(int n)
        {
            if (Length + n <= Buffer.Length) return;
            Array.Resize(ref Buffer, Math.Max(Buffer.Length * 2, Length + n));
        }

        public void U8(byte v) { Ensure(1); Buffer[Length++] = v; }
        public void I8(sbyte v) => U8((byte)v);
        public void Bool(bool v) => U8(v ? (byte)1 : (byte)0);

        public void U16(ushort v)
        {
            Ensure(2);
            Buffer[Length++] = (byte)v;
            Buffer[Length++] = (byte)(v >> 8);
        }

        public void I16(short v) => U16((ushort)v);

        public void U32(uint v)
        {
            Ensure(4);
            for (int i = 0; i < 4; i++) Buffer[Length++] = (byte)(v >> (8 * i));
        }

        public void I32(int v) => U32((uint)v);

        public void U64(ulong v)
        {
            Ensure(8);
            for (int i = 0; i < 8; i++) Buffer[Length++] = (byte)(v >> (8 * i));
        }

        public void F32(float v) => U32((uint)BitConverter.SingleToInt32Bits(v));

        /// <summary>String UTF-8 com comprimento em 1 byte (corta em 255 bytes).</summary>
        public void Str(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s ?? "");
            int n = Math.Min(255, bytes.Length);
            U8((byte)n);
            Ensure(n);
            System.Buffer.BlockCopy(bytes, 0, Buffer, Length, n);
            Length += n;
        }
    }
}
