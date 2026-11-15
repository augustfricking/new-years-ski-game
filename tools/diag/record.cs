using Ski.Core;
using Ski.Game;
using UnityEngine;

// Puts the current run just before the finish with a record-breaking result, to check the
// "new record" banner and the fireworks.
var run = GameApp.Instance.Run;
var s = run.Session;
s.Phase = RunPhase.Running;
s.Time = Courses.ScoreBased(s.Slope) ? 30f : 20f;
s.Score = 20000;
s.Sim.Teleport(s.Course.CenterX(s.Course.FinishZ - 25f), s.Course.FinishZ - 25f, 0f);
s.Sim.Vel = new Vector3(0f, 0f, 9f);
run.Autopilot = new Bot(s);
return "ready at z=" + s.Sim.Pos.z;
