using System;
using System.IO;
using System.Linq;
using ES.Content;
using ES.Game.Proof;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ES.GameEditor
{
    /// <summary>
    /// Builds em batchmode: -executeMethod ES.GameEditor.BuildScripts.BuildWebGL -esScene NetProof -esOut Build/WebGL-proof
    /// </summary>
    public static class BuildScripts
    {
        const string ScenesDir = "Assets/_Project/Scenes";
        const string ContentPath = "Assets/_Project/Content/ContentDatabase.asset";

        [MenuItem("Engineering Survivors/Cenas/Recriar cena de prova de rede")]
        public static void CreateNetProofScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(light.gameObject);
            var cam = Camera.main;
            cam.orthographic = true;
            cam.orthographicSize = 9;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            var go = new GameObject("NetProof");
            go.AddComponent<NetProof>().content = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(ContentPath);
            Directory.CreateDirectory(ScenesDir);
            EditorSceneManager.SaveScene(scene, $"{ScenesDir}/NetProof.unity");
        }

        public static void BuildWebGL()
        {
            string sceneName = Arg("-esScene") ?? "NetProof";
            string output = Arg("-esOut") ?? "Build/WebGL";
            ES.EditorTools.ProjectSetup.Apply();
            if (sceneName == "NetProof" && !File.Exists($"{ScenesDir}/NetProof.unity")) CreateNetProofScene();

            string scenePath = $"{ScenesDir}/{sceneName}.unity";
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[ES] Build {s.result}: {s.totalSize / (1024 * 1024)} MB em {s.totalTime.TotalSeconds:0}s, {s.totalErrors} erros -> {output}");
            if (s.result != BuildResult.Succeeded)
            {
                foreach (var step in report.steps)
                    foreach (var m in step.messages.Where(m => m.type == LogType.Error)) Debug.LogError("[ES] " + m.content);
                EditorApplication.Exit(1);
            }
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
