using UnityEngine;

namespace Ski.Core
{
    /// <summary>
    /// A simple autopilot that plays through <see cref="RunSession"/> with the same inputs a player
    /// has. Used by tests (every level must be finishable) and by the balance check.
    /// </summary>
    public sealed class Bot
    {
        public float SpeedLimit = 100f;   // m/s; brakes above this
        public bool UseTuck = true;
        public bool DoTricks = true;
        public float Skill = 1f;          // 0..1, lower = sloppier pole timing
        public float Lane;                // metres to the side of the usual line (rivals skiing beside each other)
        /// <summary>Slalom: straight down the racing line, ignoring the gates (to check that skipping them never pays).</summary>
        public bool IgnoreGates;

        readonly RunSession _s;
        int _airPlan, _jumps;

        public Bot(RunSession session, int route = 0)
        {
            _s = session;
            if (session.Course is OffPisteCourse op) op.PreferredRoute = route;
            switch (session.Slope)
            {
                case SlopeId.Green: SpeedLimit = 12.5f; break;
                case SlopeId.Blue: SpeedLimit = 17f; UseTuck = false; break;
                case SlopeId.Red: SpeedLimit = 12.5f; UseTuck = false; break;
                case SlopeId.Black: SpeedLimit = 33f; break;
                case SlopeId.OffPiste: SpeedLimit = route == 1 ? 9.5f : route == 2 ? 11.5f : 11f; UseTuck = false; break;
            }
        }

        public SkiInput Decide()
        {
            var sim = _s.Sim; var c = _s.Course;
            var inp = new SkiInput();
            if (sim.Crashed) return inp;
            float speed = sim.HorizontalSpeed;

            if (!sim.Contact) return DecideAir(inp);
            _airPlan = 0;

            // --- where do we want to be a little further down? ---------------------------
            float look = Mathf.Clamp(speed * 0.9f, 7f, 26f);
            if (_s.Slope == SlopeId.OffPiste) look = Mathf.Clamp(speed * 0.75f, 5f, 11f); // glades twist: look closer
            if (_s.Slope == SlopeId.Red && !IgnoreGates) look = Mathf.Clamp(speed * 0.6f, 5f, 12f);        // gates come quickly
            float tz = sim.Pos.z + look;
            float tx = TargetX(tz, look);
            float desired = Mathf.Atan2(tx - sim.Pos.x, look);
            float velYaw = speed > 1.5f ? Mathf.Atan2(sim.Vel.x, sim.Vel.z) : sim.Heading;
            float err = M.WrapPi(desired - Mathf.Lerp(velYaw, sim.Heading, 0.5f));

            // --- steer, keeping the lean inside the carve band when the error is small ----
            float dead = 0.035f;
            if (Mathf.Abs(err) > dead)
            {
                float dir = Mathf.Sign(err);
                bool sameSide = Mathf.Sign(sim.Lean) == dir && sim.Lean != 0f;
                float maxLean = Mathf.Abs(err) > 0.3f ? 1f : Mathf.Abs(err) > 0.12f ? 0.66f : 0.42f;
                if (!sameSide || Mathf.Abs(sim.Lean) < maxLean) inp.Steer = dir;
                // Too much edge on: a dab the other way takes it off (letting go only eases it).
                else if (Mathf.Abs(sim.Lean) > maxLean + 0.16f) inp.Steer = -dir;
            }

            // --- speed ------------------------------------------------------------------
            bool straight = Mathf.Abs(err) < 0.12f && Mathf.Abs(sim.Lean) < 0.3f;
            if (speed > SpeedLimit) inp.Brake = true;
            else if (UseTuck && straight && speed > 6f) inp.Tuck = true;

            // --- poles at low speed ------------------------------------------------------
            if (sim.PoleAvailable && speed < sim.P.PoleMaxSpeed - 0.5f && !inp.Brake && _s.Phase != RunPhase.Finished)
            {
                float target = sim.P.PoleSweetSpot + (1f - Skill) * 0.2f;
                if (sim.PolePhase >= target - 0.03f && sim.PolePhase <= target + 0.08f) inp.Pole = true;
            }
            return inp;
        }

        SkiInput DecideAir(SkiInput inp)
        {
            var sim = _s.Sim;
            if (!DoTricks || !sim.RealAir) return inp;
            if (_airPlan == 0) _airPlan = 1 + (_jumps++ % 3);
            switch (_airPlan)
            {
                case 1: // 360 with a safety grab
                    if (Mathf.Abs(sim.AirYaw) < 2f * Mathf.PI - 1.3f) inp.Steer = 1f;
                    if (sim.AirTime < 0.8f) inp.Trick = 1;
                    break;
                case 2: // mute grab, held
                    if (sim.AirTime < 0.75f) { inp.Trick = 2; inp.Variant = sim.AirTime > 0.3f ? 1 : 0; }
                    break;
                default: // backflip when there is enough air for it, else a spread eagle
                    // S held (pressed after take-off) turns a backflip; let go near the end and it settles.
                    if (sim.Vel.y > -3.5f && sim.AirTime < 0.25f && sim.AirTime > 0.04f || Mathf.Abs(sim.FlipVel) > 0.1f && Mathf.Abs(sim.Flip) < 4.6f) inp.Brake = true;
                    else if (Mathf.Abs(sim.Flip) < 0.01f && sim.AirTime < 0.7f) inp.Trick = 4;
                    break;
            }
            return inp;
        }

        float TargetX(float z, float look)
        {
            var c = _s.Course; var sim = _s.Sim;
            float x = c.RacingLineX(z) + (_s.Slope == SlopeId.OffPiste ? Lane * 0.2f : Lane);
            if (z > c.FinishZ) return c.VillageX + Lane;

            switch (_s.Slope)
            {
                case SlopeId.Red:
                {
                    // Aim through the middle of the next gate, then the one after.
                    if (_s.NextGate < _s.Gates.Count && !IgnoreGates)
                    {
                        var g = _s.Gates[_s.NextGate];
                        float aim = g.InnerX + g.Side * 1.5f;
                        float dz = g.Z - sim.Pos.z;
                        if (dz > look && _s.NextGate > 0)
                        {
                            var p = _s.Gates[_s.NextGate - 1];
                            return Mathf.Lerp(p.InnerX + p.Side * 1.5f, aim, M.Smooth(Mathf.InverseLerp(p.Z, g.Z, z)));
                        }
                        // Steer for the gate itself: return the x that points at it.
                        return sim.Pos.x + (aim - sim.Pos.x) * Mathf.Clamp(look / Mathf.Max(dz, 3f), 0f, 1.6f);
                    }
                    return x;
                }
                case SlopeId.Green:
                    return AvoidKids(z, x);
                case SlopeId.Black:
                    return AvoidFeatures(z, x);
                case SlopeId.OffPiste:
                    return AvoidObstacles(z, x, look);
                default:
                    return x;
            }
        }

        float AvoidKids(float z, float centre)
        {
            var c = _s.Course; var sim = _s.Sim;
            float hw = c.HalfWidth(z) - 3f;
            float best = centre, bestCost = float.MaxValue;
            float speed = Mathf.Max(sim.HorizontalSpeed, 4f);
            for (int i = -5; i <= 5; i++)
            {
                float x = c.CenterX(z) + i / 5f * hw;
                float cost = Mathf.Abs(x - sim.Pos.x) * 0.06f + Mathf.Abs(i) * 0.05f;
                if (_s.Crowd != null) foreach (var k in _s.Crowd.Kids)
                {
                    if (!k.Active) continue;
                    float dz = k.Z - sim.Pos.z;
                    if (dz < -14f || dz > 55f) continue;
                    // Where will this kid be when we get there?
                    float t = Mathf.Clamp(dz / Mathf.Max(speed - k.Speed * Mathf.Cos(k.Heading), 2f), 0f, 6f);
                    float kx = k.X + Mathf.Sin(k.Heading) * k.Speed * t;
                    float d = Mathf.Abs(kx - x);
                    float danger = Mathf.Max(0f, 5.5f - d);
                    cost += danger * danger * (dz < 0f ? 0.25f : 1f) * Mathf.Lerp(1.4f, 0.4f, Mathf.Clamp01(dz / 55f));
                }
                if (cost < bestCost) { bestCost = cost; best = x; }
            }
            return best;
        }

        /// <summary>Off-piste: stay on the route but step round rocks, bushes and lone trees in the glade.</summary>
        float AvoidObstacles(float z, float centre, float look)
        {
            var c = (OffPisteCourse)_s.Course; var sim = _s.Sim;
            float best = centre, bestCost = float.MaxValue;
            for (int i = -4; i <= 4; i++)
            {
                float x = centre + i * 1.3f;
                float cost = Mathf.Abs(i) * 0.5f + Mathf.Abs(x - sim.Pos.x) * 0.04f;
                // Check the straight line from here to that point.
                for (int k = 1; k <= 4; k++)
                {
                    float t = k / 4f;
                    float px = Mathf.Lerp(sim.Pos.x, x, t), pz = Mathf.Lerp(sim.Pos.z, z, t);
                    float edge = c.Clear(px, pz);
                    if (edge < 1.2f) cost += (1.2f - edge) * 6f;
                    float room = c.Clearance(px, pz, 2.2f);
                    if (room < 1.3f) cost += (1.3f - room) * 14f;
                }
                if (cost < bestCost) { bestCost = cost; best = x; }
            }
            return best;
        }

        float AvoidFeatures(float z, float centre)
        {
            var c = (PisteCourse)_s.Course; var sim = _s.Sim;
            float hw = c.HalfWidth(z) - 3.5f;
            float best = centre, bestCost = float.MaxValue;
            for (int i = -4; i <= 4; i++)
            {
                float x = c.CenterX(z) + i / 4f * hw;
                float cost = Mathf.Abs(x - sim.Pos.x) * 0.05f + Mathf.Abs(i) * 0.04f;
                foreach (var f in c.Features)
                {
                    if (f.Z1 < sim.Pos.z - 2f || f.Z0 > z + 30f) continue;
                    float d = Mathf.Abs(f.X - x);
                    float danger = Mathf.Max(0f, f.HalfW + 2.5f - d);
                    cost += danger * danger;
                }
                if (cost < bestCost) { bestCost = cost; best = x; }
            }
            return best;
        }

        /// <summary>Plays a whole run. Returns false if it did not finish within the time limit.</summary>
        public static bool Play(RunSession s, Bot bot, float maxSeconds = 400f)
        {
            int steps = Mathf.CeilToInt(maxSeconds / SkiSim.FixedDt);
            for (int i = 0; i < steps; i++)
            {
                s.Step(bot.Decide(), SkiSim.FixedDt);
                s.Events.Clear();
                if (s.Phase == RunPhase.Finished) return true;
            }
            return false;
        }
    }
}
