// Where the renderers are: mesh renderers grouped by their top-level object (count, shadow casters, triangles),
// the number of distinct materials and shaders, and whether the SRP Batcher can batch them.
var sb = new System.Text.StringBuilder();
var groups = new System.Collections.Generic.Dictionary<string, int[]>();
var mats = new System.Collections.Generic.HashSet<UnityEngine.Material>();
var shaders = new System.Collections.Generic.Dictionary<string, int>();
int mpb = 0;
foreach (var r in UnityEngine.Object.FindObjectsByType<UnityEngine.Renderer>())
{
    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
    var t = r.transform; while (t.parent != null && t.parent.parent != null) t = t.parent;
    string key = (t.parent != null ? t.parent.name + "/" : "") + t.name;
    if (r.GetComponentInParent<Ski.Game.Person>() != null) key = "Person";
    if (!groups.TryGetValue(key, out var g)) groups[key] = g = new int[4];
    g[0]++;
    if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) g[1]++;
    var mf = r.GetComponent<UnityEngine.MeshFilter>();
    if (mf != null && mf.sharedMesh != null) g[2] += (int)(mf.sharedMesh.GetIndexCount(0) / 3);
    if (r.isVisible) g[3]++;
    if (r.HasPropertyBlock()) mpb++;
    foreach (var m in r.sharedMaterials) if (m != null) { mats.Add(m); }
}
foreach (var m in mats) { string n = m.shader != null ? m.shader.name : "-"; shaders[n] = shaders.TryGetValue(n, out var c) ? c + 1 : 1; }
var list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int[]>>(groups);
list.Sort((a, b) => b.Value[0].CompareTo(a.Value[0]));
foreach (var kv in list) if (kv.Value[0] >= 3) sb.AppendLine(kv.Key + ": " + kv.Value[0] + " (casting " + kv.Value[1] + ", visible " + kv.Value[3] + ", tris " + kv.Value[2] / 1000 + "k)");
sb.AppendLine("materials " + mats.Count + "  with property blocks " + mpb + "  srpBatcher " + UnityEngine.Rendering.GraphicsSettings.useScriptableRenderPipelineBatching);
foreach (var kv in shaders) sb.AppendLine("  " + kv.Key + " x" + kv.Value);
return sb.ToString();
