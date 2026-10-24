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

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            AssetDatabase.SaveAssets();
            Debug.Log("[ES] Configurações do projeto aplicadas.");
        }
    }
}
