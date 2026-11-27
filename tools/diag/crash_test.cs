// During a run (play.ps1 -Keep -NoBot): stages a real crash of the kind in pose.txt and turns the camera to third
// person to watch the body take it. Nothing is forced: the run itself finds the crash.
//   Person      - right behind a child, faster than them, on their line
//   Trees       - across the slope into the forest at its edge
//   HardLanding - eight metres up, falling onto the snow
//   BadLanding  - just off the snow, upside down and turned sideways (too late to land it)
string[] a = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
string kind = a[0];
var rc = UnityEngine.Object.FindFirstObjectByType<Ski.Game.RunController>();
if (rc == null) return "no run";
var s = rc.Session; var sim = s.Sim; var c = s.Course;
rc.Rig.SetThirdPerson(true);
typeof(Ski.Game.PlayerRig).GetField("_third", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(rc.Rig, 1f);   // at once, even in slow motion
typeof(Ski.Game.PlayerRig).GetField("_tpInit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(rc.Rig, false);   // the camera jumps along with the skier
if (s.Phase == Ski.Core.RunPhase.Ready) s.Phase = Ski.Core.RunPhase.Running;
float z = sim.Pos.z + 40f;
if (kind == "Person" && s.Crowd != null)
{
    // First call: skiing straight down at speed. Second ("Person go"): a child moved right ahead, on the line.
    if (a.Length > 1 && a[1] == "go")
    {
        Ski.Core.Kid best = null;
        foreach (var k in s.Crowd.Kids) if (!k.Fallen && k.Active && k.Kind == Ski.Core.KidKind.SchoolKid && (best == null || UnityEngine.Mathf.Abs(k.Z - sim.Pos.z) < UnityEngine.Mathf.Abs(best.Z - sim.Pos.z))) best = k;
        if (best == null) return "no child";
        var d = sim.Vel; d.y = 0f; d.Normalize();
        best.X = sim.Pos.x + d.x * 1.3f; best.Z = sim.Pos.z + d.z * 1.3f;
        return "child placed ahead at speed " + (sim.HorizontalSpeed * 3.6f).ToString("0") + " km/h";
    }
    float cx = c.CenterX(sim.Pos.z + 60f);
    sim.Teleport(cx, sim.Pos.z + 60f, 0f);
    sim.Vel = new UnityEngine.Vector3(0f, 0f, 11f);
    return "skiing down";
}
if (kind == "Trees")
{
    float edge = c.CenterX(z) + c.HalfWidth(z) - 2f;
    sim.Teleport(edge - 5f, z, 0.9f);
    sim.Vel = new UnityEngine.Vector3(UnityEngine.Mathf.Sin(0.9f), 0f, UnityEngine.Mathf.Cos(0.9f)) * 13f;
    return "towards the trees at x " + edge.ToString("0");
}
if (kind == "HardLanding" || kind == "BadLanding")
{
    float x = c.CenterX(z);
    sim.Teleport(x, z, 0f);
    bool hard = kind == "HardLanding";
    sim.Pos.y = c.Height(x, z) + (hard ? 8f : 0.25f);
    sim.Grounded = false;
    sim.AirTime = 0.6f; sim.MaxAirHeight = hard ? 8f : 1.5f;
    sim.Vel = new UnityEngine.Vector3(0f, hard ? -6f : -2f, 12f);
    if (!hard) { sim.Flip = 2.6f; sim.FlipVel = 0f; sim.Heading = 1.2f; }
    return "in the air " + (hard ? "falling hard" : "mid-flip");
}
return "unknown kind " + kind;
