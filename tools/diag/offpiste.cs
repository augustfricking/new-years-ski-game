using Ski.Core;
using System.Text;
using UnityEngine;

var sb = new StringBuilder();
foreach (int route in new[] { 1, 2 })
{
    var s = new RunSession(SlopeId.OffPiste, CharacterId.Erik);
    var c = (OffPisteCourse)s.Course;
    var bot = new Bot(s, route);
    int logged = 0;
    for (int i = 0; i < 120 * 200 && s.Phase != RunPhase.Finished && logged < 14; i++)
    {
        s.Step(bot.Decide(), SkiSim.FixedDt);
        foreach (var e in s.Events)
        {
            if (e.Type == RunEventType.Crash || e.Type == RunEventType.DeadEnd || e.Type == RunEventType.Respawned)
            {
                var p = s.Sim.Pos;
                sb.AppendLine($"r{route} t={s.Time:0.0} {e.Type} {e.Reason} at ({p.x:0.0},{p.z:0.0}) clear={c.Clear(p.x, p.z):0.0} routeX={c.RouteX(route, p.z):0.0} deadEnd={c.InDeadEnd(p.x, p.z, out _)} blocked={c.Blocked(p.x, p.z)}");
                logged++;
            }
        }
        s.Events.Clear();
    }
    sb.AppendLine($"route {route}: z={s.Sim.Pos.z:0} phase={s.Phase}");
    // The way-points of the route around the trouble spot.
    var pts = c.Solutions[route];
    foreach (var p in pts) if (p.y > (route == 1 ? 600 : 900) && p.y < (route == 1 ? 860 : 1150)) sb.Append($" ({p.x:0},{p.y:0})");
    sb.AppendLine();
}
return sb.ToString();
