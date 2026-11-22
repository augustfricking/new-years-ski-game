using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ski.Game
{
    /// <summary>
    /// The graphics settings: five parts, each low, medium or high (shadows, people, view distance, ink
    /// lines, effects), a preset that sets them all, and the resolution the 3D view is drawn at. Applied
    /// at once; nothing needs a restart.
    /// </summary>
    public static class GraphicsQuality
    {
        public static readonly string[] Names = { "LOW", "MEDIUM", "HIGH" };

        /// <summary>Bumped on every change, so people and cameras can follow.</summary>
        public static int Version { get; private set; }

        static int Level(int v) => Mathf.Clamp(v, 0, 2);

        /// <summary>The preset all five parts share, or -1 when they differ.</summary>
        public static int Preset
        {
            get
            {
                int s = SaveData.Shadows;
                return s == SaveData.People && s == SaveData.View && s == SaveData.Lines && s == SaveData.Effects ? s : -1;
            }
        }

        public static void SetPreset(int q)
        {
            SaveData.Shadows = SaveData.People = SaveData.View = SaveData.Lines = SaveData.Effects = Level(q);
            Apply();
        }

        /// <summary>People on low: crowds without faces or shadows.</summary>
        public static bool Light => SaveData.People == 0;
        /// <summary>How far off someone in the crowd is still drawn (metres).</summary>
        public static float PeopleReach => SaveData.People == 0 ? 70f : SaveData.People == 1 ? 150f : 100000f;
        /// <summary>Whether the crowd casts shadows.</summary>
        public static bool CrowdShadows => SaveData.Shadows > 0 && SaveData.People > 0;
        /// <summary>How clear the air is: the fog's distances are scaled by this.</summary>
        public static float Reach => SaveData.View == 0 ? 0.55f : SaveData.View == 1 ? 0.8f : 1f;
        /// <summary>The far edge of the view outdoors: just past where the fog has swallowed everything.</summary>
        public static float Far
        {
            get
            {
                if (SaveData.View >= 2) return 2500f;
                float fogEnd = WorldLook.Instance != null ? WorldLook.Instance.FogEnd : 1100f;
                return fogEnd * Reach * 1.5f + 100f;
            }
        }

        public static void Apply()
        {
            int sh = Level(SaveData.Shadows), ln = Level(SaveData.Lines), fx = Level(SaveData.Effects);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = Mathf.Clamp(SaveData.RenderScale, 0.5f, 1f);
                urp.msaaSampleCount = fx == 2 ? 2 : 1;          // the cameras smooth edges with FXAA already
                urp.supportsHDR = fx == 2;
                urp.shadowDistance = sh == 0 ? 45f : sh == 1 ? 100f : 150f;
                urp.shadowCascadeCount = sh == 0 ? 1 : 2;
                urp.mainLightShadowmapResolution = sh == 0 ? 1024 : 2048;
                // Ink lines on low come from depth alone: no pass that draws the whole scene again for its normals.
                foreach (var data in urp.rendererDataList)
                {
                    if (data == null) continue;
                    foreach (var f in data.rendererFeatures)
                        if (f is FullScreenPassRendererFeature ink && f.name == "InkEdges")
                            ink.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color | (ln > 0 ? ScriptableRenderPassInput.Normal : ScriptableRenderPassInput.None);
                }
            }
            var mat = Resources.Load<Material>("Materials/InkEdges");
            if (mat != null)
            {
                // The inked contour, a touch heavier than the default, as in a comic.
                mat.SetFloat("_Thickness", 2.1f);
                mat.SetFloat("_InnerThickness", 0.9f);
                mat.SetFloat("_UseNormals", ln > 0 ? 1f : 0f);
                mat.SetFloat("_InnerEnd", ln == 2 ? 40f : 28f);
                mat.SetFloat("_FadeEnd", ln == 2 ? 190f : 140f);
            }
            var look = WorldLook.Instance;
            if (look != null)
            {
                if (look.Sun != null) look.Sun.shadows = sh == 0 ? LightShadows.Hard : LightShadows.Soft;
                look.Reach = Reach;
                look.Effects = fx;
                look.Apply();
            }
            // The camera outdoors follows the view distance (the tavern and the map keep their own).
            var cam = Camera.main;
            if (cam != null && cam.farClipPlane > 400f) cam.farClipPlane = Far;
            QualitySettings.vSyncCount = 0;
#if UNITY_WEBGL && !UNITY_EDITOR
            // In the browser the frame comes with the screen's refresh (requestAnimationFrame); a target rate
            // would switch to a timer instead, which stutters.
            Application.targetFrameRate = -1;
#else
            Application.targetFrameRate = 60;
#endif
            Version++;
        }
    }
}
