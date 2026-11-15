using Ski.Core;
using System.Text;
using UnityEngine;

var sb = new StringBuilder();
var s = new RunSession(SlopeId.Red, CharacterId.Erik);
var bot = new Bot(s);
int last = 0;
for (int i = 0; i < 120 * 300 && s.Phase != RunPhase.Finished; i++)
{
    float px = s.Sim.Pos.x;
    s.Step(bot.Decide(), SkiSim.FixedDt);
    if (s.NextGate != last)
    {
        var g = s.Gates[last];
        float gap = last > 0 ? g.Z - s.Gates[last - 1].Z : 0f;
        float off = (g.InnerX - s.Course.CenterX(g.Z)) * g.Side;
        float along = (s.Sim.Pos.x - g.InnerX) * g.Side;
        if (g.State == 2) sb.AppendLine($"MISS gate {last} gap={gap:0.0} offset={off:0.0} side={g.Side} passedAt={along:0.0} (needs 0..5.5) speed={s.Sim.Speed:0.0} slope={Mathf.Atan(-(s.Course.Height(0, g.Z + 1) - s.Course.Height(0, g.Z - 1)) / 2f) * Mathf.Rad2Deg:0}deg");
        last = s.NextGate;
    }
    s.Events.Clear();
}
sb.AppendLine($"gates={s.Gates.Count} missed={s.GatesMissed} time={s.Time:0.0}");
return sb.ToString();
