using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ES.EditorTools
{
    /// <summary>
    /// Importação pixel perfect da arte gerada por ArtSource: filtro Point, sem
    /// compressão, sem mipmaps, 32 px por unidade, pivô e bordas (9-slice) em pixels
    /// vindos do manifest.json.
    /// </summary>
    public sealed class ArtImportSettings : AssetPostprocessor
    {
        const string ArtRoot = "Assets/_Project/Art/";
        public const float PixelsPerUnit = 32f;

        [Serializable] sealed class Entry { public string id; public string file; public int w; public int h; public float[] pivot; public int[] border; }
        [Serializable] sealed class Manifest { public Entry[] sprites; }

        static Dictionary<string, Entry> _byFile;

        public override uint GetVersion() => 2;

        static Dictionary<string, Entry> Entries()
        {
            if (_byFile != null) return _byFile;
            _byFile = new Dictionary<string, Entry>();
            string path = ArtRoot + "manifest.json";
            if (!File.Exists(path)) return _byFile;
            var m = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            foreach (var e in m.sprites) _byFile[ArtRoot + e.file] = e;
            return _byFile;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot, StringComparison.Ordinal)) return;
            _byFile = null; // o manifesto pode ter mudado entre importações
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = PixelsPerUnit;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.wrapMode = assetPath.Contains("/Tilesets/") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            if (Entries().TryGetValue(assetPath, out var e))
            {
                if (e.pivot != null && e.pivot.Length == 2 && e.w > 0 && e.h > 0)
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(e.pivot[0] / e.w, e.pivot[1] / e.h);
                }
                if (e.border != null && e.border.Length == 4)
                    settings.spriteBorder = new Vector4(e.border[0], e.border[1], e.border[2], e.border[3]);
            }
            ti.SetTextureSettings(settings);
        }
    }
}
