using System.Collections.Generic;
using System.IO;
using ES.Content;
using ES.Sim;
using UnityEditor;
using UnityEngine;

namespace ES.ContentEditor
{
    /// <summary>
    /// Importa sp1-content.json para ScriptableObjects (preservando referências visuais)
    /// e exporta de volta. Batch: -executeMethod ES.ContentEditor.ContentJsonSync.Import
    /// </summary>
    public static class ContentJsonSync
    {
        public const string JsonPath = "Assets/_Project/Content/Data/sp1-content.json";
        public const string DatabasePath = "Assets/_Project/Content/ContentDatabase.asset";
        const string AssetRoot = "Assets/_Project/Content/Assets";

        [MenuItem("Engineering Survivors/Conteúdo/Importar JSON")]
        public static void Import()
        {
            var def = JsonUtility.FromJson<ContentDef>(File.ReadAllText(JsonPath));
            var result = GameDatabase.Build(def);
            if (result.Errors.Length > 0)
            {
                Debug.LogError("[ES] Conteúdo inválido:\n" + string.Join("\n", result.Errors));
                return;
            }

            var db = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<ContentDatabaseAsset>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            db.rules = def.rules;
            db.balance = def.balance;
            db.spawn = def.spawn;
            db.arenas = Sync<ArenaAsset, ArenaDef>("Arenas", def.arenas, d => d.id, (a, d) => a.def = d);
            db.specializations = Sync<SpecializationAsset, SpecializationDef>("Specializations", def.specializations, d => d.id, (a, d) =>
            {
                a.def = d;
                a.portrait = Sprite($"Assets/_Project/Art/Portraits/portrait_{d.id}_determinacao.png") ?? a.portrait;
            });
            db.specials = Sync<SpecialAsset, SpecialDef>("Specials", def.specials, d => d.id, (a, d) =>
            {
                a.def = d;
                a.icon = Sprite($"Assets/_Project/Art/Abilities/icon_{d.id}.png") ?? a.icon;
            });
            db.abilities = Sync<AbilityAsset, AbilityDef>("Abilities", def.abilities, d => d.id, (a, d) =>
            {
                a.def = d;
                a.icon = Sprite($"Assets/_Project/Art/Abilities/icon_{d.id}.png") ?? a.icon;
            });
            db.enemies = Sync<EnemyAsset, EnemyDef>("Enemies", def.enemies, d => d.id, (a, d) => a.def = d);
            db.synergies = Sync<SynergyAsset, SynergyDef>("Synergies", def.synergies, d => d.id, (a, d) => a.def = d);
            db.evolutions = Sync<EvolutionAsset, EvolutionDef>("Evolutions", def.evolutions, d => d.id, (a, d) => a.def = d);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ES] Conteúdo importado: {db.abilities.Count} habilidades, {db.enemies.Count} inimigos; hash {result.Db.ContentHash:x16}");
        }

        [MenuItem("Engineering Survivors/Conteúdo/Exportar JSON")]
        public static void Export()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(DatabasePath);
            var def = db.ToContentDef();
            var result = GameDatabase.Build(def);
            if (result.Errors.Length > 0)
            {
                Debug.LogError("[ES] Não exportado, conteúdo inválido:\n" + string.Join("\n", result.Errors));
                return;
            }
            File.WriteAllText(JsonPath, JsonUtility.ToJson(def, true) + "\n");
            AssetDatabase.ImportAsset(JsonPath);
            Debug.Log($"[ES] JSON exportado. Atualize sp1-content.hash para {result.Db.ContentHash:x16} (o teste dotnet confere).");
        }

        static List<TAsset> Sync<TAsset, TDef>(string folder, TDef[] defs, System.Func<TDef, string> id, System.Action<TAsset, TDef> apply)
            where TAsset : ScriptableObject
        {
            string dir = $"{AssetRoot}/{folder}";
            Directory.CreateDirectory(dir);
            var list = new List<TAsset>();
            foreach (var d in defs)
            {
                string path = $"{dir}/{id(d)}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<TAsset>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<TAsset>();
                    AssetDatabase.CreateAsset(asset, path);
                }
                apply(asset, d);
                EditorUtility.SetDirty(asset);
                list.Add(asset);
            }
            return list;
        }

        static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
