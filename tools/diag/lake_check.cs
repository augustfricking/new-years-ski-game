// Where is the frozen lake: its renderer, bounds, triangle count, and the ground height under its middle.
var go = UnityEngine.GameObject.Find("Frozen lake");
if (go == null) return "no lake object";
var mf = go.GetComponent<UnityEngine.MeshFilter>();
var mr = go.GetComponent<UnityEngine.MeshRenderer>();
var b = mr.bounds;
var floor = UnityEngine.GameObject.Find("Valley floor");
string under = "-";
if (floor != null)
{
    var m = floor.GetComponent<UnityEngine.MeshFilter>().sharedMesh;
    if (m.isReadable)
    {
        float best = 1e9f, hy = 0f;
        foreach (var v in m.vertices) { float d = (v.x - b.center.x) * (v.x - b.center.x) + (v.z - b.center.z) * (v.z - b.center.z); if (d < best) { best = d; hy = v.y; } }
        under = hy.ToString("0.0");
    }
    else under = "floor mesh not readable; floor bounds y " + floor.GetComponent<UnityEngine.MeshRenderer>().bounds.min.y.ToString("0.0") + ".." + floor.GetComponent<UnityEngine.MeshRenderer>().bounds.max.y.ToString("0.0");
}
var n = mf.sharedMesh.normals;
return "lake centre " + b.center + " size " + b.size + " tris " + (mf.sharedMesh.triangles.Length / 3) + " enabled " + mr.enabled + " visible " + mr.isVisible
       + " normal[0] " + (n.Length > 0 ? n[0].ToString() : "-") + " ground under " + under + " finishZ " + ((Ski.Core.PisteCourse)Ski.Game.GameApp.Instance.Run.Session.Course).FinishZ;
