// What a frame costs right now: draw calls, batches, triangles, shadow casters (profiler counters, started on
// the first call and read on the next), how many people and renderers there are, and the pipeline's settings.
var sb = new System.Text.StringBuilder();
var recs = (System.Collections.Generic.List<Unity.Profiling.ProfilerRecorder>)System.AppDomain.CurrentDomain.GetData("perf_recs");
if (recs == null)
{
    recs = new System.Collections.Generic.List<Unity.Profiling.ProfilerRecorder>();
    foreach (var n in new[] { "Batches Count", "Draw Calls Count", "SetPass Calls Count", "Triangles Count", "Shadow Casters Count" })
        recs.Add(Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, n, 4));
    System.AppDomain.CurrentDomain.SetData("perf_recs", recs);
    return "counters started: run again";
}
foreach (var r in recs) sb.Append(r.Valid ? r.CurrentValue.ToString() + " " : "- ");
sb.AppendLine(" (batches, draws, setpass, tris, shadow casters)");
foreach (var r in recs) r.Dispose();
System.AppDomain.CurrentDomain.SetData("perf_recs", null);
var people = UnityEngine.Object.FindObjectsByType<Ski.Game.Person>();
int visible = 0; foreach (var p in people) { var r = p.GetComponentInChildren<UnityEngine.Renderer>(); if (r != null && r.isVisible) visible++; }
var rends = UnityEngine.Object.FindObjectsByType<UnityEngine.MeshRenderer>();
int castOn = 0, rVisible = 0; long tris = 0;
foreach (var r in rends) { if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) castOn++; if (r.isVisible) rVisible++; var mf = r.GetComponent<UnityEngine.MeshFilter>(); if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3; }
var skinned = UnityEngine.Object.FindObjectsByType<UnityEngine.SkinnedMeshRenderer>();
sb.AppendLine("people " + people.Length + " (visible " + visible + ")  meshRenderers " + rends.Length + " (visible " + rVisible + ", casting " + castOn + ")  skinned " + skinned.Length + "  scene tris " + (tris / 1000) + "k");
var ps = UnityEngine.Object.FindObjectsByType<UnityEngine.ParticleSystem>();
int parts = 0; foreach (var p in ps) parts += p.particleCount;
sb.AppendLine("particle systems " + ps.Length + " particles " + parts + "  fps(editor) " + (1f / UnityEngine.Time.smoothDeltaTime).ToString("0"));
var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
if (urp != null) sb.AppendLine("urp: scale " + urp.renderScale + " msaa " + urp.msaaSampleCount + " hdr " + urp.supportsHDR + " shadowDist " + urp.shadowDistance + " cascades " + urp.shadowCascadeCount
          + " shadowRes " + urp.mainLightShadowmapResolution + " depthTex " + urp.supportsCameraDepthTexture + " opaqueTex " + urp.supportsCameraOpaqueTexture);
var cam = UnityEngine.Camera.main;
if (cam != null)
{
    var data = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
    sb.AppendLine("camera far " + cam.farClipPlane + " postFX " + (data != null && data.renderPostProcessing) + " AA " + (data != null ? data.antialiasing.ToString() : "-"));
}
return sb.ToString();
