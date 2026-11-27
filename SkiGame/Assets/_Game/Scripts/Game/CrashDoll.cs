using System.Collections.Generic;
using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// A crash on the slope as a body takes it, every one its own. The skier goes limp (a
    /// <see cref="Ragdoll"/>) from the very pose they were in, each limb moving as it was moving the
    /// moment before (<see cref="BoneMotion"/>): mid-turn, mid-push, mid-flip. What happens next is
    /// the physics engine's: the snow (a patch of the slope made solid for the moment), the trees of
    /// the forest and the rocks, fences and boxes close by, and a child who was run into (who goes
    /// limp too), all solid. Nothing is scripted by the kind of crash: a shoulder into a trunk spins
    /// you round, a glancing hit sends you sliding, a flip landed short puts you on your back.
    ///
    /// Only for the eye. The run (the time, where the skier gets up, the replays) is the simulation's,
    /// so the body is kept loosely with the run's own slide: a light match of its speed from the start,
    /// and a gentle pull toward its place only near the end, so getting up does not jump far.
    /// </summary>
    public sealed class CrashDoll
    {
        const int Grid = 33;            // the snow patch: Grid x Grid points a metre apart
        const float ObstacleReach = 14f;
        const float PullAfter = 0.8f, PullFull = 1.6f, Spring = 350f, Damper = 70f;

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

        static PhysicsMaterial _snow, _wood;
        static PhysicsMaterial Snow => _snow != null ? _snow : _snow = new PhysicsMaterial("Snow")
        {
            dynamicFriction = 0.3f, staticFriction = 0.4f, bounciness = 0.04f,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
        };
        static PhysicsMaterial Wood => _wood != null ? _wood : _wood = new PhysicsMaterial("Wood")
        {
            dynamicFriction = 0.5f, staticFriction = 0.6f, bounciness = 0.12f,
        };

        /// <summary>
        /// The body <paramref name="p"/> goes down. <paramref name="motion"/>: how its parts were moving (or
        /// null: all of it at <paramref name="velocity"/>). <paramref name="slide"/>: where the run has the
        /// skier now. Everything made for the fall lives under <paramref name="worldParent"/>.
        /// </summary>
        public static CrashDoll Begin(Person p, Transform worldParent, Course course, BoneMotion motion, Vector3 velocity, Vector3 slide)
            => new CrashDoll(p, worldParent, course, motion, velocity, slide);

        CrashDoll(Person p, Transform worldParent, Course course, BoneMotion motion, Vector3 velocity, Vector3 slide)
        {
            _p = p;
            _world = new GameObject("Crash");
            _world.transform.SetParent(worldParent, false);
            // The body leaves its rig (which keeps following the run) and falls on its own.
            _home = p.transform.parent;
            _homePos = p.transform.localPosition;
            _homeRot = p.transform.localRotation;
            p.transform.SetParent(_world.transform, true);
            Vector3 v = motion != null && motion.Ready ? motion.Velocity : velocity;
            _patch = Ground(course, slide + Flat(v) * 0.4f);
            Solids(course, slide + Flat(v) * 0.3f);
            _doll = Ragdoll.Make(p);
            if (motion != null && motion.Ready) motion.Apply(_doll);
            else foreach (var b in _doll.Bodies) b.linearVelocity = velocity;

            // The loose tie to the run's own slide.
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
        static readonly List<Vector3> NearTrees = new List<Vector3>();

        /// <summary>The forest, rocks, fences and boxes close by, solid for the moment.</summary>
        void Solids(Course c, Vector3 at)
        {
            Near.Clear();
            c.ObstaclesNear(at.x, at.z, ObstacleReach, Near);
            foreach (var o in Near)
            {
                if (!o.Solid) continue;
                float ground = c.Height(o.X, o.Z);
                float h = Mathf.Clamp(o.Height, 0.4f, 8f);
                bool tree = o.Kind == ObstacleKind.Tree || o.Kind == ObstacleKind.DeadTree;
                if (o.IsBox) Box(new Vector3(o.X, ground + h * 0.5f, o.Z), new Vector3(o.Hx * 2f, h, o.Hz * 2f), o.Kind.ToString());
                else Pillar(new Vector3(o.X, ground, o.Z), Mathf.Max(0.12f, o.Radius * (tree ? 0.7f : 0.95f)), Mathf.Max(h, 1f), o.Kind.ToString());
            }
            // The piste's own forest (scenery to the run, which ends at its edge; solid to a body thrown into it).
            if (c is PisteCourse)
            {
                NearTrees.Clear();
                TerrainBuilder.TreesNear(at.x, at.z, ObstacleReach, NearTrees);
                foreach (var t in NearTrees) Pillar(new Vector3(t.x, c.Height(t.x, t.y), t.y), 0.42f * t.z, 5.5f * t.z, "Pine");
            }
        }

        void Box(Vector3 centre, Vector3 size, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_world.transform, false);
            go.transform.position = centre;
            var b = go.AddComponent<BoxCollider>();
            b.size = size;
            b.sharedMaterial = Wood;
        }

        /// <summary>An upright from the ground: a trunk in its lower branches, a rock, a post.</summary>
        void Pillar(Vector3 foot, float radius, float height, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_world.transform, false);
            go.transform.position = foot + Vector3.up * (height * 0.5f);
            var k = go.AddComponent<CapsuleCollider>();
            k.direction = 1;
            k.radius = radius;
            k.height = Mathf.Max(height, radius * 2f);
            k.sharedMaterial = Wood;
        }

        void Drive(float k)
        {
            var d = new JointDrive { positionSpring = Spring * k, positionDamper = Damper, maximumForce = 1500f };
            _pull.xDrive = d; _pull.zDrive = d;
            _pull.yDrive = new JointDrive { positionSpring = Spring * 0.1f * k, positionDamper = Damper * 0.1f, maximumForce = 300f };
        }

        /// <summary>Call every frame with where the run has the skier: the body is kept loosely with it.</summary>
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
