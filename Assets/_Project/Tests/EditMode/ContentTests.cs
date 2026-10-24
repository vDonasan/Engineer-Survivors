using System.IO;
using ES.Content;
using ES.Sim;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ES.Tests
{
    public class ContentTests
    {
        const string DatabasePath = "Assets/_Project/Content/ContentDatabase.asset";
        const string JsonPath = "Assets/_Project/Content/Data/sp1-content.json";
        const string HashPath = "Assets/_Project/Content/Data/sp1-content.hash";

        static ContentDatabaseAsset Database => AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(DatabasePath);

        [Test]
        public void Content_DatabaseAssetExists()
        {
            Assert.That(Database, Is.Not.Null, "rode Engineering Survivors/Conteúdo/Importar JSON");
        }

        [Test]
        public void Content_BuildsWithoutErrors()
        {
            var result = Database.Build();
            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
        }

        [Test]
        public void Content_AssetsMatchVersionedJson()
        {
            var fromJson = JsonUtility.FromJson<ContentDef>(File.ReadAllText(JsonPath));
            Assert.That(ContentCanonical.Hash(Database.ToContentDef()), Is.EqualTo(ContentCanonical.Hash(fromJson)),
                "ScriptableObjects e JSON divergem: importe ou exporte o conteúdo");
        }

        [Test]
        public void Content_HashEqualsDotnetHash()
        {
            var fromJson = JsonUtility.FromJson<ContentDef>(File.ReadAllText(JsonPath));
            ulong expected = ulong.Parse(File.ReadAllText(HashPath).Trim(), System.Globalization.NumberStyles.HexNumber);
            Assert.That(ContentCanonical.Hash(fromJson), Is.EqualTo(expected), "JsonUtility (Unity) e System.Text.Json (dotnet) leem o JSON de forma diferente");
        }

        [Test]
        public void Content_EveryAbilityHasAsset()
        {
            var db = Database.Build().Db;
            Assert.That(Database.abilities.Count, Is.EqualTo(db.AbilityCount));
            foreach (var a in Database.abilities) Assert.That(a.def.id, Is.Not.Empty);
        }

        [Test]
        public void Content_AllAbilitiesHaveIcons()
        {
            foreach (var a in Database.abilities) Assert.That(a.icon, Is.Not.Null, a.def.id);
        }
    }
}
