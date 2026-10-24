using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ES.EditorTools
{
    /// <summary>
    /// Configura a importação da arte gerada por ArtSource: sprite único, 64 px por
    /// unidade, sem mipmaps, pivô e bordas (9-slice) vindos do manifest.json.
    /// </summary>
    public sealed class ArtImportSettings : AssetPostprocessor
    {
        const string ArtRoot = "Assets/_Project/Art/";
        public const float PixelsPerUnit = 64f;

        [Serializable] sealed class Entry { public string id; public string file; public float[] pivot; public int[] border; }
        [Serializable] sealed class Manifest { public Entry[] sprites; }

        static Dictionary<string, Entry> _byFile;

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
            ti.filterMode = FilterMode.Bilinear;
            ti.alphaIsTransparency = true;
            ti.wrapMode = assetPath.Contains("/Arena/tile_") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Compressed;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            if (Entries().TryGetValue(assetPath, out var e))
            {
                if (e.pivot != null && e.pivot.Length == 2)
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(e.pivot[0], e.pivot[1]);
                }
                if (e.border != null && e.border.Length == 4)
                    settings.spriteBorder = new Vector4(e.border[0], e.border[1], e.border[2], e.border[3]);
            }
            ti.SetTextureSettings(settings);
        }
    }
}
