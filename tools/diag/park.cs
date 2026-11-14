using Ski.Core;
using System.Text;
using UnityEngine;

var sb = new StringBuilder();
var s = new RunSession(SlopeId.Blue, CharacterId.Linn);
var bot = new Bot(s);
float takeoffSpeed = 0f, takeoffZ = 0f;
for (int i = 0; i < 120 * 200 && s.Phase != RunPhase.Finished; i++)
{
    s.Step(bot.Decide(), SkiSim.FixedDt);
    foreach (var e in s.Events)
    {
        if (e.Type == RunEventType.Takeoff || e.Type == RunEventType.Jump) { takeoffSpeed = s.Sim.Speed; takeoffZ = s.Sim.Pos.z; }
        if (e.Type == RunEventType.Landed) sb.AppendLine($"land z={s.Sim.Pos.z:0} from z={takeoffZ:0} v0={takeoffSpeed:0.0} air={e.Trick.AirTime:0.00}s h={e.Trick.Height:0.0} vn={e.Value:0.0} {e.Trick.Name} {e.Trick.Points} clean={e.Trick.Clean} sketchy={e.Trick.Sketchy}");
        if (e.Type == RunEventType.HeavyLanding) sb.AppendLine($"  heavy vn={e.Value:0.0} z={s.Sim.Pos.z:0}");
        if (e.Type == RunEventType.Crash) sb.AppendLine($"  CRASH {e.Reason} z={s.Sim.Pos.z:0} x={s.Sim.Pos.x:0.0}");
        if (e.Type == RunEventType.TrickScored) sb.AppendLine($"  +{e.Trick.Points} {e.Trick.Name}");
    }
    s.Events.Clear();
}
sb.AppendLine($"score={s.Score} time={s.Time:0.0}");

// Pit at speed
var c = new BlackCourse();
Pit pit = null;
foreach (var f in c.Features) if (f is Pit p && p.Zc > 300f) { pit = p; break; }
sb.AppendLine($"pit r={pit.Radius:0.0} depth={pit.Depth:0.00} z={pit.Zc:0}");
foreach (float speed in new[] { 12f, 20f, 27f, 33f })
{
    var sim = new SkiSim(c, SkiParams.For(CharacterId.Erik));
    sim.Teleport(pit.X, pit.Zc - 30f, 0f);
    sim.Vel = new Vector3(0f, -speed * 0.45f, speed * 0.89f);
    float maxG = 0f; string ev = "";
    for (int i = 0; i < 400; i++)
    {
        sim.Step(default, SkiSim.FixedDt);
        maxG = Mathf.Max(maxG, sim.GLoad);
        foreach (var e in sim.Events) ev += $" {e.Type}({e.Value:0.0})";
        sim.Events.Clear();
        if (sim.Pos.z > pit.Zc + pit.Radius + 12f) break;
    }
    sb.AppendLine($"  v={speed}: maxG={maxG:0} events:{ev}");
}
return sb.ToString();
