// During a physical crash: where the pelvis, head and feet are (relative to the snow under them), and how fast the parts move and turn.
var rc = UnityEngine.Object.FindFirstObjectByType<Ski.Game.RunController>();
if (rc == null) return "no run";
var b = rc.Rig.Body; var c = rc.Session.Course;
System.Func<UnityEngine.Transform, string> at = t => { var p = t.position; return (p.y - c.Height(p.x, p.z)).ToString("0.00"); };
string s = "limp " + rc.Rig.Limp + " pelvis " + at(b.Hips) + " head " + at(b.Head) + " feet " + at(b.FootL) + "/" + at(b.FootR);
foreach (var t in new[] { b.Hips, b.Torso, b.Head, b.ThighL, b.KneeL })
{
    var rb = t.GetComponent<UnityEngine.Rigidbody>();
    if (rb != null) s += "  " + t.name + " v" + rb.linearVelocity.magnitude.ToString("0.0") + " w" + rb.angularVelocity.magnitude.ToString("0.0");
}
return s;
