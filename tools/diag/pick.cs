using UnityEngine;
// Which renderers could be drawing at a screen point (pose.txt: "x y" in 1280x720 pixels, from the top)?
string[] a = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
var cam = Camera.main;
var pt = new Vector2(float.Parse(a[0]) / 1280f * cam.pixelWidth, (1f - float.Parse(a[1]) / 720f) * cam.pixelHeight);
var ray = cam.ScreenPointToRay(pt);
var hits = new System.Collections.Generic.List<(float, string)>();
foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
{
    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
    if (!r.bounds.IntersectRay(ray, out float d)) continue;
    var mf = r.GetComponent<MeshFilter>();
    string mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name + " v" + mf.sharedMesh.vertexCount : r.GetType().Name;
    string path = r.name; var t = r.transform.parent; for (int i = 0; i < 3 && t != null; i++, t = t.parent) path = t.name + "/" + path;
    hits.Add((d, d.ToString("0.0") + " m  " + path + "  [" + mesh + "] mat=" + (r.sharedMaterial != null ? r.sharedMaterial.name : "-") + " size=" + r.bounds.size.ToString("0")));
}
hits.Sort((x, y) => x.Item1.CompareTo(y.Item1));
var sb = new System.Text.StringBuilder();
foreach (var h in hits) sb.Append(h.Item2).Append('\n');
return sb.ToString();
