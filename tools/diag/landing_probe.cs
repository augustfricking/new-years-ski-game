// Steps a scratch session through a staged landing (upside down, turned sideways, just off the snow) and reports
// what the run makes of it: events, flip, heading against the travel, and whether it crashed.
var s = new Ski.Core.RunSession(Ski.Core.SlopeId.Green, Ski.Core.CharacterId.Linn);
s.Phase = Ski.Core.RunPhase.Running;
var sim = s.Sim; var c = s.Course;
float z = 200f, x = c.CenterX(z);
sim.Teleport(x, z, 0f);
sim.Pos.y = c.Height(x, z) + 0.25f; sim.Grounded = false; sim.AirTime = 0.6f; sim.MaxAirHeight = 1.5f;
sim.Vel = new UnityEngine.Vector3(0f, -2f, 12f); sim.Flip = 2.6f; sim.FlipVel = 0f; sim.Heading = 1.2f;
var log = new System.Text.StringBuilder();
for (int i = 0; i < 120; i++)
{
    s.Step(default, Ski.Core.SkiSim.FixedDt);
    foreach (var e in s.Events) log.Append(" [" + i + " " + e.Type + " " + e.Reason + "]");
    s.Events.Clear();
    if (i % 10 == 0) log.Append(" {" + i + " air " + sim.AirHeight.ToString("0.00") + " flip " + sim.Flip.ToString("0.00") + " grounded " + sim.Grounded + "}");
}
return log.ToString();
