using Ski.Core;
using Ski.Game;
using UnityEngine;

// Fast-forwards the current run to just before the finish line with a good time, so the
// results card and fireworks can be checked quickly.
var app = GameApp.Instance;
var run = app.Run;
var s = run.Session;
var bot = new Bot(s);
int guard = 0;
while (s.Sim.Pos.z < s.Course.FinishZ - 60f && guard++ < 120 * 400)
{
    s.Step(bot.Decide(), SkiSim.FixedDt);
    s.Events.Clear();
}
run.Autopilot = bot;
return "z=" + s.Sim.Pos.z + " time=" + s.Time + " score=" + s.Score;
