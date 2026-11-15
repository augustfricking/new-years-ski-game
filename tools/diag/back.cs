using Ski.Core;
using Ski.Game;
using UnityEngine;

// Stands the player in the village plaza, facing back up the mountain, for a screenshot.
// Screenshots/pose.txt holds "<metres past the finish> <look pitch>" (defaults 70 and 14).
var run = GameApp.Instance.Run;
var s = run.Session; var sim = s.Sim; var c = s.Course;
string[] arg = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
float past = arg.Length > 0 && float.TryParse(arg[0], out float a0) ? a0 : 70f;
float pitch = arg.Length > 1 && float.TryParse(arg[1], out float a1) ? a1 : 14f;
run.Autopilot = null;
float z = c.FinishZ + past;
sim.Teleport(c.VillageX, z, Mathf.PI);
sim.Vel = Vector3.zero;
for (int i = 0; i < 4; i++) { s.Step(default, SkiSim.FixedDt); s.Events.Clear(); }
run.Paused = true;
run.Rig.Look = new Vector2(0f, pitch);
return "z=" + sim.Pos.z + " fwd=" + sim.Forward;
