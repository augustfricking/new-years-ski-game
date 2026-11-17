using Ski.Core;
using UnityEngine;
// Do the off-piste rocks stop a skier? Counts rocks whose own centre does not register a hit.
var c = Courses.Create(SlopeId.OffPiste);
int rocks = 0, missed = 0, low = 0;
foreach (var o in c.Obstacles)
{
    if (o.Kind != ObstacleKind.Rock) continue;
    rocks++;
    if (!c.HitObstacle(o.X + o.Radius * 0.5f, o.Z, 0.35f, 0f, out var h)) missed++;
    if (o.Height < 1.2f) low++;
}
return "rocks=" + rocks + " missed=" + missed + " lowRocks=" + low;
