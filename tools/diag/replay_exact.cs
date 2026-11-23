// During a run (play.ps1 -Keep -NoBot): records a bot run on this slope with its inputs, as a board row
// would carry it, and watches it through the replay. Returns when (seconds after this call) the recorded
// skier ran into a child, if it did, so a screenshot can be taken there.
var rc = UnityEngine.Object.FindFirstObjectByType<Ski.Game.RunController>();
if (rc == null) return "no run";
var slope = rc.Session.Slope;
var s = new Ski.Core.RunSession(slope, Ski.Core.CharacterId.Maja);
var bot = new Ski.Core.Bot(s);
var inputs = new Ski.Core.RunInputs();
var ghost = new Ski.Core.GhostTrack();
float hitAt = -1f, startAt = -1f;
for (int i = 0; i < 120 * 300 && s.Phase != Ski.Core.RunPhase.Finished; i++)
{
    var input = Ski.Core.RunInputs.Quantize(bot.Decide());
    inputs.Record(input);
    s.Step(input, Ski.Core.SkiSim.FixedDt);
    if (startAt < 0f && s.Phase == Ski.Core.RunPhase.Running) startAt = i * Ski.Core.SkiSim.FixedDt;
    if (s.Phase == Ski.Core.RunPhase.Running) ghost.Record(s.Time, s.Sim.Pos.x, s.Sim.Pos.z, s.Sim.Contact ? 0f : s.Sim.AirHeight);
    foreach (var e in s.Events) if (e.Type == Ski.Core.RunEventType.PersonHit && hitAt < 0f) hitAt = s.Time;
    s.Events.Clear();
}
var entry = new Ski.Game.OnlineBoards.Entry { Name = "Replay test", Ghost = ghost.Encode(), Inputs = inputs.Encode(), Character = (int)Ski.Core.CharacterId.Maja, Rank = 0 };
bool ok = rc.Watch(entry);
return "watching " + ok + " exact " + (rc.Watching) + " steps " + inputs.Steps + " time " + s.Time.ToString("0.0") + " kids hit " + s.PeopleHit + (hitAt >= 0f ? " first hit at " + (1.5f + hitAt).ToString("0.0") + " s from now" : "");
