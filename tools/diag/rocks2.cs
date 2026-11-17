using Ski.Core;
using UnityEngine;
// Ski straight at off-piste rocks and see what happens.
var sb = new System.Text.StringBuilder();
var s0 = new RunSession(SlopeId.OffPiste, CharacterId.Erik);
int n = 0, crashed = 0, through = 0;
foreach (var o in s0.Course.Obstacles)
{
    if (o.Kind != ObstacleKind.Rock || n >= 12) continue;
    n++;
    var s = new RunSession(SlopeId.OffPiste, CharacterId.Erik);
    var sim = s.Sim;
    sim.Pos = s.Course.PointOnSurface(o.X, o.Z - 6f);
    sim.Heading = 0f;
    sim.Vel = new Vector3(0f, 0f, 9f);
    bool hit = false; float minD = 99f;
    for (int i = 0; i < 120 * 2; i++)
    {
        s.Step(new SkiInput(), SkiSim.FixedDt); s.Events.Clear();
        float d = new Vector2(sim.Pos.x - o.X, sim.Pos.z - o.Z).magnitude;
        minD = Mathf.Min(minD, d);
        if (sim.Crashed) hit = true;
    }
    if (hit) crashed++;
    if (minD < o.Radius) through++;
    sb.Append("r=").Append(o.Radius.ToString("0.00")).Append(" h=").Append(o.Height.ToString("0.0")).Append(" minDist=").Append(minD.ToString("0.00")).Append(" crashed=").Append(hit).Append(" endZ-rockZ=").Append((sim.Pos.z - o.Z).ToString("0.0")).Append('\n');
}
return "rocks tried=" + n + " crashed=" + crashed + " centre inside rock=" + through + "\n" + sb;
