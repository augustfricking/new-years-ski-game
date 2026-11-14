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
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                data.renderingMode = RenderingMode.Forward;
                foreach (var f in data.rendererFeatures) if (f != null) f.SetActive(false); // no SSAO on a toon look
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

            // --- Player.
            PlayerSettings.companyName = "Last Run";
            PlayerSettings.productName = "Last Run";
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true; // GitHub Pages cannot set Content-Encoding
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
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
            if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
            return SceneManager.GetActiveScene().path;
        }
    }
}
