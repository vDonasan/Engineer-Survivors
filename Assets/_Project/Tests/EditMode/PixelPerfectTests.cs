using ES.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ES.Tests
{
    public class PixelPerfectTests
    {
        const string CharPath = "Assets/_Project/Art/Characters/eletrica/char_eletrica_idle_down_0.png";
        const string TilePath = "Assets/_Project/Art/Tilesets/tile_floor_0.png";

        [Test]
        public void Art_CharacterImport_IsPixelPerfect()
        {
            foreach (var path in new[] { CharPath, TilePath })
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(ti, Is.Not.Null, path);
                Assert.That(ti.filterMode, Is.EqualTo(FilterMode.Point), path);
                Assert.That(ti.spritePixelsPerUnit, Is.EqualTo(32f), path);
                Assert.That(ti.mipmapEnabled, Is.False, path);
                Assert.That(ti.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
            }
        }

        [Test]
        public void Art_CharacterPivot_AtFeetFromManifest()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CharPath);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.pivot.x, Is.EqualTo(16f).Within(1e-3));
            Assert.That(sprite.pivot.y, Is.EqualTo(1f).Within(1e-3));
            Assert.That(sprite.rect.width, Is.EqualTo(32f));
        }

        [Test]
        public void Quality_AntiAliasingAndAnisoDisabled()
        {
            Assert.That(QualitySettings.antiAliasing, Is.EqualTo(0));
            Assert.That(QualitySettings.anisotropicFiltering, Is.EqualTo(AnisotropicFiltering.Disable));
        }

        [Test]
        public void Urp_NoMsaaNoHdr()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.That(urp, Is.Not.Null, "nenhum URP ativo no nível de qualidade atual");
            Assert.That(urp.msaaSampleCount, Is.EqualTo(1));
            Assert.That(urp.supportsHDR, Is.False);
        }

        [Test]
        public void PixelPerfectSetup_AddsUrpPixelPerfectCamera()
        {
            var go = new GameObject("cam");
            try
            {
                var cam = go.AddComponent<Camera>();
                PixelPerfectSetup.Apply(cam);
                var ppc = go.GetComponent<PixelPerfectCamera>();
                Assert.That(ppc, Is.Not.Null);
                Assert.That(ppc.assetsPPU, Is.EqualTo(32));
                Assert.That(ppc.refResolutionX, Is.EqualTo(640));
                Assert.That(ppc.refResolutionY, Is.EqualTo(360));
                Assert.That(ppc.gridSnapping, Is.EqualTo(PixelPerfectCamera.GridSnapping.PixelSnapping));
                Assert.That(cam.orthographic, Is.True);
                Assert.That(cam.allowHDR, Is.False);
                Assert.That(cam.allowMSAA, Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
