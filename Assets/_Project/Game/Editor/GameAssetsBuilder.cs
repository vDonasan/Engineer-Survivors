using System;
using System.IO;
using ES.Game;
using UnityEditor;
using UnityEngine;

namespace ES.GameEditor
{
    /// <summary>Monta o catálogo GameAssets a partir do manifesto da arte e da pasta de áudio.</summary>
    public static class GameAssetsBuilder
    {
        public const string AssetPath = "Assets/_Project/Game/GameAssets.asset";
        public const string ArtDir = "Assets/_Project/Art";
        public const string AudioDir = "Assets/_Project/Audio";
        public const string SelectionDir = "Assets/_Project/UI/Selection";
        public const string FontPath = "Assets/_Project/UI/Fonts/VT323-Regular.ttf";
        public const string StringsPath = "Assets/_Project/UI/Strings/pt-BR.json";

        [Serializable] public class ManifestSprite { public string id, category, file; public int[] anchor; }
        [Serializable] public class Manifest { public ManifestSprite[] sprites = new ManifestSprite[0]; }

        public static Manifest ReadManifest() => JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(ArtDir, "manifest.json")));

        [MenuItem("Engineering Survivors/Atualizar catálogo de arte e áudio")]
        public static GameAssets Rebuild()
        {
            var ga = AssetDatabase.LoadAssetAtPath<GameAssets>(AssetPath);
            if (ga == null)
            {
                ga = ScriptableObject.CreateInstance<GameAssets>();
                AssetDatabase.CreateAsset(ga, AssetPath);
            }
            ga.sprites.Clear();
            int missing = 0;
            foreach (var s in ReadManifest().sprites)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{s.file}");
                if (sprite == null) { missing++; Debug.LogError($"[ES] Sprite não importado: {s.file}"); continue; }
                var anchor = s.anchor != null && s.anchor.Length == 2 ? new Vector2Int(s.anchor[0], s.anchor[1]) : new Vector2Int(-1, -1);
                ga.sprites.Add(new GameAssets.SpriteEntry { id = s.id, sprite = sprite, anchor = anchor });
            }
            // retratos da tela de escolha (imagens fornecidas pelo autor)
            foreach (var path in Directory.GetFiles(SelectionDir, "*.png"))
            {
                var p = path.Replace('\\', '/');
                if (AssetImporter.GetAtPath(p) is TextureImporter ti && ti.textureType != TextureImporterType.Sprite)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.mipmapEnabled = false;
                    ti.SaveAndReimport();
                }
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (sprite != null) ga.sprites.Add(new GameAssets.SpriteEntry { id = Path.GetFileNameWithoutExtension(p), sprite = sprite, anchor = new Vector2Int(-1, -1) });
            }
            ga.clips.Clear();
            foreach (var path in Directory.GetFiles(AudioDir, "*.wav", SearchOption.AllDirectories))
            {
                var p = path.Replace('\\', '/');
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                if (clip != null) ga.clips.Add(new GameAssets.ClipEntry { id = Path.GetFileNameWithoutExtension(p), clip = clip });
            }
            ga.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            ga.strings = AssetDatabase.LoadAssetAtPath<TextAsset>(StringsPath);
            ga.Invalidate();
            EditorUtility.SetDirty(ga);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ES] Catálogo: {ga.sprites.Count} sprites ({missing} ausentes), {ga.clips.Count} sons");
            return ga;
        }
    }
}
