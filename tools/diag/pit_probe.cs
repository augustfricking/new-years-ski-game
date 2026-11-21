// Rides straight over a black-slope pit at a range of speeds and reports what happens: the hardest hit
// into the snow, whether it was a landing or a compression while on the snow, and whether it crashed.
var c = new Ski.Core.BlackCourse();
Ski.Core.Pit pit = null;
foreach (var f in c.Features) if (f is Ski.Core.Pit p && p.Zc > 300f) { pit = p; break; }
var sb = new System.Text.StringBuilder();
sb.Append("pit r=" + pit.Radius.ToString("0.0") + " depth=" + pit.Depth.ToString("0.00") + "\n");
foreach (float speed in new[] { 12f, 18f, 22f, 26f, 30f, 34f, 38f })
{
    var prm = Ski.Core.SkiParams.For(Ski.Core.CharacterId.Erik); if (System.IO.File.Exists("C:/Users/augus/AppData/Local/Temp/nocrash.flag")) prm.CrashImpact = 999f;
    var s = new Ski.Core.SkiSim(c, prm);
    s.Teleport(pit.X, pit.Zc - 30f, 0f);
    s.Vel = new UnityEngine.Vector3(0f, -speed * 0.45f, speed * 0.89f);
    float maxV = 0f; string kind = "-"; bool crashed = false; float air = 0f;
    for (int i = 0; i < 600; i++)
    {
        s.Step(new Ski.Core.SkiInput { Tuck = true }, Ski.Core.SkiSim.FixedDt);
        foreach (var e in s.Events)
        {
            if (e.Type == Ski.Core.SkiEventType.HeavyLanding && e.Value > maxV) { maxV = e.Value; kind = "heavy"; }
            if (e.Type == Ski.Core.SkiEventType.Crash) { crashed = true; kind = "crash " + e.Reason; }
        }
        if (!s.Grounded) air = UnityEngine.Mathf.Max(air, s.AirHeight);
        if (s.Crashed || s.Pos.z > pit.Zc + pit.Radius + 12f) break;
    }
    sb.Append((speed * 3.6f).ToString("0") + " km/h: " + kind + " maxHeavy=" + maxV.ToString("0.0") + " air=" + air.ToString("0.00") + "\n");
}
return sb.ToString();
