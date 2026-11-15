using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Ski.EditorTools
{
    /// <summary>
    /// One-time, repeatable project configuration: the empty Main scene, build settings, render
    /// pipeline and WebGL player settings. Run with: unity command script eval
    /// "return Ski.EditorTools.ProjectSetup.Run();"
    /// </summary>
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/_Game/Scenes/Main.unity";

        public static string Run()
        {
            var log = new System.Text.StringBuilder();

            // --- Main scene: empty, with linear fog saved in it so fog shader variants are kept in builds.
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 140f;
                RenderSettings.fogEndDistance = 1100f;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                EditorSceneManager.SaveScene(scene, ScenePath);
                log.AppendLine("created " + ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            // --- Render pipeline: one look everywhere. Forward path (the toon shader loops over lights itself).
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                asset.shadowDistance = 140f;
                asset.shadowCascadeCount = 2;
                asset.msaaSampleCount = 4;
                asset.supportsHDR = true;
                asset.renderScale = 1f;
                asset.maxAdditionalLightsCount = 6;
                var so = new SerializedObject(asset);
                Set(so, "m_MainLightShadowmapResolution", 2048);
                Set(so, "m_SoftShadowsSupported", true);
                Set(so, "m_AdditionalLightsRenderingMode", 1); // per pixel
                Set(so, "m_SupportsCameraDepthTexture", false);
                Set(so, "m_SupportsCameraOpaqueTexture", false);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                log.AppendLine("configured " + asset.name);
            }
            // The comic ink lines: a full-screen pass on depth and normals, on every renderer.
            Directory.CreateDirectory("Assets/_Game/Resources/Materials");
            const string inkMatPath = "Assets/_Game/Resources/Materials/InkEdges.mat";
            var inkShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Resources/Shaders/SkiInkEdges.shader");
            var inkMat = AssetDatabase.LoadAssetAtPath<Material>(inkMatPath);
            if (inkMat == null && inkShader != null)
            {
                inkMat = new Material(inkShader);
                AssetDatabase.CreateAsset(inkMat, inkMatPath);
                log.AppendLine("created " + inkMatPath);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                data.renderingMode = RenderingMode.Forward;
                FullScreenPassRendererFeature ink = null;
                foreach (var f in data.rendererFeatures)
                {
                    if (f == null) continue;
                    if (f is FullScreenPassRendererFeature fs && f.name == "InkEdges") ink = fs;
                    else f.SetActive(false); // no SSAO on a toon look
                }
                if (ink == null && inkMat != null)
                {
                    ink = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                    ink.name = "InkEdges";
                    AssetDatabase.AddObjectToAsset(ink, data);
                    var so = new SerializedObject(data);
                    var feats = so.FindProperty("m_RendererFeatures");
                    feats.arraySize++;
                    feats.GetArrayElementAtIndex(feats.arraySize - 1).objectReferenceValue = ink;
                    var map = so.FindProperty("m_RendererFeatureMap");
                    if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ink, out _, out long localId))
                    {
                        map.arraySize++;
                        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    log.AppendLine("added InkEdges to " + data.name);
                }
                if (ink != null)
                {
                    ink.passMaterial = inkMat;
                    ink.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
                    ink.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Color;
                    ink.fetchColorBuffer = true;
                    ink.SetActive(true);
                    EditorUtility.SetDirty(ink);
                }
                EditorUtility.SetDirty(data);
                log.AppendLine("renderer " + data.name + " -> Forward");
            }
            // Use the PC tier on every platform so the editor and the web build match.
            int pc = System.Array.IndexOf(QualitySettings.names, "PC");
            if (pc >= 0)
            {
                QualitySettings.SetQualityLevel(pc, true);
                var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
                var perPlatform = qs.FindProperty("m_PerPlatformDefaultQuality");
                if (perPlatform != null)
                {
                    for (int i = 0; i < perPlatform.arraySize; i++) perPlatform.GetArrayElementAtIndex(i).FindPropertyRelative("second").intValue = pc;
                    qs.ApplyModifiedPropertiesWithoutUndo();
                }
                QualitySettings.vSyncCount = 1;
                QualitySettings.antiAliasing = 4;
            }

            // --- UI Toolkit: a theme and panel settings the game loads from Resources.
            Directory.CreateDirectory("Assets/_Game/Resources/UI");
            const string themePath = "Assets/_Game/Resources/UI/GameTheme.tss", panelPath = "Assets/_Game/Resources/UI/Panel.asset";
            if (!File.Exists(themePath))
            {
                File.WriteAllText(themePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(themePath);
            }
            var panel = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(panelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
                AssetDatabase.CreateAsset(panel, panelPath);
                log.AppendLine("created " + panelPath);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.ThemeStyleSheet>(themePath);
            panel.scaleMode = UnityEngine.UIElements.PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = UnityEngine.UIElements.PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);

            // --- Player.
            PlayerSettings.companyName = "Last Run";
            PlayerSettings.productName = "Last Run";
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true; // GitHub Pages cannot set Content-Encoding
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.template = "PROJECT:LastRun";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);

            AssetDatabase.SaveAssets();
            log.AppendLine("scenes in build: " + EditorBuildSettings.scenes.Length + ", quality: " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
            return log.ToString();
        }

        static void Set(SerializedObject so, string name, int value) { var p = so.FindProperty(name); if (p != null) p.intValue = value; }
        static void Set(SerializedObject so, string name, bool value) { var p = so.FindProperty(name); if (p != null) p.boolValue = value; }

        /// <summary>Opens the Main scene in the Editor (batch mode starts with an empty one).</summary>
        public static string OpenMain()
        {
            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            PlayModeWindow.SetCustomRenderingResolution(1280, 720, "HD 720");
            if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
            return SceneManager.GetActiveScene().path;
        }
    }
}
