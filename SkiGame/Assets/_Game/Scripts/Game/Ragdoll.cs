using System.Collections.Generic;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// Makes a <see cref="Person"/> go limp: each bone gets a body, a collider and a joint to its
    /// parent, and the physics engine throws them about (into tables, benches, walls, each other).
    /// The joints are made in a standing pose, so their limits are those of a body, whatever pose
    /// the person was in when hit. <see cref="End"/> takes it all away and hands the person back
    /// to their animation, lying where they came to rest.
    /// </summary>
    public sealed class Ragdoll
    {
        readonly Person _p;
        readonly List<Object> _added = new List<Object>();
        readonly List<Rigidbody> _bodies = new List<Rigidbody>();
        readonly List<Collider> _colliders = new List<Collider>();
        public Rigidbody Pelvis { get; private set; }
        public Rigidbody Chest { get; private set; }
        public Rigidbody HeadBody { get; private set; }
        /// <summary>Upper and lower arms (left then right), thighs and shins (left then right).</summary>
        public readonly List<Rigidbody> Arms = new List<Rigidbody>(), Legs = new List<Rigidbody>();
        /// <summary>Every part of the body.</summary>
        public IReadOnlyList<Rigidbody> Bodies => _bodies;
        public float Age;
        float _massScale = 1f;

        Ragdoll(Person p) { _p = p; }

        /// <summary>The fastest any part of the body is moving.</summary>
        public float Speed
        {
            get
            {
                float s = 0f;
                foreach (var b in _bodies) s = Mathf.Max(s, b.linearVelocity.magnitude);
                return s;
            }
        }

        public static Ragdoll Make(Person p)
        {
            var r = new Ragdoll(p);
            r.Build();
            return r;
        }

        void Build()
        {
            var p = _p;
            // Where every bone is now, so the body can be put back after the joints are made.
            var bones = new[] { p.Hips, p.Torso, p.Head, p.ShoulderL, p.ElbowL, p.HandL, p.ShoulderR, p.ElbowR, p.HandR, p.ThighL, p.KneeL, p.FootL, p.ThighR, p.KneeR, p.FootR };
            var pos = new Vector3[bones.Length]; var rot = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) { pos[i] = bones[i].localPosition; rot[i] = bones[i].localRotation; }
            var current = p.Current;
            var rest = Pose.Stand;
            rest.ElbowL = rest.ElbowR = 15f; rest.KneeL = rest.KneeR = 8f;
            p.Snap(rest);
            p.enabled = false;   // the animation lets go

            float k = Mathf.Max(0.5f, p.Head.localPosition.magnitude / 0.5f);   // body size against a 0.5 m torso
            _massScale = Mathf.Clamp(k * k * k, 0.2f, 1.6f);                       // a child weighs a fraction of a grown-up
            float shoulders = Mathf.Abs(p.ShoulderR.localPosition.x - p.ShoulderL.localPosition.x);
            float hips = Mathf.Abs(p.ThighR.localPosition.x - p.ThighL.localPosition.x);

            Pelvis = Body(p.Hips, null, 12f);
            Box(p.Hips, new Vector3(0f, 0.01f * k, 0f), new Vector3(hips + 0.15f * k, 0.22f * k, 0.22f * k));
            Chest = Body(p.Torso, Pelvis, 22f);
            Box(p.Torso, p.Head.localPosition * 0.5f, new Vector3(shoulders + 0.08f * k, p.Head.localPosition.magnitude, 0.22f * k));
            Joint(Chest, Pelvis, -25f, 55f, 25f, 20f);
            HeadBody = Body(p.Head, Chest, 6f);
            Sphere(p.Head, new Vector3(0f, 0.11f * k, 0.01f * k), 0.12f * k);
            Joint(HeadBody, Chest, -35f, 45f, 45f, 30f);

            foreach (int side in new[] { -1, 1 })
            {
                var sh = side < 0 ? p.ShoulderL : p.ShoulderR;
                var el = side < 0 ? p.ElbowL : p.ElbowR;
                var hand = side < 0 ? p.HandL : p.HandR;
                var upper = Body(sh, Chest, 2.5f);
                Capsule(sh, el.localPosition, 0.055f * k, 0f);
                Joint(upper, Chest, -150f, 60f, 45f, 100f);
                var fore = Body(el, upper, 1.8f);
                Capsule(el, hand.localPosition, 0.048f * k, 0.08f * k);
                Joint(fore, upper, -140f, 2f, 8f, 8f);
                Arms.Add(upper); Arms.Add(fore);

                var hip = side < 0 ? p.ThighL : p.ThighR;
                var knee = side < 0 ? p.KneeL : p.KneeR;
                var foot = side < 0 ? p.FootL : p.FootR;
                var thigh = Body(hip, Pelvis, 8f);
                Capsule(hip, knee.localPosition, 0.075f * k, 0f);
                Joint(thigh, Pelvis, -110f, 30f, 20f, 45f);
                var shin = Body(knee, thigh, 4.5f);
                Capsule(knee, foot.localPosition, 0.058f * k, 0.06f * k);
                Joint(shin, thigh, -2f, 140f, 5f, 5f);
                Legs.Add(thigh); Legs.Add(shin);
            }

            // Parts of one body do not collide with each other.
            for (int i = 0; i < _colliders.Count; i++)
                for (int j = i + 1; j < _colliders.Count; j++)
                    Physics.IgnoreCollision(_colliders[i], _colliders[j]);

            // Back into the pose they were hit in; the joints keep the standing pose as their rest.
            for (int i = 0; i < bones.Length; i++) { bones[i].localPosition = pos[i]; bones[i].localRotation = rot[i]; }
            Physics.SyncTransforms();
        }

        Rigidbody Body(Transform bone, Rigidbody parent, float mass)
        {
            var b = bone.gameObject.AddComponent<Rigidbody>();
            b.mass = mass * _massScale;
            b.linearDamping = 0.05f;
            b.angularDamping = 0.6f;
            b.interpolation = RigidbodyInterpolation.Interpolate;
            b.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            b.maxDepenetrationVelocity = 2f;
            b.solverIterations = 10;
            _added.Add(b);
            _bodies.Add(b);
            return b;
        }

        void Box(Transform bone, Vector3 centre, Vector3 size)
        {
            var c = bone.gameObject.AddComponent<BoxCollider>();
            c.center = centre; c.size = size;
            Keep(c);
        }

        void Sphere(Transform bone, Vector3 centre, float radius)
        {
            var c = bone.gameObject.AddComponent<SphereCollider>();
            c.center = centre; c.radius = radius;
            Keep(c);
        }

        /// <summary>A capsule from the bone to its child (given in the bone's space), a little longer for a hand or foot.</summary>
        void Capsule(Transform bone, Vector3 to, float radius, float extra)
        {
            var c = bone.gameObject.AddComponent<CapsuleCollider>();
            Vector3 a = new Vector3(Mathf.Abs(to.x), Mathf.Abs(to.y), Mathf.Abs(to.z));
            c.direction = a.x > a.y && a.x > a.z ? 0 : a.y > a.z ? 1 : 2;
            float len = to.magnitude + extra;
            c.center = to.normalized * len * 0.5f;
            c.height = len + radius * 2f;
            c.radius = radius;
            Keep(c);
        }

        void Keep(Collider c)
        {
            var m = Friction;
            c.sharedMaterial = m;
            _added.Add(c);
            _colliders.Add(c);
        }

        static PhysicsMaterial _friction;
        static PhysicsMaterial Friction => _friction != null ? _friction : _friction = new PhysicsMaterial("Body") { dynamicFriction = 0.6f, staticFriction = 0.7f, bounciness = 0.15f };

        /// <summary>A joint to the parent body, bending around the bone's sideways axis (as knees, elbows and hips do).</summary>
        void Joint(Rigidbody body, Rigidbody parent, float low, float high, float swing1, float swing2)
        {
            var j = body.gameObject.AddComponent<CharacterJoint>();
            j.connectedBody = parent;
            j.axis = Vector3.right;
            j.swingAxis = Vector3.up;
            j.lowTwistLimit = new SoftJointLimit { limit = low };
            j.highTwistLimit = new SoftJointLimit { limit = high };
            j.swing1Limit = new SoftJointLimit { limit = swing1 };
            j.swing2Limit = new SoftJointLimit { limit = swing2 };
            j.enableProjection = true;
            j.enablePreprocessing = false;
            _added.Insert(0, j);   // joints go first when it is all taken away
        }

        /// <summary>Sends the whole body moving, and a part of it harder (the one that took the punch).</summary>
        public void Throw(Vector3 velocity, Vector3 spin, Rigidbody struck, Vector3 extra)
        {
            foreach (var b in _bodies) { b.linearVelocity = velocity; b.angularVelocity = spin; }
            if (struck != null) struck.linearVelocity += extra;
        }

        /// <summary>Takes the physics away; the person lies where the pelvis came to rest.</summary>
        public void End()
        {
            // Joints first, then bodies: a body cannot go while a joint still hangs on it.
            foreach (var o in _added) if (o != null) Object.DestroyImmediate(o);
            _added.Clear(); _bodies.Clear(); _colliders.Clear(); Arms.Clear(); Legs.Clear();
            if (_p != null) _p.enabled = true;
        }
    }
}
