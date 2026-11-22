// The people in the scene: how many of each kind (by object name), renderers each, skinned or not, and how far from the camera.
var sb = new System.Text.StringBuilder();
var cam = UnityEngine.Camera.main;
var kinds = new System.Collections.Generic.Dictionary<string, int[]>();
foreach (var p in UnityEngine.Object.FindObjectsByType<Ski.Game.Person>())
{
    string n = p.name; int cut = n.IndexOfAny("0123456789 (".ToCharArray()); if (cut > 0) n = n.Substring(0, cut);
    if (p.transform.parent != null) n = p.transform.parent.name + "/" + n;
    if (!kinds.TryGetValue(n, out var k)) kinds[n] = k = new int[5];
    var rs = p.GetComponentsInChildren<UnityEngine.Renderer>();
    k[0]++; k[1] += rs.Length;
    foreach (var r in rs) if (r is UnityEngine.SkinnedMeshRenderer) k[2]++;
    float d = cam != null ? UnityEngine.Vector3.Distance(cam.transform.position, p.transform.position) : 0f;
    if (d > 80f) k[3]++;
    if (d > 150f) k[4]++;
}
foreach (var kv in kinds) sb.AppendLine(kv.Key + ": " + kv.Value[0] + " people, " + kv.Value[1] + " renderers (" + kv.Value[2] + " skinned), beyond 80 m " + kv.Value[3] + ", beyond 150 m " + kv.Value[4]);
return sb.ToString();
