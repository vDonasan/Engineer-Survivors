using ES.Sim;
using UnityEngine;

namespace ES.Game
{
    /// <summary>Ordem de desenho e conversões da simulação para a cena (1 u = 32 px).</summary>
    public static class Layers
    {
        public const int Floor = -1000, Decal = -990, Obstacle = -900, Gem = -500, Shadow = -400;
        public const int Fx = 6000, Hostile = 6500, Numbers = 7000;
        public const float Pixel = 1f / PixelPerfectSetup.AssetsPPU;

        /// <summary>Atores mais abaixo na tela ficam na frente.</summary>
        public static int Actor(float y) => 5000 - Mathf.RoundToInt(y * PixelPerfectSetup.AssetsPPU);

        public static Vector2 V(Vec2 v) => new Vector2(v.X, v.Y);

        /// <summary>Arredonda para a grade de pixels.</summary>
        public static Vector2 Snap(Vector2 v) => new Vector2(Mathf.Round(v.x * 32f) / 32f, Mathf.Round(v.y * 32f) / 32f);

        /// <summary>0 baixo, 1 cima, 2 esquerda, 3 direita.</summary>
        public static int Dir(Vector2 v, int fallback)
        {
            if (v.sqrMagnitude < 1e-4f) return fallback;
            if (Mathf.Abs(v.x) > Mathf.Abs(v.y)) return v.x < 0 ? 2 : 3;
            return v.y < 0 ? 0 : 1;
        }

        public static readonly string[] DirNames = { "down", "up", "left", "right" };

        public static readonly Color[] PlayerColors =
        {
            new Color32(0xff, 0xb5, 0x2e, 0xff), new Color32(0x3f, 0xe0, 0xff, 0xff),
            new Color32(0xb8, 0xe0, 0x2c, 0xff), new Color32(0xff, 0x3d, 0xf0, 0xff),
        };
    }
}
