using System.Collections.Generic;
using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// A crash on the slope as a body would take it. The skier goes limp (a <see cref="Ragdoll"/>) from
    /// the very pose they were in, mid-spin or mid-turn, onto the snow (a patch of the slope made solid
    /// for the moment) and into the trees, rocks and boxes close by, thrown the way the crash happened:
    /// tripped over a child (the legs stop, the body pitches over), into a tree (stopped hard and
    /// thrown back while the legs sweep on), a landing from too high (slammed down) or a landing
    /// askew (the spin carries on and rolls them). After the first moment the body is drawn to where
    /// the run says the skier slid, so the camera and the getting-up stay with it; when the run
    /// stands them up, the body goes back to its animation. Only for the eye: the run itself (the
    /// time, the line, the replays) is the simulation's.
    /// </summary>
    public sealed class CrashDoll
    {
        const int Grid = 33;            // the snow patch: Grid x Grid points a metre apart
        const float ObstacleReach = 14f;
        // The body keeps pace with the run's slide (a damper on the pelvis from the first moment), and is
        // drawn to its place only gently, later: a hard late pull would yank the hips and flip the legs over.
        const float PullAfter = 0.3f, PullFull = 1.0f, Spring = 900f, Damper = 220f;

        readonly Person _p;
        readonly Ragdoll _doll;
        readonly Transform _home;
        readonly Vector3 _homePos;
        readonly Quaternion _homeRot;
        readonly GameObject _world;
        readonly Rigidbody _anchor;
        readonly ConfigurableJoint _pull;
        readonly Mesh _patch;
        float _age;

        public Person Body => _p;
        /// <summary>Where the pelvis is now (for a camera to follow).</summary>
        public Vector3 Centre => _doll.Pelvis != null ? _doll.Pelvis.position : _p.transform.position;

        static PhysicsMaterial _snow;
        static PhysicsMaterial Snow => _snow != null ? _snow : _snow = new PhysicsMaterial("Snow")
        {
            // A jacket slides on snow: the body skids along it rather than catching and cartwheeling.
            dynamicFriction = 0.32f, staticFriction = 0.4f, bounciness = 0.04f,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
        };

        /// <summary>
        /// The body <paramref name="p"/> goes down. <paramref name="velocity"/> and <paramref name="spin"/>
        /// are how it was moving just before (m/s, rad/s, in the world); <paramref name="slide"/> where the
        /// run has the skier now. Everything made for it lives under <paramref name="worldParent"/>.
        /// </summary>
        public static CrashDoll Begin(Person p, Transform worldParent, Course course, Vector3 velocity, Vector3 spin, CrashReason reason, Vector3 slide)
            => new CrashDoll(p, worldParent, course, velocity, spin, reason, slide);

        CrashDoll(Person p, Transform worldParent, Course course, Vector3 velocity, Vector3 spin, CrashReason reason, Vector3 slide)
        {
            _p = p;
            _world = new GameObject("Crash");
            _world.transform.SetParent(worldParent, false);
            // The body leaves its rig (which keeps following the run) and falls on its own.
            _home = p.transform.parent;
            _homePos = p.transform.localPosition;
            _homeRot = p.transform.localRotation;
            p.transform.SetParent(_world.transform, true);
            _patch = Ground(course, slide + Flat(velocity) * 0.35f);
            Obstacles(course, slide);
            _doll = Ragdoll.Make(p);
            Throw(velocity, spin, reason);

            // The pull toward the run's own slide (it starts slack: the first flight is the crash's own).
            var a = new GameObject("Slide");
            a.transform.SetParent(_world.transform, false);
            a.transform.position = _doll.Pelvis.position;
            _anchor = a.AddComponent<Rigidbody>();
            _anchor.isKinematic = true;
            _pull = _doll.Pelvis.gameObject.AddComponent<ConfigurableJoint>();
            _pull.connectedBody = _anchor;
            _pull.configuredInWorldSpace = true;
            _pull.autoConfigureConnectedAnchor = false;
            _pull.anchor = Vector3.zero;
            _pull.connectedAnchor = Vector3.zero;
            _pull.xMotion = _pull.yMotion = _pull.zMotion = ConfigurableJointMotion.Free;
            _pull.angularXMotion = _pull.angularYMotion = _pull.angularZMotion = ConfigurableJointMotion.Free;
            Drive(0f);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>The slope around the crash, solid: a mesh of its heights.</summary>
        Mesh Ground(Course c, Vector3 centre)
        {
            var verts = new Vector3[Grid * Grid];
            float x0 = Mathf.Round(centre.x) - Grid / 2, z0 = Mathf.Round(centre.z) - Grid / 2;
            for (int j = 0; j < Grid; j++)
                for (int i = 0; i < Grid; i++)
                {
                    float x = x0 + i, z = z0 + j;
                    verts[j * Grid + i] = new Vector3(x, c.Height(x, z), z);
                }
            var tris = new int[(Grid - 1) * (Grid - 1) * 6];
            int t = 0;
            for (int j = 0; j < Grid - 1; j++)
                for (int i = 0; i < Grid - 1; i++)
                {
                    int a = j * Grid + i, b = a + 1, d = a + Grid, e = d + 1;
                    tris[t++] = a; tris[t++] = d; tris[t++] = b;
                    tris[t++] = b; tris[t++] = d; tris[t++] = e;
                }
            var m = new Mesh { name = "CrashSnow", vertices = verts, triangles = tris };
            var go = new GameObject("Snow");
            go.transform.SetParent(_world.transform, false);
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = m;
            col.sharedMaterial = Snow;
            return m;
        }

        static readonly List<Obstacle> Near = new List<Obstacle>();

        /// <summary>Trees, rocks, fences and boxes close by, solid for the moment.</summary>
        void Obstacles(Course c, Vector3 at)
        {
            Near.Clear();
            c.ObstaclesNear(at.x, at.z, ObstacleReach, Near);
            foreach (var o in Near)
            {
                if (!o.Solid) continue;
                float ground = c.Height(o.X, o.Z);
                float h = Mathf.Clamp(o.Height, 0.4f, 8f);
                var go = new GameObject(o.Kind.ToString());
                go.transform.SetParent(_world.transform, false);
                if (o.IsBox)
                {
                    go.transform.position = new Vector3(o.X, ground + h * 0.5f, o.Z);
                    var b = go.AddComponent<BoxCollider>();
                    b.size = new Vector3(o.Hx * 2f, h, o.Hz * 2f);
                    b.sharedMaterial = Snow;
                }
                else
                {
                    go.transform.position = new Vector3(o.X, ground + h * 0.5f, o.Z);
                    var k = go.AddComponent<CapsuleCollider>();
                    k.direction = 1;
                    k.radius = Mathf.Max(0.12f, o.Radius * (o.Kind == ObstacleKind.Tree || o.Kind == ObstacleKind.DeadTree ? 0.45f : 0.9f));
                    k.height = Mathf.Max(h, k.radius * 2f);
                    k.sharedMaterial = Snow;
                }
            }
        }

        /// <summary>How the body sets off, by what brought it down.</summary>
        void Throw(Vector3 v, Vector3 spin, CrashReason reason)
        {
            float speed = Flat(v).magnitude;
            Vector3 fwd = speed > 0.5f ? Flat(v) / speed : _p.transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float r = Random.value < 0.5f ? -1f : 1f;
            // A spin carried into the crash counts in full only when the crash is a landing askew (the trick's own turn).
            spin = Vector3.ClampMagnitude(spin, 10f) * (reason == CrashReason.BadLanding ? 1f : 0.3f);
            foreach (var b in _doll.Bodies) { b.linearVelocity = v; b.angularVelocity = spin; }
            switch (reason)
            {
                case CrashReason.Person:
                    // Tripped: the feet are caught at the child, the rest goes on, forward and down onto the front.
                    for (int i = 0; i < _doll.Legs.Count; i++) _doll.Legs[i].linearVelocity = v * (i % 2 == 1 ? 0.55f : 0.68f) + Vector3.up * 0.4f;
                    _doll.Pelvis.linearVelocity = v * 0.82f + Vector3.up * 0.9f;
                    Upper(v * 0.95f + Vector3.up * 1.2f);
                    _doll.Chest.angularVelocity = right * Mathf.Clamp(speed / 3f, 1.5f, 4f);
                    break;
                case CrashReason.Trees:
                case CrashReason.Obstacle:
                    // Into something solid: the top is stopped and thrown back, the legs sweep on underneath.
                    Upper(-fwd * (1.2f + speed * 0.12f) + Vector3.up * 0.6f);
                    _doll.Pelvis.linearVelocity = v * 0.3f + Vector3.up * 0.4f;
                    foreach (var l in _doll.Legs) l.linearVelocity = v * 0.65f + Vector3.up * 1.1f;
                    foreach (var b in _doll.Bodies) b.angularVelocity = Vector3.up * r * Random.Range(1.5f, 4f) + fwd * r * Random.Range(0.5f, 2f);
                    break;
                case CrashReason.HardLanding:
                    // Down from too high: the legs fold under the impact and the body is slammed onto the snow.
                    foreach (var l in _doll.Legs) l.linearVelocity = Flat(v) * 0.85f + Vector3.up * Mathf.Min(v.y * 0.3f, -1f);
                    _doll.Pelvis.linearVelocity = Flat(v) * 0.9f + Vector3.up * Mathf.Min(v.y * 0.7f, -2f);
                    Upper(Flat(v) * 0.95f + Vector3.up * (Mathf.Min(v.y, -3f) - 1.5f));
                    _doll.Chest.angularVelocity = right * Random.Range(1f, 2.2f) + fwd * r * Random.Range(0.5f, 1.5f);
                    break;
                case CrashReason.BadLanding:
                    // Landed askew: the spin carries on, and the body rolls over sideways.
                    foreach (var b in _doll.Bodies) b.angularVelocity = spin * 0.8f + fwd * r * Random.Range(4f, 7f);
                    foreach (var a in _doll.Arms) a.linearVelocity += right * r * Random.Range(1f, 2.5f) + Vector3.up * 1.2f;
                    break;
                default:
                    foreach (var b in _doll.Bodies) b.angularVelocity += Random.insideUnitSphere * 3f;
                    break;
            }
        }

        void Upper(Vector3 velocity)
        {
            _doll.Chest.linearVelocity = velocity;
            _doll.HeadBody.linearVelocity = velocity;
            foreach (var a in _doll.Arms) a.linearVelocity = velocity + Random.insideUnitSphere * 1.5f;
        }

        void Drive(float k)
        {
            var d = new JointDrive { positionSpring = Spring * k, positionDamper = Damper, maximumForce = 2500f };
            _pull.xDrive = d; _pull.zDrive = d;
            _pull.yDrive = new JointDrive { positionSpring = Spring * 0.1f * k, positionDamper = Damper * 0.1f, maximumForce = 400f };
        }

        /// <summary>Call every frame with where the run has the skier: the body is drawn there once it has flown.</summary>
        public void Follow(Vector3 slide, float dt)
        {
            _age += dt;
            if (_anchor == null) return;
            _anchor.MovePosition(slide + Vector3.up * 0.3f);
            Drive(Mathf.Clamp01((_age - PullAfter) / (PullFull - PullAfter)));
        }

        /// <summary>Everything the fall made goes, the body with it (its owner is going away too).</summary>
        public void Drop()
        {
            if (_world != null) Object.Destroy(_world);
            if (_patch != null) Object.Destroy(_patch);
        }

        /// <summary>The body goes back to its animation (the run has stood the skier up).</summary>
        public void End()
        {
            if (_pull != null) Object.DestroyImmediate(_pull);
            _doll.End();
            if (_p != null)
            {
                _p.transform.SetParent(_home, false);
                _p.transform.localPosition = _homePos;
                _p.transform.localRotation = _homeRot;
            }
            if (_world != null) Object.Destroy(_world);
            if (_patch != null) Object.Destroy(_patch);
        }
    }
}
