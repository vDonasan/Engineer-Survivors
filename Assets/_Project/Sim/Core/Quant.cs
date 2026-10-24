using System;

namespace ES.Sim
{
    /// <summary>Quantização usada nos codecs de rede.</summary>
    public static class Quant
    {
        /// <summary>Posição em 16 bits lineares sobre [min, max] (64 u → ~0,001 u).</summary>
        public static ushort Pos(float v, float min, float max)
        {
            float t = (v - min) / (max - min);
            if (t <= 0f) return 0;
            if (t >= 1f) return ushort.MaxValue;
            return (ushort)Math.Round(t * ushort.MaxValue);
        }

        public static float UnPos(ushort q, float min, float max) => min + (max - min) * (q / (float)ushort.MaxValue);

        /// <summary>Eixo de entrada em [-1, 1] como sbyte [-127, 127].</summary>
        public static sbyte Axis(float v)
        {
            if (v >= 1f) return 127;
            if (v <= -1f) return -127;
            return (sbyte)Math.Round(v * 127f);
        }

        public static float UnAxis(sbyte q) => q / 127f;
    }
}
