using System.Collections.Generic;
using UnityEngine;

namespace Ski.Core
{
    public enum RunPhase { Ready, Running, Finished }

    public enum RunEventType
    {
        Started, Finished, GatePassed, GateMissed, PersonHit, TrickScored, Penalty, Respawned, DeadEnd,
        Crash, Recovered, PolePush, Jump, Landed, HeavyLanding, Takeoff, GrindStart, Brush,
    }

    public struct RunEvent
    {
        public RunEventType Type;
        public float Value;
        public string Text;
        public TrickResult Trick;
        public CrashReason Reason;
        public Kid Kid;
    }

    /// <summary>A slalom gate: a pair of poles. Ski between them.</summary>
    public sealed class Gate
    {
        public float Z, InnerX, OuterX;
        public bool Red;
        public int State; // 0 = ahead, 1 = passed, 2 = missed
        public float CentreX => (InnerX + OuterX) * 0.5f;
        public int Side => OuterX > InnerX ? 1 : -1;
    }

    /// <summary>
    /// One attempt at a slope: owns the simulation, the clock, the rules of the level (kids, gates,
    /// trick points, dead ends) and the result. Deterministic; step it with a fixed dt.
    /// </summary>
    public sealed class RunSession
    {
        public readonly Course Course;
        public readonly SkiSim Sim;
        public readonly SlopeId Slope;
        public readonly CharacterId Character;

        public RunPhase Phase = RunPhase.Ready;
        public float Time;          // seconds since the start line
        public float Penalty;       // seconds added
        public int Score;           // trick points (blue)
        public float TopSpeed;
        public int GatesMissed, PeopleHit, Respawns, Crashes;
        public float LongestAir, LongestGrind;   // seconds / metres, this run
        public string BestTrickName; public int BestTrickPoints;

        public readonly KidCrowd Crowd;                  // green only
        public readonly List<Gate> Gates = new List<Gate>(); // red only
        public int NextGate;
        public readonly List<RunEvent> Events = new List<RunEvent>();

        public const float GatePenalty = 6f, PersonPenalty = 5f, RespawnPenalty = 5f;
        public const float StartLineZ = 9f;
        public const float GateWidth = 5.5f;

        readonly Dictionary<string, int> _trickCounts = new Dictionary<string, int>();
        float _stuckTimer;
        Vector3 _prevPos;
        Vector2 _lastJunction;
        bool _hasJunction;

        public bool ScoreBased => Courses.ScoreBased(Slope);
        public float FinalTime => Time + Penalty;
        /// <summary>The value that goes on the leaderboard: points on blue, seconds elsewhere.</summary>
        public float ResultValue => ScoreBased ? Score : FinalTime;

        /// <param name="withCrowd">False for rivals sharing the player's course: they do not need their own crowd of kids.</param>
        public RunSession(SlopeId slope, CharacterId character, Course course = null, bool withCrowd = true)
        {
            Slope = slope; Character = character;
            Course = course ?? Courses.Create(slope);
            Sim = new SkiSim(Course, SkiParams.For(character));
            _prevPos = Sim.Pos;
            if (slope == SlopeId.Green && withCrowd) Crowd = new KidCrowd(Course, Course.Seed * 7 + 1);
            if (slope == SlopeId.Red) BuildGates();
        }

        void BuildGates()
        {
            // Set like a real slalom: the rhythm changes all the way down. Open, rounded turns;
            // a steady medium rhythm; a quick flush of tight gates close to the fall line; and
            // the odd long delayed turn out to the side.
            var rng = new Rng(Course.Seed + 5);
            float z = 75f;
            int side = 1;
            bool red = true;
            int section = 0;
            bool first = true;
            while (z < Course.FinishZ - 50f)
            {
                int kind = section++ % 4;
                int count = kind == 0 ? 3 : kind == 1 ? 4 : kind == 2 ? 3 : 2;
                for (int i = 0; i < count && z < Course.FinishZ - 50f; i++)
                {
                    float offset, gap;
                    switch (kind)
                    {
                        // Room between them to carry speed: a gate should be worth taking, not cheaper to miss.
                        case 0: offset = rng.Range(4.0f, 5.0f); gap = rng.Range(34f, 38f); break;   // open
                        case 1: offset = rng.Range(3.0f, 3.6f); gap = rng.Range(27f, 30f); break;   // rhythm
                        case 2: offset = rng.Range(1.4f, 1.9f); gap = rng.Range(21f, 23f); break;   // flush
                        default: offset = rng.Range(5.4f, 6.2f); gap = rng.Range(42f, 46f); break;  // delay
                    }
                    // The distance to a gate depends on that gate: a wide one needs room to get to.
                    if (!first) z += gap;
                    first = false;
                    if (z >= Course.FinishZ - 50f) break;
                    float cx = Course.CenterX(z);
                    float inner = cx + side * offset;
                    Gates.Add(new Gate { Z = z, InnerX = inner, OuterX = inner + side * GateWidth, Red = red });
                    side = -side; red = !red;
                }
            }
        }

        void Emit(RunEventType type, float value = 0f, string text = null, TrickResult trick = null, CrashReason reason = default, Kid kid = null)
        {
            Events.Add(new RunEvent { Type = type, Value = value, Text = text, Trick = trick, Reason = reason, Kid = kid });
        }

        public void Step(in SkiInput input, float dt)
        {
            _prevPos = Sim.Pos;
            Sim.Step(input, dt);

            if (Phase == RunPhase.Ready && Sim.Pos.z >= StartLineZ) { Phase = RunPhase.Running; Emit(RunEventType.Started); }
            if (Phase == RunPhase.Running)
            {
                Time += dt;
                TopSpeed = Mathf.Max(TopSpeed, Sim.Speed);
            }

            if (Crowd != null)
            {
                Crowd.Step(dt, Sim.Pos.z, Sim.Pos.x);
                // Nobody can be skied through, before the finish or after it (only the penalty stops at the line).
                if (!Sim.Crashed && Sim.Shield <= 0f && Sim.AirHeight < 1.1f)
                {
                    var k = Crowd.Hit(Sim.Pos.x, Sim.Pos.z, Sim.P.BodyRadius);
                    if (k != null && !k.Fallen)
                    {
                        Crowd.Knock(k);
                        Sim.Crash(CrashReason.Person);
                        if (Phase != RunPhase.Finished)
                        {
                            Penalty += PersonPenalty; PeopleHit++;
                            Emit(RunEventType.PersonHit, PersonPenalty, "Watch out! +" + PersonPenalty.ToString("0") + " s", kid: k);
                        }
                        else Emit(RunEventType.PersonHit, 0f, "Oops!", kid: k);
                    }
                }
            }

            // Remember the last junction on a valid route that we skied through.
            var cps = Course.Checkpoints;
            for (int i = 0; i < cps.Count; i++)
            {
                float dx = Sim.Pos.x - cps[i].x, dz = Sim.Pos.z - cps[i].y;
                if (dx * dx + dz * dz < 13f * 13f) { _lastJunction = cps[i]; _hasJunction = true; break; }
            }

            if (Gates.Count > 0 && Phase == RunPhase.Running) CheckGates();
            if (Course is OffPisteCourse op && Phase == RunPhase.Running) CheckDeadEnd(op, dt);

            // Translate simulation events.
            for (int i = 0; i < Sim.Events.Count; i++)
            {
                var e = Sim.Events[i];
                switch (e.Type)
                {
                    case SkiEventType.Landed:
                    case SkiEventType.GrindEnd:
                        if (e.Type == SkiEventType.Landed) LongestAir = Mathf.Max(LongestAir, Sim.AirTime);
                        else LongestGrind = Mathf.Max(LongestGrind, e.Value);
                        if (e.Trick != null) ScoreTrick(e.Trick);
                        if (e.Type == SkiEventType.Landed) Emit(RunEventType.Landed, e.Value, trick: e.Trick);
                        break;
                    case SkiEventType.Crash: Crashes++; Emit(RunEventType.Crash, e.Value, reason: e.Reason); break;
                    case SkiEventType.Recovered: Emit(RunEventType.Recovered); break;
                    case SkiEventType.PolePush: Emit(RunEventType.PolePush, e.Value); break;
                    case SkiEventType.Jump: Emit(RunEventType.Jump); break;
                    case SkiEventType.Takeoff: Emit(RunEventType.Takeoff); break;
                    case SkiEventType.HeavyLanding: LongestAir = Mathf.Max(LongestAir, Sim.AirTime); Emit(RunEventType.HeavyLanding, e.Value); break;
                    case SkiEventType.GrindStart: Emit(RunEventType.GrindStart); break;
                    case SkiEventType.Brush: Emit(RunEventType.Brush); break;
                }
            }
            Sim.Events.Clear();

            if (Phase == RunPhase.Running && Sim.Pos.z >= Course.FinishZ)
            {
                Phase = RunPhase.Finished;
                Emit(RunEventType.Finished, ResultValue);
            }
        }

        void ScoreTrick(TrickResult t)
        {
            if (Phase != RunPhase.Running || t.Points <= 0) return;
            // Repeating the same trick is worth less each time.
            _trickCounts.TryGetValue(t.Name, out int n);
            _trickCounts[t.Name] = n + 1;
            if (n > 0 && t.Name != "Air") t.Points = Mathf.RoundToInt(t.Points * Mathf.Pow(0.7f, n) / 5f) * 5;
            if (!ScoreBased) return; // tricks only count in the park
            Score += t.Points;
            if (t.Points > BestTrickPoints) { BestTrickPoints = t.Points; BestTrickName = t.Name; }
            Emit(RunEventType.TrickScored, t.Points, t.Name, t);
        }

        void CheckGates()
        {
            while (NextGate < Gates.Count && Sim.Pos.z >= Gates[NextGate].Z)
            {
                var g = Gates[NextGate++];
                // Where did we cross the gate line?
                float dz = Sim.Pos.z - _prevPos.z;
                float t = dz > 1e-5f ? Mathf.Clamp01((g.Z - _prevPos.z) / dz) : 1f;
                float x = Mathf.Lerp(_prevPos.x, Sim.Pos.x, t);
                float along = (x - g.InnerX) * g.Side;
                if (along >= -0.45f && along <= GateWidth + 0.45f)
                {
                    g.State = 1;
                    Emit(RunEventType.GatePassed, NextGate);
                }
                else
                {
                    g.State = 2;
                    GatesMissed++; Penalty += GatePenalty;
                    Emit(RunEventType.GateMissed, GatePenalty, "Missed gate! +" + GatePenalty.ToString("0") + " s");
                }
            }
        }

        void CheckDeadEnd(OffPisteCourse op, float dt)
        {
            if (!op.InDeadEnd(Sim.Pos.x, Sim.Pos.z, out var kind)) { _stuckTimer = 0f; return; }
            // Stopped (or just got back up) at the end of a wrong turn: back to the last fork.
            if (Sim.Crashed) { _stuckTimer = 0f; return; }
            _stuckTimer = Sim.Speed < 1.2f ? _stuckTimer + dt : 0f;
            bool justRecovered = false;
            for (int i = 0; i < Sim.Events.Count; i++) if (Sim.Events[i].Type == SkiEventType.Recovered) justRecovered = true;
            if (justRecovered || _stuckTimer > 1.2f)
            {
                _stuckTimer = 0f;
                DoRespawn(kind == OffPisteCourse.DeadEndKind.Cliff ? "Cliff! Back to the last fork" : "Dead end! Back to the last fork", RunEventType.DeadEnd);
            }
        }

        /// <summary>Player asked to be put back on the course (R key).</summary>
        public void Respawn()
        {
            if (Phase == RunPhase.Finished || Sim.Crashed) return;
            DoRespawn("Back on course", RunEventType.Respawned);
        }

        void DoRespawn(string text, RunEventType type)
        {
            Vector2 p;
            if (Course.Checkpoints.Count > 0)
            {
                // A little above the junction, so both ways on are in view again.
                p = _hasJunction ? _lastJunction : new Vector2(Course.StartX, Course.StartZ + 30f);
                p.y -= 7f;
            }
            else
            {
                float z = Mathf.Max(Course.StartZ, Sim.Pos.z - 8f);
                p = new Vector2(Course.RacingLineX(z), z);
            }
            Sim.Teleport(p.x, p.y, 0f);
            Respawns++;
            if (Phase == RunPhase.Running) Penalty += RespawnPenalty;
            Emit(type, RespawnPenalty, text + " (+" + RespawnPenalty.ToString("0") + " s)");
        }
    }
}
