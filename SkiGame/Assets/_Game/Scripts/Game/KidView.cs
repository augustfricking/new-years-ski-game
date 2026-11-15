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
        readonly List<float> _prevHeading = new List<float>(), _turn = new List<float>();
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

        /// <summary>The pose right now: everyone wobbles, balances, looks about and waves in their own rhythm.</summary>
        static Pose Act(Kid k, float bank)
        {
            var p = PoseFor(k);
            float t = Time.time * (0.85f + (k.Id % 7) * 0.05f) + k.Id * 1.37f;
            float wob = Mathf.Sin(t * 3.1f), slow = Mathf.Sin(t * 0.9f), bob = Mathf.Sin(t * 2.4f);
            if (k.Fallen)
            {
                // Sitting in the snow: one arm waves for help, the other pushes at the ground.
                p.ArmFwdL = 70f + Mathf.Sin(t * 5f) * 30f; p.ArmOutL = 60f + Mathf.Cos(t * 5f) * 25f; p.ElbowL = 30f;
                p.ArmFwdR = -20f + bob * 10f; p.HeadYaw = slow * 30f; p.TorsoPitch += bob * 6f;
                return p;
            }
            switch (k.Kind)
            {
                case KidKind.Bomber:
                    // Arms up and flapping, knees pumping, screaming straight down.
                    p.ArmOutL = 118f + Mathf.Sin(t * 9f) * 26f; p.ArmOutR = 118f + Mathf.Sin(t * 9f + 2f) * 26f;
                    p.ElbowL = 24f + Mathf.Sin(t * 7f) * 18f; p.ElbowR = 24f + Mathf.Cos(t * 7f) * 18f;
                    p.TorsoRoll = Mathf.Sin(t * 6f) * 9f; p.TorsoPitch += Mathf.Sin(t * 4f) * 6f;
                    p.KneeL += Mathf.Sin(t * 8f) * 10f; p.KneeR += Mathf.Cos(t * 8f) * 10f;
                    p.HeadPitch = -6f + bob * 6f;
                    break;
                case KidKind.Instructor:
                    // Clean turns, and a look back over the shoulder at the class now and then.
                    p.TorsoRoll = -bank * 0.4f; p.TorsoYaw = bank * 0.6f;
                    p.ThighL += Mathf.Abs(bank) * 0.5f; p.KneeL += Mathf.Abs(bank); p.ThighR += Mathf.Abs(bank) * 0.5f; p.KneeR += Mathf.Abs(bank);
                    float back = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Sin(t * 0.45f) * 3f - 1.8f));
                    p.TorsoYaw += back * 42f; p.HeadYaw = back * 78f; p.ArmOutR += back * 40f; p.ArmFwdR -= back * 40f;
                    break;
                case KidKind.HarnessParent:
                    p.TorsoPitch += 4f + bob * 3f; p.TorsoRoll = -bank * 0.4f;
                    p.ArmFwdL += wob * 6f; p.ArmFwdR -= wob * 6f; p.HeadPitch = 10f;   // watching the child
                    break;
                default:
                    // A wobbly snowplough: arms out like wings, always correcting.
                    p.ArmOutL = 52f + wob * 24f; p.ArmOutR = 52f - wob * 24f;
                    p.ArmFwdL = 28f + Mathf.Sin(t * 2.3f) * 14f; p.ArmFwdR = 28f + Mathf.Cos(t * 2.1f) * 14f;
                    p.ElbowL = 28f + bob * 12f; p.ElbowR = 28f - bob * 12f;
                    p.TorsoRoll = -wob * 7f - bank * 0.3f; p.TorsoPitch += Mathf.Sin(t * 1.7f) * 6f; p.TorsoYaw = slow * 10f;
                    p.ThighL += bob * 5f; p.KneeL += bob * 10f; p.ThighR += bob * 5f; p.KneeR += bob * 10f;
                    p.HeadYaw = slow * 26f; p.HeadPitch = -6f + Mathf.Sin(t * 1.3f) * 8f;
                    if (k.Kind == KidKind.SchoolKid) { p.ArmOutL *= 0.7f; p.ArmOutR *= 0.7f; p.HeadYaw *= 0.4f; }   // concentrating on the kid in front
                    break;
            }
            return p;
        }

        /// <summary>Where the player is; people far away are frozen or hidden to save time.</summary>
        public float ViewerZ;

        public void Sync(float dt)
        {
            int leash = 0;
            for (int i = 0; i < _crowd.Kids.Count; i++)
            {
                var k = _crowd.Kids[i];
                if (i >= _people.Count) { _people.Add(Spawn(k)); _prevHeading.Add(k.Heading); _turn.Add(0f); }
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
                // How hard they are turning: the body banks into it.
                if (dt > 0f)
                {
                    float rate = Mathf.DeltaAngle(_prevHeading[i] * Mathf.Rad2Deg, k.Heading * Mathf.Rad2Deg) / dt;
                    _turn[i] = Mathf.Lerp(_turn[i], Mathf.Clamp(rate, -90f, 90f), 1f - Mathf.Exp(-5f * dt));
                    _prevHeading[i] = k.Heading;
                }
                float bank = k.Fallen ? 0f : Mathf.Clamp(_turn[i] * 0.3f, -20f, 20f);
                var rot = Quaternion.LookRotation(fwd, Vector3.Lerp(Vector3.up, n, 0.6f)) * Quaternion.Euler(0f, 0f, -bank);
                p.transform.SetPositionAndRotation(new Vector3(k.X, y, k.Z), Quaternion.Slerp(p.transform.rotation, rot, dt <= 0f ? 1f : 1f - Mathf.Exp(-8f * dt)));
                p.Target = Act(k, bank);
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
