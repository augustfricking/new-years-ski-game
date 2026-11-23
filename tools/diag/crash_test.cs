// During a run (play.ps1 -Keep -NoBot): sets up a crash of the kind in pose.txt ("Person", "Trees", "HardLanding",
// "BadLanding"; a second word "go" makes it happen) and turns the camera to third person to watch the body take it.
// First call: places the skier and gets them moving. Second call (with "go"): the crash, for the kinds forced directly.
string[] a = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
string kind = a[0]; bool go = a.Length > 1 && a[1] == "go";
var rc = UnityEngine.Object.FindFirstObjectByType<Ski.Game.RunController>();
if (rc == null) return "no run";
var s = rc.Session; var sim = s.Sim; var c = s.Course;
rc.Rig.SetThirdPerson(true);
if (s.Phase == Ski.Core.RunPhase.Ready) s.Phase = Ski.Core.RunPhase.Running;
if (go)
{
    if (kind == "Person") sim.Crash(Ski.Core.CrashReason.Person);
    else if (kind == "HardLanding") sim.Crash(Ski.Core.CrashReason.HardLanding);
    else if (kind == "BadLanding") sim.Crash(Ski.Core.CrashReason.BadLanding);
    return "crash " + kind + " crashed " + sim.Crashed + " limp " + rc.Rig.Limp;
}
float z = sim.Pos.z + 40f;
if (kind == "Person" && s.Crowd != null)
{
    // The nearest child well down the slope, straight ahead of a skier six metres above them.
    Ski.Core.Kid best = null;
    foreach (var k in s.Crowd.Kids) if (!k.Fallen && k.Z > sim.Pos.z + 20f && (best == null || k.Z < best.Z)) best = k;
    if (best == null) return "no child";
    sim.Teleport(best.X, best.Z - 2.5f, 0f);
    sim.Vel = new UnityEngine.Vector3(0f, 0f, 11f);
    return "towards child at " + best.X.ToString("0") + "," + best.Z.ToString("0");
}
if (kind == "Trees")
{
    float edge = c.CenterX(z) + c.HalfWidth(z) - 2f;
    sim.Teleport(edge - 5f, z, 0.9f);
    sim.Vel = new UnityEngine.Vector3(UnityEngine.Mathf.Sin(0.9f), 0f, UnityEngine.Mathf.Cos(0.9f)) * 13f;
    return "towards the trees at x " + edge.ToString("0");
}
if (kind == "HardLanding") { sim.Teleport(c.CenterX(z), z, 0f); sim.Vel = new UnityEngine.Vector3(0f, -9f, 11f); return "falling fast"; }
if (kind == "BadLanding") { sim.Teleport(c.CenterX(z), z, 0f); sim.Vel = new UnityEngine.Vector3(2f, -2f, 13f); return "landing askew"; }
return "unknown kind " + kind;
