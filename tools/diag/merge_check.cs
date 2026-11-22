// For the comparison figures (merge_compare.cs): where each one's hands are, and the bounds of its skinned
// meshes after skinning, both relative to the figure, so the merged and the parted figure can be compared.
var root = UnityEngine.GameObject.Find("MergeCompare");
if (root == null) return "no MergeCompare";
var sb = new System.Text.StringBuilder();
foreach (UnityEngine.Transform t in root.transform)
{
    var p = t.GetComponent<Ski.Game.Person>();
    sb.Append(t.name + " x" + t.localPosition.x.ToString("0.00") + ": handL " + t.InverseTransformPoint(p.HandL.position).ToString("F2") + " handR " + t.InverseTransformPoint(p.HandR.position).ToString("F2"));
    var min = UnityEngine.Vector3.one * 99f; var max = -min;
    foreach (var r in t.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>())
    {
        var m = new UnityEngine.Mesh();
        r.BakeMesh(m, true);
        foreach (var v in m.vertices) { var w = t.InverseTransformPoint(r.transform.TransformPoint(v)); min = UnityEngine.Vector3.Min(min, w); max = UnityEngine.Vector3.Max(max, w); }
        UnityEngine.Object.Destroy(m);
    }
    foreach (var mf in t.GetComponentsInChildren<UnityEngine.MeshFilter>())
        foreach (var v in mf.sharedMesh.vertices) { var w = t.InverseTransformPoint(mf.transform.TransformPoint(v)); min = UnityEngine.Vector3.Min(min, w); max = UnityEngine.Vector3.Max(max, w); }
    sb.AppendLine("  all " + min.ToString("F2") + " .. " + max.ToString("F2"));
}
return sb.ToString();
