using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ski.Game
{
    /// <summary>
    /// The graphics settings: a preset (low, medium, high) and the resolution the 3D view is drawn
    /// at. Applied to the render pipeline at once; nothing needs a restart.
    /// </summary>
    public static class GraphicsQuality
    {
        public static readonly string[] Names = { "LOW", "MEDIUM", "HIGH" };

        /// <summary>Whether people further off and in crowds get the light treatment (no faces, fewer shadows).</summary>
        public static bool Light => SaveData.Quality == 0;

        public static void Apply()
        {
            int q = Mathf.Clamp(SaveData.Quality, 0, 2);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = Mathf.Clamp(SaveData.RenderScale, 0.5f, 1f);
                urp.msaaSampleCount = q == 2 ? 2 : 1;          // the cameras smooth edges with FXAA already
                urp.supportsHDR = q == 2;
                urp.shadowDistance = q == 0 ? 60f : q == 1 ? 100f : 140f;
                urp.shadowCascadeCount = q == 0 ? 1 : 2;
            }
            var sun = WorldLook.Instance != null ? WorldLook.Instance.Sun : null;
            if (sun != null) sun.shadows = q == 0 ? LightShadows.Hard : LightShadows.Soft;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }
    }
}
