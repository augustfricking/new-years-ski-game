using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// How each part of a <see cref="Person"/> is moving, from one frame to the next: the speed and the
    /// turn of every bone in the world, as the animation and the run move it. A body that goes limp
    /// takes these over (<see cref="Apply"/>), so a crash starts from how every limb really was going:
    /// a leg out in a turn, an arm mid-push, the whole body turning in a flip.
    /// </summary>
    public sealed class BoneMotion
    {
        readonly Person _p;
        readonly Transform[] _bones;
        readonly Vector3[] _pos, _vel, _spin;
        readonly Quaternion[] _rot;
        bool _has;

        public BoneMotion(Person p)
        {
            _p = p;
            _bones = new[] { p.Hips, p.Torso, p.Head, p.ShoulderL, p.ElbowL, p.HandL, p.ShoulderR, p.ElbowR, p.HandR, p.ThighL, p.KneeL, p.FootL, p.ThighR, p.KneeR, p.FootR };
            _pos = new Vector3[_bones.Length]; _vel = new Vector3[_bones.Length]; _spin = new Vector3[_bones.Length];
            _rot = new Quaternion[_bones.Length];
        }

        /// <summary>Forgets the last frame (after a jump in place: a respawn, getting up, a teleport).</summary>
        public void Reset() => _has = false;

        /// <summary>Call once a frame after the body is posed.</summary>
        public void Sample(float dt)
        {
            for (int i = 0; i < _bones.Length; i++)
            {
                var b = _bones[i];
                if (b == null) continue;
                Vector3 p = b.position; Quaternion r = b.rotation;
                if (_has && dt > 1e-4f)
                {
                    Vector3 v = (p - _pos[i]) / dt;
                    // A jump of many metres in a frame is a teleport, not motion.
                    if (v.sqrMagnitude < 60f * 60f)
                    {
                        _vel[i] = Vector3.Lerp(_vel[i], v, 0.6f);
                        (r * Quaternion.Inverse(_rot[i])).ToAngleAxis(out float ang, out Vector3 axis);
                        if (ang > 180f) ang -= 360f;
                        Vector3 w = float.IsInfinity(axis.x) || float.IsNaN(axis.x) ? Vector3.zero : axis * (ang * Mathf.Deg2Rad / dt);
                        _spin[i] = Vector3.Lerp(_spin[i], Vector3.ClampMagnitude(w, 25f), 0.6f);
                    }
                }
                _pos[i] = p; _rot[i] = r;
            }
            _has = true;
        }

        /// <summary>Whether there is motion to hand over (a frame has been seen).</summary>
        public bool Ready => _has;

        /// <summary>Gives each part of a limp body the motion its bone had.</summary>
        public void Apply(Ragdoll doll)
        {
            foreach (var body in doll.Bodies)
            {
                int i = System.Array.IndexOf(_bones, body.transform);
                if (i < 0) continue;
                body.linearVelocity = _vel[i];
                body.angularVelocity = _spin[i];
            }
        }

        /// <summary>The body's own speed (its hips).</summary>
        public Vector3 Velocity => _vel[0];
    }
}
