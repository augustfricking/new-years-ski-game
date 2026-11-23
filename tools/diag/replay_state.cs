// While a replay plays: the replayed run's clock, whether its skier is down, how many children it has hit,
// and how many of the children shown are lying in the snow.
var rc = UnityEngine.Object.FindFirstObjectByType<Ski.Game.RunController>();
if (rc == null) return "no run";
var f = typeof(Ski.Game.RunController).GetField("_replay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var rp = f.GetValue(rc) as Ski.Game.RunReplay;
if (rp == null || rp.Session == null) return "no exact replay";
var s = rp.Session;
int down = 0;
if (s.Crowd != null) foreach (var k in s.Crowd.Kids) if (k.Fallen) down++;
return "t " + s.Time.ToString("0.00") + " crashed " + s.Sim.Crashed + " speed " + (s.Sim.HorizontalSpeed * 3.6f).ToString("0") + " km/h hit " + s.PeopleHit + " kids down " + down;
