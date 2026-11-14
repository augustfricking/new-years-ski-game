using System.Collections.Generic;
using UnityEngine;

namespace Ski.Core
{
    /// <summary>Player intent for one simulation step.</summary>
    public struct SkiInput
    {
        public float Steer;   // -1 = left (A), +1 = right (D)
        public bool Tuck;     // W held
        public bool Brake;    // S held
        public bool Jump;     // Space pressed this step
        public bool Pole;     // Shift pressed this step
        public int Trick;     // 0 = none, 1..5 = trick key held
    }

    public enum SkiEventType { PolePush, Jump, Takeoff, Landed, HeavyLanding, Crash, Recovered, GrindStart, GrindEnd, Brush }
    public enum PoleQuality { Miss = 0, Ok = 1, Perfect = 2 }
    public enum CrashReason { HardLanding, BadLanding, Trees, Obstacle, Person, Cliff }

    public struct SkiEvent
    {
        public SkiEventType Type;
        public float Value;          // push quality, impact speed, grind metres...
        public CrashReason Reason;
        public TrickResult Trick;    // for Landed / GrindEnd
    }

    /// <summary>
    /// Deterministic first-person ski physics on a <see cref="Course"/>. No Unity scene objects:
    /// call <see cref="Step"/> with a fixed dt and read the public state. Heading 0 looks down +Z,
    /// positive heading turns right (+X).
    /// </summary>
    public sealed class SkiSim
    {
        public const float FixedDt = 1f / 120f;

        public readonly Course Course;
        public readonly SkiParams P;

        public Vector3 Pos, Vel;
        public float Heading;
        public float Lean;          // -1..1, how hard the skis are edged
        public float Tuck;          // 0..1
        public float Plough;        // 0..1 snowplough
        public bool Grounded = true;
        public bool Switch;         // riding backwards after a 180

        // Air state.
        public float AirTime, AirHeight, MaxAirHeight;
        public float SpinVel, AirYaw;
        public float Flip, FlipVel;
        public float Cork, CorkVel;
        public int ActiveGrab, ActiveGrabVariant;
        public readonly float[] GrabTime = new float[15]; // [trick * 3 + variant]

        // Feedback values for HUD, audio and visuals.
        public float CarveQuality;  // 0..1, 1 = in the middle of the carve band
        public float SkidAmount;    // 0..1 sideways slip
        public float OverLean;      // 0..1 beyond the carve band
        public float GLoad;         // smoothed normal acceleration, m/s^2
        public float PolePhase;
        public bool PoleAvailable;
        public PoleQuality LastPole;
        public float SincePole = 10f;
        public SurfaceKind Surface;
        public float GrindDistance;

        public float CrashTimer;
        public CrashReason LastCrash;
        public int CrashCount;

        public readonly List<SkiEvent> Events = new List<SkiEvent>();

        float _poleCooldown;
        bool _grinding;
        float _grindSideways;

        public float Speed => Vel.magnitude;
        public float HorizontalSpeed => Mathf.Sqrt(Vel.x * Vel.x + Vel.z * Vel.z);
        public bool Crashed => CrashTimer > 0f;
        public bool Contact => Grounded || AirHeight < 0.12f;
        public bool RealAir => !Grounded && AirTime > 0.18f && AirHeight > 0.25f;
        public Vector3 Forward => new Vector3(Mathf.Sin(Heading), 0f, Mathf.Cos(Heading));

        public SkiSim(Course course, SkiParams p)
        {
            Course = course; P = p;
            Teleport(course.StartX, course.StartZ, 0f);
        }

        public void Teleport(float x, float z, float heading)
        {
            Pos = Course.PointOnSurface(x, z);
            Vel = Vector3.zero;
            Heading = heading;
            Lean = Tuck = Plough = 0f;
            Grounded = true;
            CrashTimer = 0f;
            ResetAir();
        }

        void ResetAir()
        {
            AirTime = AirHeight = MaxAirHeight = 0f;
            SpinVel = AirYaw = Flip = FlipVel = Cork = CorkVel = 0f;
            ActiveGrab = 0; ActiveGrabVariant = 0;
            for (int i = 0; i < GrabTime.Length; i++) GrabTime[i] = 0f;
        }

        void Emit(SkiEventType t, float value = 0f, CrashReason reason = default, TrickResult trick = null)
        {
            Events.Add(new SkiEvent { Type = t, Value = value, Reason = reason, Trick = trick });
        }

        public void Crash(CrashReason reason)
        {
            if (Crashed) return;
            CrashTimer = P.CrashTime;
            LastCrash = reason;
            CrashCount++;
            if (_grinding) { _grinding = false; GrindDistance = 0f; }
            Emit(SkiEventType.Crash, Speed, reason);
            ResetAir();
        }

        public void Step(in SkiInput inp, float dt)
        {
            if (Crashed) { StepCrashed(dt); return; }

            var c = Course;
            float g = P.Gravity;
            AirHeight = Pos.y - c.Height(Pos.x, Pos.z);
            bool contact = Contact;
            Surface = contact ? c.SurfaceAt(Pos.x, Pos.z) : SurfaceKind.Snow;
            bool onBox = Surface == SurfaceKind.Box;
            float steer = Mathf.Clamp(inp.Steer, -1f, 1f);
            float speed = Vel.magnitude;
            float sH = HorizontalSpeed;

            // --- body position -----------------------------------------------------------
            Tuck = Mathf.MoveTowards(Tuck, inp.Tuck ? 1f : 0f, (inp.Tuck ? P.TuckIn : P.TuckOut) * dt);
            bool brake = inp.Brake && contact;
            Plough = Mathf.MoveTowards(Plough, brake ? 1f : 0f, (brake ? P.PloughIn : P.PloughOut) * dt);
            if (contact)
            {
                bool returning = steer == 0f || (Lean != 0f && Mathf.Sign(steer) != Mathf.Sign(Lean));
                Lean = Mathf.MoveTowards(Lean, steer, (returning ? P.LeanOut : P.LeanIn) * dt);
            }
            else Lean = Mathf.MoveTowards(Lean, 0f, P.LeanOut * dt);

            Vector3 acc = new Vector3(0f, -g, 0f);
            Vector3 n = contact ? c.Normal(Pos.x, Pos.z) : Vector3.up;
            Vector3 f = Forward;
            bool jumped = false;
            CarveQuality = 0f; OverLean = 0f;

            if (contact)
            {
                // --- steering ------------------------------------------------------------
                float maxRate = Mathf.Min(P.TurnRateLow, P.LatAccelMax / Mathf.Max(sH, 1f));
                maxRate *= Mathf.Lerp(1f, P.TuckTurnFactor, Tuck) * Mathf.Lerp(1f, 0.8f, Plough);
                Heading = M.WrapPi(Heading + Lean * maxRate * dt);
                Vector3 fwd = Forward;
                f = (fwd - Vector3.Dot(fwd, n) * n).normalized;
                Vector3 r = Vector3.Cross(n, f);

                // --- edge grip: sideways slip is scrubbed off ----------------------------
                float grip = onBox ? 0.4f : Mathf.Lerp(P.Grip, P.GripPlough, Plough);
                float vl = Vector3.Dot(Vel, r);
                float before = Vel.magnitude;
                Vel -= r * (vl * (1f - Mathf.Exp(-grip * dt)));
                float after = Vel.magnitude;
                if (P.SkidLossFactor < 1f && after > 0.01f)
                    Vel *= (after + (before - after) * (1f - P.SkidLossFactor)) / after;
                SkidAmount = Mathf.Clamp01(Mathf.Abs(vl) / Mathf.Max(speed, 2f) * 3f);

                if (Grounded) acc -= n * Vector3.Dot(acc, n); // the snow carries the normal part

                Vector3 vhat = speed > 0.05f ? Vel / Mathf.Max(Vel.magnitude, 1e-4f) : f;
                float mu = onBox ? 0.02f : P.Mu * c.MuScale;
                if (speed > 0.05f) acc -= vhat * (mu * g * n.y);

                if (!onBox)
                {
                    float absLean = Mathf.Abs(Lean);
                    // Too much edge: the skis skid and brake.
                    OverLean = Mathf.Clamp01((absLean - P.CarveMax) / (1f - P.CarveMax));
                    acc -= vhat * (OverLean * P.SkidDecel * Mathf.Clamp01(speed / 7f));
                    // Just enough edge: the carve gives speed back.
                    float band = Mathf.Clamp01((absLean - P.CarveMin) / (P.CarveMax - P.CarveMin));
                    float q = absLean < P.CarveMax ? Mathf.Sin(Mathf.PI * band) : 0f;
                    float moving = Mathf.Clamp01((speed - 3f) / 5f) * (1f - Plough);
                    CarveQuality = q * (moving > 0f ? 1f : 0f);
                    acc += f * (q * P.CarveBoost * moving);
                    // Snowplough.
                    acc -= vhat * (Plough * Mathf.Min(P.PloughDecel, 1.2f + speed * 0.9f));
                }

                // --- pole pushes ---------------------------------------------------------
                _poleCooldown -= dt;
                SincePole += dt;
                PoleAvailable = sH < P.PoleMaxSpeed && Plough < 0.3f && !onBox;
                if (PoleAvailable)
                {
                    float period = Mathf.Lerp(P.PolePeriodSlow, P.PolePeriodFast, sH / P.PoleMaxSpeed);
                    PolePhase += dt / period;
                    if (PolePhase >= 1f) PolePhase -= 1f;
                    if (inp.Pole && _poleCooldown <= 0f)
                    {
                        float err = Mathf.Abs(PolePhase - P.PoleSweetSpot);
                        LastPole = err <= P.PoleWindow ? PoleQuality.Perfect : err <= P.PoleOkWindow ? PoleQuality.Ok : PoleQuality.Miss;
                        float dv = LastPole == PoleQuality.Perfect ? P.PolePerfect : LastPole == PoleQuality.Ok ? P.PoleOk : P.PoleMiss;
                        dv *= 1f - 0.5f * sH / P.PoleMaxSpeed;
                        Vel += f * dv;
                        PolePhase = 0f;
                        _poleCooldown = 0.16f;
                        SincePole = 0f;
                        Emit(SkiEventType.PolePush, (float)LastPole);
                    }
                }

                // --- standing still ------------------------------------------------------
                if (Grounded && Vel.magnitude < 0.3f && !inp.Jump)
                {
                    float slopePull = g * Mathf.Sqrt(Mathf.Max(0f, 1f - n.y * n.y));
                    if (Plough > 0.5f || slopePull < 0.5f) { Vel = Vector3.zero; acc = Vector3.zero; }
                }

                // --- jump ----------------------------------------------------------------
                if (inp.Jump)
                {
                    Vel += n * P.JumpSpeed;
                    Grounded = false;
                    jumped = true;
                    AirTime = 0f; MaxAirHeight = 0f;
                    Emit(SkiEventType.Jump);
                }

                // --- grinding ------------------------------------------------------------
                if (onBox)
                {
                    if (!_grinding) { _grinding = true; GrindDistance = 0f; _grindSideways = 0f; Emit(SkiEventType.GrindStart); }
                    GrindDistance += sH * dt;
                    float velYaw = Mathf.Atan2(Vel.x, Vel.z);
                    float side = Mathf.Abs(Mathf.Sin(M.WrapPi(Heading - velYaw)));
                    _grindSideways = Mathf.Max(_grindSideways, side);
                }
            }
            if (_grinding && !onBox && (Grounded || AirHeight > 0.2f || jumped)) EndGrind();

            if (!contact)
            {
                // --- air control: spins, flips, corks, grabs -----------------------------
                if (steer != 0f) SpinVel = Mathf.MoveTowards(SpinVel, steer * P.AirSpinRate, P.AirSpinAccel * dt);
                else
                {
                    // Released: settle on the nearest half turn so the skis point along the flight path.
                    float rel = M.WrapPi(Heading - Mathf.Atan2(Vel.x, Vel.z));
                    float settle = Mathf.Round(rel / Mathf.PI) * Mathf.PI - rel;
                    SpinVel = Mathf.MoveTowards(SpinVel, Mathf.Clamp(settle * 9f, -P.AirSpinRate, P.AirSpinRate), P.AirSpinAccel * dt);
                }
                Heading = M.WrapPi(Heading + SpinVel * dt);
                AirYaw += SpinVel * dt;

                StepRotation(ref Flip, ref FlipVel, inp.Trick == 3, inp.Tuck ? 1f : -1f, P.FlipRate, dt);
                StepRotation(ref Cork, ref CorkVel, inp.Trick == 5, steer < 0f ? -1f : 1f, P.CorkRate, dt);

                ActiveGrab = 0;
                if (inp.Trick == 1 || inp.Trick == 2 || inp.Trick == 4)
                {
                    ActiveGrab = inp.Trick;
                    ActiveGrabVariant = inp.Tuck ? 1 : inp.Brake ? 2 : 0;
                    GrabTime[inp.Trick * 3 + ActiveGrabVariant] += dt;
                }
                acc += c.AirAssist(Pos, Vel);
            }

            // --- air resistance ----------------------------------------------------------
            float k = Mathf.Lerp(P.DragUpright, P.DragTuck, Tuck);
            acc -= Vel * (k * Vel.magnitude);

            // --- integrate ---------------------------------------------------------------
            Vector3 vPrev = Vel;
            if (Grounded)
            {
                Vel += acc * dt;
                float nx = Pos.x + Vel.x * dt, nz = Pos.z + Vel.z * dt;
                float ht = c.Height(nx, nz);
                float vyTerrain = (ht - Pos.y) / dt;
                float vyFree = vPrev.y - g * dt;
                if (vyTerrain < vyFree - 0.02f)
                {
                    // The ground falls away faster than gravity pulls: we are in the air.
                    Grounded = false;
                    Vel.y = vyFree;
                    Pos += Vel * dt;
                    AirTime = 0f; MaxAirHeight = 0f;
                    Emit(SkiEventType.Takeoff);
                }
                else
                {
                    Vector3 np = new Vector3(nx, ht, nz);
                    Vector3 d = np - Pos;
                    float spd = Vel.magnitude;
                    if (d.sqrMagnitude > 1e-12f && spd > 1e-4f)
                    {
                        Vector3 dir = d.normalized;
                        float kink = 0f;
                        if (vPrev.sqrMagnitude > 1f)
                        {
                            kink = spd * Vector3.Dot(dir - vPrev.normalized, n); // push from the snow this step
                            GLoad = Mathf.Lerp(GLoad, kink / dt + g * n.y, 1f - Mathf.Exp(-dt / 0.06f));
                        }
                        Vel = dir * spd;
                        Pos = np;
                        if (kink > 1.3f) Impact(kink, false, n);
                    }
                    else Pos = np;
                }
            }
            else
            {
                Vel += acc * dt;
                Pos += Vel * dt;
                AirTime += dt;
                float ht = c.Height(Pos.x, Pos.z);
                float above = Pos.y - ht;
                if (above > MaxAirHeight) MaxAirHeight = above;
                GLoad = Mathf.Lerp(GLoad, 0f, 1f - Mathf.Exp(-dt / 0.06f));
                if (above <= 0f && !jumped) Land(ht);
            }

            if (!Crashed) Collide();
        }

        static void StepRotation(ref float angle, ref float vel, bool held, float dir, float rate, float dt)
        {
            const float Full = 2f * Mathf.PI;
            if (held)
            {
                if (Mathf.Abs(vel) < 0.1f) vel = dir * 0.2f;
                vel = Mathf.MoveTowards(vel, Mathf.Sign(vel) * rate, 40f * dt);
                angle += vel * dt;
            }
            else if (vel != 0f)
            {
                // Released: finish the current rotation, then stop.
                float turns = angle / Full;
                float next = vel > 0f ? Mathf.Ceil(turns - 1e-4f) * Full : Mathf.Floor(turns + 1e-4f) * Full;
                float step = vel * dt;
                if (Mathf.Abs(next - angle) <= Mathf.Abs(step) || Mathf.Abs(next - angle) < 1e-3f) { angle = next; vel = 0f; }
                else angle += step;
            }
        }

        void EndGrind()
        {
            _grinding = false;
            if (GrindDistance > 1.5f)
            {
                var t = TrickScorer.ScoreGrind(GrindDistance, _grindSideways);
                Emit(SkiEventType.GrindEnd, GrindDistance, default, t);
            }
            GrindDistance = 0f;
        }

        void Land(float groundHeight)
        {
            Pos.y = groundHeight;
            Vector3 n = Course.Normal(Pos.x, Pos.z);
            float vn = -Vector3.Dot(Vel, n);
            if (vn > 0f) Vel += n * vn;
            Grounded = true;
            Impact(Mathf.Max(vn, 0f), true, n);
        }

        /// <summary>Handles hitting the snow with a normal speed of vn, either landing or a sharp compression.</summary>
        void Impact(float vn, bool landing, Vector3 n)
        {
            bool real = landing && AirTime > 0.3f && MaxAirHeight > 0.35f;
            float velYaw = Mathf.Atan2(Vel.x, Vel.z);
            float err = M.WrapPi(Heading - velYaw);
            bool sw = Mathf.Abs(err) > Mathf.PI * 0.5f;
            float yawErr = sw ? Mathf.PI - Mathf.Abs(err) : Mathf.Abs(err);
            const float Full = 2f * Mathf.PI;
            float flipErr = Mathf.Abs(Flip - Mathf.Round(Flip / Full) * Full);
            float corkErr = Mathf.Abs(Cork - Mathf.Round(Cork / Full) * Full);

            if (vn > P.CrashImpact) { Crash(CrashReason.HardLanding); return; }
            if (real && (yawErr > P.LandYawTolerance || flipErr > P.LandFlipTolerance || corkErr > P.LandFlipTolerance))
            {
                Crash(CrashReason.BadLanding);
                return;
            }
            if (landing && sw && HorizontalSpeed > 2f) { Heading = M.WrapPi(Heading + Mathf.PI); Switch = !Switch; }
            if (vn > P.HeavyImpact)
            {
                Vel *= 1f - Mathf.Min(0.45f, (vn - P.HeavyImpact) * 0.075f);
                Emit(SkiEventType.HeavyLanding, vn);
            }
            if (real)
            {
                var result = TrickScorer.Score(this, yawErr, vn);
                Emit(SkiEventType.Landed, vn, default, result);
            }
            if (landing) ResetAir();
        }

        void Collide()
        {
            var c = Course;
            if (c.Walled)
            {
                float cx = c.CenterX(Pos.z), hw = c.HalfWidth(Pos.z) - 0.4f;
                float off = Pos.x - cx;
                if (Mathf.Abs(off) > hw)
                {
                    float side = Mathf.Sign(off);
                    float into = Vel.x * side;
                    Pos.x = cx + side * hw;
                    if (Grounded) Pos.y = c.Height(Pos.x, Pos.z);
                    if (Speed > 5f && into > 1.2f || into > 3f) { Crash(CrashReason.Trees); return; }
                    if (into > 0f) Vel.x -= into * side;
                    Vel *= 0.92f;
                }
            }
            if (AirHeight < 3f && c.Blocked(Pos.x, Pos.z)) { Crash(CrashReason.Trees); return; }
            if (c.HitObstacle(Pos.x, Pos.z, P.BodyRadius, AirHeight, out var o))
            {
                if (!o.Solid)
                {
                    Vel *= Mathf.Exp(-2.5f * FixedDt);
                    if (Speed > 4f && Events.Count == 0) Emit(SkiEventType.Brush);
                    return;
                }
                if (Speed > 3.2f)
                {
                    Crash(o.Kind == ObstacleKind.Tree || o.Kind == ObstacleKind.DeadTree ? CrashReason.Trees : CrashReason.Obstacle);
                    return;
                }
                PushOut(o);
            }
        }

        void PushOut(in Obstacle o)
        {
            float r = P.BodyRadius;
            Vector3 dir;
            if (o.IsBox)
            {
                float px = Mathf.Clamp(Pos.x, o.X - o.Hx, o.X + o.Hx), pz = Mathf.Clamp(Pos.z, o.Z - o.Hz, o.Z + o.Hz);
                dir = new Vector3(Pos.x - px, 0f, Pos.z - pz);
                if (dir.sqrMagnitude < 1e-6f) dir = new Vector3(0f, 0f, -1f);
                dir.Normalize();
                Pos.x = px + dir.x * r; Pos.z = pz + dir.z * r;
            }
            else
            {
                dir = new Vector3(Pos.x - o.X, 0f, Pos.z - o.Z);
                if (dir.sqrMagnitude < 1e-6f) dir = new Vector3(0f, 0f, -1f);
                dir.Normalize();
                Pos.x = o.X + dir.x * (o.Radius + r); Pos.z = o.Z + dir.z * (o.Radius + r);
            }
            float into = -Vector3.Dot(Vel, dir);
            if (into > 0f) Vel += dir * into;
            if (Grounded) Pos.y = Course.Height(Pos.x, Pos.z);
        }

        void StepCrashed(float dt)
        {
            var c = Course;
            CrashTimer -= dt;
            Lean = Mathf.MoveTowards(Lean, 0f, 4f * dt);
            Tuck = Mathf.MoveTowards(Tuck, 0f, 4f * dt);
            Plough = 0f;
            float ht = c.Height(Pos.x, Pos.z);
            if (Pos.y > ht + 0.05f)
            {
                Vel.y -= P.Gravity * dt;
                Pos += Vel * dt;
                if (Pos.y < c.Height(Pos.x, Pos.z)) { Pos.y = c.Height(Pos.x, Pos.z); Vel.y = 0f; }
            }
            else
            {
                Vel.y = 0f;
                Vel *= Mathf.Exp(-2.4f * dt);
                Pos.x += Vel.x * dt; Pos.z += Vel.z * dt;
                if (c.Walled)
                {
                    float cx = c.CenterX(Pos.z), hw = c.HalfWidth(Pos.z) - 0.4f;
                    Pos.x = Mathf.Clamp(Pos.x, cx - hw, cx + hw);
                }
                if (c.HitObstacle(Pos.x, Pos.z, P.BodyRadius, 0f, out var o) && o.Solid) { PushOut(o); Vel = Vector3.zero; }
                Pos.y = c.Height(Pos.x, Pos.z);
            }
            AirHeight = Pos.y - c.Height(Pos.x, Pos.z);
            if (CrashTimer <= 0f)
            {
                CrashTimer = 0f;
                var rp = c.RecoverPoint(Pos);
                Pos = c.PointOnSurface(rp.x, rp.y);
                Vel = Vector3.zero;
                Heading = c.FallHeading(Pos.x, Pos.z);
                Grounded = true;
                Switch = false;
                Emit(SkiEventType.Recovered);
            }
        }
    }
}
