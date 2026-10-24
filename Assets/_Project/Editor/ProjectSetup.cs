using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ES.EditorTools
{
    /// <summary>
    /// Configurações do projeto que não devem depender de cliques no editor.
    /// Executável em batchmode: -executeMethod ES.EditorTools.ProjectSetup.Apply
    /// </summary>
    public static class ProjectSetup
    {
        public const string Version = "0.1.0";

        [MenuItem("Engineering Survivors/Projeto/Aplicar configurações")]
        public static void Apply()
        {
            PlayerSettings.companyName = "Engineering Survivors";
            PlayerSettings.productName = "Engineering Survivors";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.runInBackground = true;

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);

            // Pixel perfect: nada de suavização em lugar nenhum.
            var levels = QualitySettings.names;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < levels.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.antiAliasing = 0;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            }
            QualitySettings.SetQualityLevel(current, false);
            // No Unity 6 o asset do URP fica em cada nível de qualidade (e opcionalmente no padrão).
            var pipelines = new System.Collections.Generic.List<UnityEngine.Rendering.RenderPipelineAsset> { UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline };
            for (int i = 0; i < levels.Length; i++) pipelines.Add(QualitySettings.GetRenderPipelineAssetAt(i));
            foreach (var asset in pipelines)
            {
                if (!(asset is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)) continue;
                urp.msaaSampleCount = 1;
                urp.supportsHDR = false;
                EditorUtility.SetDirty(urp);
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            AssetDatabase.SaveAssets();
            Debug.Log("[ES] Configurações do projeto aplicadas.");
        }
    }
}
