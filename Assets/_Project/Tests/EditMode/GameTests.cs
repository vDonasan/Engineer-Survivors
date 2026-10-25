using System.IO;
using System.Linq;
using ES.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ES.Tests
{
    public class GameTests
    {
        const string CatalogPath = "Assets/_Project/Game/GameAssets.asset";

        static GameAssets Catalog => AssetDatabase.LoadAssetAtPath<GameAssets>(CatalogPath);

        [Test]
        public void Catalog_HasEveryManifestSprite()
        {
            Assert.That(Catalog, Is.Not.Null, "rode Engineering Survivors/Atualizar catálogo de arte e áudio");
            var manifest = File.ReadAllText("Assets/_Project/Art/manifest.json");
            var ids = System.Text.RegularExpressions.Regex.Matches(manifest, "\"id\":\\s*\"([^\"]+)\"").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.That(ids.Count, Is.GreaterThan(400));
            Catalog.Invalidate();
            var missing = ids.Where(id => !Catalog.HasSprite(id)).ToList();
            Assert.That(missing, Is.Empty, string.Join(", ", missing.Take(10)));
        }

        [Test]
        public void Catalog_HasEverySoundAndFont()
        {
            var wavs = Directory.GetFiles("Assets/_Project/Audio", "*.wav", SearchOption.AllDirectories).Select(Path.GetFileNameWithoutExtension).ToList();
            Assert.That(wavs.Count, Is.GreaterThanOrEqualTo(18));
            foreach (var id in wavs) Assert.That(Catalog.Clip(id), Is.Not.Null, id);
            Assert.That(Catalog.font, Is.Not.Null);
            Assert.That(Catalog.strings, Is.Not.Null);
        }

        [Test]
        public void Catalog_SpritesArePointFilteredAt32Ppu()
        {
            foreach (var e in Catalog.sprites.Take(60))
            {
                Assert.That(e.sprite.pixelsPerUnit, Is.EqualTo(32f), e.id);
                Assert.That(e.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), e.id);
            }
        }

        [Test]
        public void VoiceLimiter_FourPerSound_ThenFreesWhenOneEnds()
        {
            var v = new VoiceLimiter(4, 24);
            for (int i = 0; i < 4; i++) Assert.That(v.TryAcquire("arco", 0, 0.2), Is.True);
            Assert.That(v.TryAcquire("arco", 0.1, 0.2), Is.False, "5ª voz do mesmo som");
            Assert.That(v.TryAcquire("gema", 0.1, 0.2), Is.True, "outro som não é afetado");
            Assert.That(v.TryAcquire("arco", 0.25, 0.2), Is.True, "voz liberada ao terminar");
        }

        [Test]
        public void VoiceLimiter_TotalCap()
        {
            var v = new VoiceLimiter(4, 6);
            int ok = 0;
            for (int i = 0; i < 10; i++) if (v.TryAcquire("s" + i, 0, 1)) ok++;
            Assert.That(ok, Is.EqualTo(6));
        }

        [Test]
        public void ViewPool_ReusesInstances()
        {
            var parent = new GameObject("pool");
            try
            {
                var pool = new ViewPool<SpriteRenderer>(() => new GameObject("v", typeof(SpriteRenderer)) { transform = { parent = parent.transform } }.GetComponent<SpriteRenderer>());
                var a = pool.Get();
                var b = pool.Get();
                pool.Release(a);
                Assert.That(a.gameObject.activeSelf, Is.False);
                var c = pool.Get();
                Assert.That(c, Is.SameAs(a));
                Assert.That(c.gameObject.activeSelf, Is.True);
                Assert.That(pool.Created, Is.EqualTo(2));
                Assert.That(b, Is.Not.SameAs(a));
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void Settings_SaveLoad_RoundTripAndClamp()
        {
            string old = PlayerPrefs.GetString(SettingsStore.Key, null);
            try
            {
                var s = new SettingsStore { Master = 0.3f, Shake = 2f, UiScale = 5f, Effects = EffectsLevel.Low, SpecialBinding = "<Keyboard>/f", PlayerName = "Ana" };
                s.Save();
                var r = new SettingsStore();
                r.Load();
                Assert.That(r.Master, Is.EqualTo(0.3f).Within(1e-4));
                Assert.That(r.Shake, Is.EqualTo(1f));
                Assert.That(r.UiScale, Is.EqualTo(1.3f).Within(1e-4));
                Assert.That(r.Effects, Is.EqualTo(EffectsLevel.Low));
                Assert.That(r.SpecialBinding, Is.EqualTo("<Keyboard>/f"));
                Assert.That(r.PlayerName, Is.EqualTo("Ana"));
                PlayerPrefs.SetString(SettingsStore.Key, "{lixo");
                var g = new SettingsStore();
                Assert.DoesNotThrow(() => g.Load());
                Assert.That(g.SpecialBinding, Is.EqualTo(SettingsStore.DefaultSpecialBinding));
            }
            finally
            {
                if (old == null) PlayerPrefs.DeleteKey(SettingsStore.Key);
                else PlayerPrefs.SetString(SettingsStore.Key, old);
            }
        }

        [Test]
        public void GameInput_UsesSavedSpecialBinding()
        {
            using var input = new GameInput("<Keyboard>/f");
            Assert.That(input.SpecialPath, Is.EqualTo("<Keyboard>/f"));
            input.SetSpecialBinding("<Keyboard>/q");
            Assert.That(input.SpecialPath, Is.EqualTo("<Keyboard>/q"));
        }
    }
}
