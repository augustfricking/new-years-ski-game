using System.Collections.Generic;
using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>Shows the crowd on the children's slope: one posable <see cref="Person"/> per kid or parent.</summary>
    public sealed class KidView : MonoBehaviour
    {
        KidCrowd _crowd;
        PisteCourse _course;
        readonly List<Person> _people = new List<Person>();
        readonly List<LineRenderer> _leashes = new List<LineRenderer>();
        Rng _rng;
        Material _leashMat;

        public static KidView Create(KidCrowd crowd, PisteCourse course, Transform parent)
        {
            var go = new GameObject("Kids");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<KidView>();
            v._crowd = crowd; v._course = course; v._rng = new Rng(course.Seed + 500);
            v._leashMat = Mats.Glow(Palette.SignalRed, 1f);
            v.Sync(0f);
            return v;
        }

        public Person PersonFor(Kid k) => k.Id < _people.Count ? _people[k.Id] : null;

        Person Spawn(Kid k)
        {
            bool adult = k.Kind == KidKind.HarnessParent || k.Kind == KidKind.Instructor;
            var look = PersonLook.Random(_rng, !adult, k.Size);
            if (k.Kind == KidKind.Instructor) { look.Jacket = Palette.SignalRed; look.Trim = Palette.White; look.Pants = Palette.Ink; look.Helmet = Palette.SignalRed; }
            if (k.Kind == KidKind.SchoolKid)
            {
                // Ski-school bibs.
                look.Trim = Palette.SignalYellow; look.Jacket = Palette.Mix(look.Jacket, Palette.SignalYellow, 0.55f);
            }
            look.Poles = adult && k.Kind == KidKind.Instructor;
            var p = Person.Create(look, transform, k.Kind.ToString());
            p.Snap(PoseFor(k));
            if (k.Kind == KidKind.Bomber) p.MouthOpen = 1f;
            if (k.Kind == KidKind.HarnessParent)
            {
                var lr = new GameObject("Harness").AddComponent<LineRenderer>();
                lr.transform.SetParent(transform, false);
                lr.positionCount = 2; lr.startWidth = lr.endWidth = 0.035f;
                lr.sharedMaterial = _leashMat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _leashes.Add(lr);
                lr.gameObject.name = "Harness " + k.Id;
            }
            return p;
        }

        static Pose PoseFor(Kid k)
        {
            if (k.Fallen) return Pose.Fallen;
            switch (k.Kind)
            {
                case KidKind.Bomber: return Pose.Bomber;
                case KidKind.Instructor: return Pose.Ski;
                case KidKind.HarnessParent: return Pose.Plough.WithArms(55, 12, 35, 55, 12, 35);
                default: return Pose.Plough;
            }
        }

        /// <summary>Where the player is; people far away are frozen or hidden to save time.</summary>
        public float ViewerZ;

        public void Sync(float dt)
        {
            int leash = 0;
            for (int i = 0; i < _crowd.Kids.Count; i++)
            {
                var k = _crowd.Kids[i];
                if (i >= _people.Count) _people.Add(Spawn(k));
                var p = _people[i];
                float ahead = k.Z - ViewerZ;
                bool visible = k.Active && ahead > -45f && ahead < 260f;
                if (p.gameObject.activeSelf != visible) p.gameObject.SetActive(visible);
                if (!visible)
                {
                    if (k.Kind == KidKind.HarnessParent) { _leashes[leash].enabled = false; leash++; }
                    continue;
                }
                // Far away the pose no longer needs animating.
                p.enabled = ahead < 110f;
                float y = _course.Height(k.X, k.Z);
                Vector3 n = _course.Normal(k.X, k.Z);
                Vector3 fwd = new Vector3(Mathf.Sin(k.Heading), 0f, Mathf.Cos(k.Heading));
                fwd = (fwd - Vector3.Dot(fwd, n) * n).normalized;
                var rot = Quaternion.LookRotation(fwd, Vector3.Lerp(Vector3.up, n, 0.6f));
                p.transform.SetPositionAndRotation(new Vector3(k.X, y, k.Z), Quaternion.Slerp(p.transform.rotation, rot, dt <= 0f ? 1f : 1f - Mathf.Exp(-8f * dt)));
                p.Target = PoseFor(k);
                if (k.Kind == KidKind.Bomber) p.Target.TorsoRoll = Mathf.Sin(Time.time * 6f + k.Id) * 8f;
                if (k.Kind == KidKind.HarnessParent)
                {
                    var lr = _leashes[leash++];
                    var kid = PersonFor(k.Leader);
                    bool show = kid != null && k.Leader.Active;
                    lr.enabled = show;
                    if (show)
                    {
                        lr.SetPosition(0, p.HandR.position);
                        lr.SetPosition(1, kid.transform.position + Vector3.up * 0.55f * k.Leader.Size / 0.5f * 0.6f);
                    }
                }
            }
        }
    }
}
