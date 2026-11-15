using Ski.Core;
using Ski.Game;
using UnityEngine;

// Freezes the current run in a chosen situation so a screenshot can show it.
// Edit `what` before running: "brake", "carve", "air", "kids", "park", "offpiste".
string what = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim();
var run = GameApp.Instance.Run;
var s = run.Session; var sim = s.Sim; var c = s.Course;
run.Autopilot = null;
run.Paused = false;
float z = 200f;
SkiInput inp = default;
Vector2 look = new Vector2(0, -8);
int steps = 60;
switch (what)
{
    case "brake": inp.Brake = true; look = new Vector2(0, -48); steps = 50; break;
    case "carve": inp.Steer = 1f; look = new Vector2(0, -30); steps = 45; break;
    case "air": z = 232f; inp.Trick = 1; look = new Vector2(0, -35); steps = 110; break;
    case "kids":
        Kid k = null;
        foreach (var kid in s.Crowd.Kids) if (kid.Kind == KidKind.Instructor) { k = kid; break; }
        z = k.Z - 14f; look = new Vector2(Mathf.Clamp((k.X - c.CenterX(z)) * 2f, -25, 25), -6); steps = 2; break;
    case "park": z = 310f; look = new Vector2(0, -6); steps = 2; break;
    case "pipe": z = 470f; look = new Vector2(0, -8); steps = 2; break;
    case "offpiste": z = 150f; look = new Vector2(0, -6); steps = 2; break;
}
float x = what == "air" ? -9f : c.RacingLineX(z);
sim.Teleport(x, z, 0f);
sim.Vel = new Vector3(0f, -4f, what == "air" ? 16f : 13f);
for (int i = 0; i < steps; i++) { s.Step(inp, SkiSim.FixedDt); s.Events.Clear(); }
run.Paused = true;
run.Rig.Look = look;
return what + " z=" + sim.Pos.z + " air=" + sim.RealAir + " plough=" + sim.Plough + " lean=" + sim.Lean;
