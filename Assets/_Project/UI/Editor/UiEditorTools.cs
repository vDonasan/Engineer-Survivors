using System.IO;
using System.Linq;
using ES.Content;
using ES.Game;
using ES.GameEditor;
using ES.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace ES.UIEditor
{
    /// <summary>Recursos da interface (painel, tema, lista de UXML) e a cena Main.</summary>
    [InitializeOnLoad]
    public static class UiEditorTools
    {
        public const string UiDir = "Assets/_Project/UI";
        public const string UiAssetsPath = UiDir + "/UiAssets.asset";
        public const string PanelPath = UiDir + "/PanelSettings.asset";
        public const string RuntimeThemePath = UiDir + "/Uss/RuntimeTheme.tss";
        public const string ThemeUssPath = UiDir + "/Uss/Theme.uss";
        public const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        const string ContentPath = "Assets/_Project/Content/ContentDatabase.asset";

        static UiEditorTools()
        {
            BuildScripts.SceneCreators["Main"] = CreateMainScene;
        }

        [MenuItem("Engineering Survivors/Interface/Atualizar recursos da interface")]
        public static UiAssets RebuildUiAssets()
        {
            if (!File.Exists(RuntimeThemePath))
            {
                File.WriteAllText(RuntimeThemePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(RuntimeThemePath);
            }
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(RuntimeThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.clearColor = false;
            EditorUtility.SetDirty(panel);

            var ui = AssetDatabase.LoadAssetAtPath<UiAssets>(UiAssetsPath);
            if (ui == null)
            {
                ui = ScriptableObject.CreateInstance<UiAssets>();
                AssetDatabase.CreateAsset(ui, UiAssetsPath);
            }
            ui.screens = Directory.GetFiles(UiDir + "/Uxml", "*.uxml")
                .Select(p => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(p.Replace('\\', '/')))
                .Where(t => t != null).OrderBy(t => t.name).ToArray();
            ui.theme = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemeUssPath);
            ui.panel = panel;
            EditorUtility.SetDirty(ui);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ES] Interface: {ui.screens.Length} telas, tema {(ui.theme != null ? "ok" : "AUSENTE")}");
            return ui;
        }

        [MenuItem("Engineering Survivors/Cenas/Recriar cena Main")]
        public static void CreateMainScene()
        {
            var assets = GameAssetsBuilder.Rebuild();
            var ui = RebuildUiAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x14, 0x16, 0x1b, 0xff);
            camGo.transform.position = new Vector3(32, 32, -10);
            PixelPerfectSetup.Apply(cam);
            camGo.AddComponent<AudioListener>();

            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            var so = new SerializedObject(light);
            so.FindProperty("m_LightType").intValue = (int)Light2D.LightType.Global;
            so.FindProperty("m_Intensity").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            var boot = new GameObject("Game").AddComponent<ES.UI.GameBootstrap>();
            boot.content = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(ContentPath);
            boot.assets = assets;
            boot.ui = ui;

            Directory.CreateDirectory(Path.GetDirectoryName(MainScenePath));
            EditorSceneManager.SaveScene(scene, MainScenePath);
            Debug.Log("[ES] Cena Main criada");
        }
    }
}
