using System.Collections.Generic;
using UnityEngine;

namespace Ski.Core
{
    public enum KidKind { Bomber, Traverser, HarnessKid, HarnessParent, Instructor, SchoolKid }

    /// <summary>One person on the children's slope.</summary>
    public sealed class Kid
    {
        public KidKind Kind;
        public int Id, Group;
        public float X, Z, Heading, Speed;
        public float Radius = 0.42f;
        public float Size = 1f;        // 1 = adult height, kids about 0.6
        public int Colour;
        public float FallTimer;        // > 0 while sitting in the snow after being hit
        public bool Screaming;
        public bool Active = true;
        public Kid Leader;             // harness parent follows the kid; school kids follow the one in front
        public bool Fallen => FallTimer > 0f;

        // Behaviour state.
        internal float Phase, Rate, Amp, Lane, Down, Pause;
        internal int Dir = 1;
        internal bool Turns;
    }

    /// <summary>
    /// The crowd on the green slope: bombers, slow traversers, kids on a harness with a parent
    /// behind, and ski-school snakes. Pure and deterministic.
    /// </summary>
    public sealed class KidCrowd
    {
        public readonly List<Kid> Kids = new List<Kid>();
        readonly Course _c;
        readonly Rng _rng;
        readonly List<Vector2>[] _trails;   // path history of each school leader
        readonly List<Kid> _instructors = new List<Kid>();
        float _time;
        int _nextBomber;
        readonly List<(float time, float offset, float speed)> _bomberPlan = new List<(float, float, float)>();

        public const float LeashLength = 3.2f;
        const float SchoolSpacing = 2.3f;

        public KidCrowd(Course course, int seed)
        {
            _c = course;
            _rng = new Rng(seed);
            float top = 70f, bottom = course.FinishZ - 40f;

            // Slow traversers, spread down the hill.
            const int traversers = 17;
            for (int i = 0; i < traversers; i++)
            {
                float z = Mathf.Lerp(top, bottom, (i + _rng.Range(0.1f, 0.9f)) / traversers);
                var k = Add(KidKind.Traverser, z, _rng.Range(-0.8f, 0.8f));
                k.Dir = _rng.Chance(0.5f) ? 1 : -1;
                k.Rate = _rng.Range(2.2f, 3.4f);       // sideways speed
                k.Down = _rng.Range(0.5f, 1.2f);       // downhill speed
                k.Turns = _rng.Chance(0.5f);           // half of them stop at the edge instead of turning
                k.Size = _rng.Range(0.52f, 0.68f);
            }

            // Kids on a harness with a parent behind.
            const int pairs = 8;
            for (int i = 0; i < pairs; i++)
            {
                float z = Mathf.Lerp(top + 30f, bottom - 20f, (i + _rng.Range(0.2f, 0.8f)) / pairs);
                var kid = Add(KidKind.HarnessKid, z, _rng.Range(-0.4f, 0.4f));
                kid.Size = _rng.Range(0.45f, 0.55f);
                kid.Amp = _rng.Range(4.5f, 7f); kid.Rate = _rng.Range(0.42f, 0.6f); kid.Down = _rng.Range(2.8f, 3.8f);
                kid.Phase = _rng.Range(0f, 6.28f); kid.Lane = kid.X - course.CenterX(z);
                var parent = Add(KidKind.HarnessParent, z - LeashLength, 0f);
                parent.X = kid.X; parent.Leader = kid; parent.Group = kid.Group = 100 + i; parent.Size = 1f; parent.Radius = 0.5f;
            }

            // Ski schools: an instructor and a row of kids snaking across the slope.
            const int schools = 5;
            _trails = new List<Vector2>[schools];
            for (int i = 0; i < schools; i++)
            {
                float z = Mathf.Lerp(top + 50f, bottom - 50f, (i + _rng.Range(0.25f, 0.75f)) / schools);
                var lead = Add(KidKind.Instructor, z, 0f);
                lead.Size = 1f; lead.Radius = 0.5f; lead.Group = 200 + i;
                lead.Amp = _rng.Range(0.62f, 0.8f);    // fraction of the half-width
                lead.Rate = _rng.Range(0.3f, 0.4f); lead.Down = _rng.Range(2.6f, 3.3f); lead.Phase = _rng.Range(0f, 6.28f);
                _instructors.Add(lead);
                _trails[i] = new List<Vector2>();
                Kid prev = lead;
                int count = _rng.Range(6, 9);
                for (int j = 0; j < count; j++)
                {
                    var k = Add(KidKind.SchoolKid, z - (j + 1) * SchoolSpacing, 0f);
                    k.Leader = prev; k.Group = lead.Group; k.Size = _rng.Range(0.5f, 0.66f); k.Phase = (j + 1) * SchoolSpacing;
                    prev = k;
                }
                // Lay down a trail behind the instructor so the row starts in formation.
                PlaceInstructor(lead, 0f);
                for (float back = 40f; back >= 0f; back -= 0.5f)
                {
                    float t = -back / lead.Down;
                    _trails[i].Add(InstructorPos(lead, z + lead.Down * t, t));
                }
            }

            // Bombers appear behind the skier during the run.
            for (int i = 0; i < 13; i++)
                _bomberPlan.Add((3f + i * _rng.Range(3.2f, 4.6f), _rng.Range(-0.8f, 0.8f), _rng.Range(12.5f, 16.5f)));

            foreach (var k in Kids) k.Colour = _rng.Range(0, 8);
            Step(0f, 0f, 0f);
        }

        Kid Add(KidKind kind, float z, float lane)
        {
            float hw = _c.HalfWidth(z) - 2.5f;
            var k = new Kid { Kind = kind, Id = Kids.Count, Z = z, X = _c.CenterX(z) + lane * hw, Lane = lane * hw };
            Kids.Add(k);
            return k;
        }

        Vector2 InstructorPos(Kid lead, float z, float t)
        {
            float hw = _c.HalfWidth(z) - 3f;
            return new Vector2(_c.CenterX(z) + Mathf.Sin(lead.Phase + t * lead.Rate) * hw * lead.Amp, z);
        }

        void PlaceInstructor(Kid lead, float t)
        {
            var p = InstructorPos(lead, lead.Z, t);
            lead.X = p.x;
        }

        public void Step(float dt, float playerZ, float playerX)
        {
            _time += dt;
            float end = _c.FinishZ - 12f;

            // Spawn bombers behind the player.
            while (_nextBomber < _bomberPlan.Count && _time >= _bomberPlan[_nextBomber].time)
            {
                var plan = _bomberPlan[_nextBomber++];
                float z = Mathf.Max(20f, playerZ - 55f);
                if (playerZ > end - 80f) continue;
                var b = Add(KidKind.Bomber, z, plan.offset);
                b.Speed = plan.speed; b.Size = 0.62f; b.Screaming = true; b.Colour = _nextBomber % 8;
                b.Phase = plan.offset * 10f; b.Rate = 0.7f + 0.3f * Mathf.Abs(plan.offset);
            }

            for (int i = 0; i < Kids.Count; i++)
            {
                var k = Kids[i];
                if (!k.Active) continue;
                if (k.FallTimer > 0f) { k.FallTimer -= dt; k.Speed = 0f; continue; }
                float cx = _c.CenterX(k.Z), hw = _c.HalfWidth(k.Z) - 2.5f;
                float px = k.X, pz = k.Z;
                switch (k.Kind)
                {
                    case KidKind.Bomber:
                        // Straight down with a nervous wobble.
                        k.Z += k.Speed * dt;
                        k.Lane = Mathf.Clamp(k.Lane + Mathf.Sin(_time * k.Rate + k.Phase) * 1.3f * dt, -hw, hw);
                        k.X = _c.CenterX(k.Z) + k.Lane;
                        break;
                    case KidKind.Traverser:
                        if (k.Pause > 0f) { k.Pause -= dt; break; }
                        k.Lane += k.Dir * k.Rate * dt;
                        k.Z += k.Down * dt;
                        if (Mathf.Abs(k.Lane) > hw)
                        {
                            k.Lane = Mathf.Sign(k.Lane) * hw;
                            k.Dir = -k.Dir;
                            k.Pause = k.Turns ? 0.4f : _rng.Range(1.6f, 3f);
                        }
                        k.X = _c.CenterX(k.Z) + k.Lane;
                        break;
                    case KidKind.HarnessKid:
                        k.Z += k.Down * dt;
                        k.X = _c.CenterX(k.Z) + Mathf.Clamp(k.Lane * 0.4f + Mathf.Sin(k.Phase + _time * k.Rate) * k.Amp, -hw, hw);
                        break;
                    case KidKind.HarnessParent:
                    {
                        // Stay one leash length uphill of the kid.
                        var kid = k.Leader;
                        Vector2 d = new Vector2(k.X - kid.X, k.Z - kid.Z);
                        if (d.sqrMagnitude < 0.01f) d = new Vector2(0f, -1f);
                        d = Vector2.Lerp(d.normalized, new Vector2(0f, -1f), 2f * dt).normalized * LeashLength;
                        k.X = kid.X + d.x; k.Z = kid.Z + d.y;
                        break;
                    }
                    case KidKind.Instructor:
                    {
                        k.Z += k.Down * dt;
                        PlaceInstructor(k, _time);
                        var trail = _trails[k.Group - 200];
                        var last = trail[trail.Count - 1];
                        if ((new Vector2(k.X, k.Z) - last).sqrMagnitude > 0.2f * 0.2f) trail.Add(new Vector2(k.X, k.Z));
                        if (trail.Count > 400) trail.RemoveRange(0, 100);
                        break;
                    }
                    case KidKind.SchoolKid:
                    {
                        // Follow the instructor's track at a fixed distance.
                        var trail = _trails[k.Group - 200];
                        float want = k.Phase, acc = 0f;
                        Vector2 p = trail[trail.Count - 1];
                        for (int t = trail.Count - 1; t > 0; t--)
                        {
                            float seg = (trail[t] - trail[t - 1]).magnitude;
                            if (acc + seg >= want) { p = Vector2.Lerp(trail[t], trail[t - 1], (want - acc) / Mathf.Max(seg, 1e-4f)); break; }
                            acc += seg; p = trail[t - 1];
                        }
                        k.X = p.x; k.Z = p.y;
                        break;
                    }
                }
                float mx = k.X - px, mz = k.Z - pz;
                if (dt > 0f && mx * mx + mz * mz > 1e-8f)
                {
                    k.Heading = Mathf.Atan2(mx, mz);
                    if (k.Kind != KidKind.Bomber) k.Speed = Mathf.Sqrt(mx * mx + mz * mz) / dt;
                }
                if (k.Z > end) k.Active = false;
            }
        }

        /// <summary>Returns the person hit by a body of the given radius at (x, z), or null.</summary>
        public Kid Hit(float x, float z, float radius)
        {
            for (int i = 0; i < Kids.Count; i++)
            {
                var k = Kids[i];
                if (!k.Active) continue;
                float dx = x - k.X, dz = z - k.Z, rr = radius + k.Radius;
                if (dx * dx + dz * dz < rr * rr) return k;
                // The harness line between parent and kid also trips you up.
                if (k.Kind == KidKind.HarnessParent && !k.Fallen && k.Leader.Active &&
                    M.DistToSegment(x, z, k.X, k.Z, k.Leader.X, k.Leader.Z, out _) < radius * 0.6f) return k.Leader;
            }
            return null;
        }

        public void Knock(Kid k)
        {
            k.FallTimer = 3.5f;
            if (k.Kind == KidKind.HarnessKid || k.Kind == KidKind.HarnessParent)
                foreach (var o in Kids) if (o.Group == k.Group) o.FallTimer = 3.5f;
        }
    }
}
