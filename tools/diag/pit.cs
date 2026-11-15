using Ski.Core;
using Ski.Game;
using UnityEngine;

// Puts the player a little uphill of the first pit (or roller) of the run, looking at it.
var run = GameApp.Instance.Run;
var s = run.Session; var sim = s.Sim; var c = (PisteCourse)s.Course;
string want = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim();
Feature hit = null; int skip = 1;
foreach (var f in c.Features)
{
    bool ok = want == "roller" ? f is Roller : f is Pit;
    if (ok && skip-- <= 0) { hit = f; break; }
}
if (hit == null) return "none";
run.Autopilot = null;
float zc = (hit.Z0 + hit.Z1) * 0.5f;
sim.Teleport(hit.X, zc - 26f, 0f);
sim.Vel = Vector3.zero;
for (int i = 0; i < 4; i++) { s.Step(default, SkiSim.FixedDt); s.Events.Clear(); }
run.Paused = true;
run.Rig.Look = new Vector2(0f, -6f);
return hit.GetType().Name + " at z=" + zc + " x=" + hit.X;
