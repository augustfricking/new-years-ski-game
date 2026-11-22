using System.Collections.Generic;
using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// People in the village below the finish: some walking between the tavern, the lift and the
    /// ski racks, some standing about talking, and skiers gliding from the finish to the lift
    /// queue. Ski into one and you both go down. Asleep until the player is near.
    /// </summary>
    public sealed class VillageLife : MonoBehaviour
    {
        enum Kind { Walker, Glider, Stander }
        sealed class Citizen
        {
            public Person P; public Kind Kind;
            public Vector2 Pos, Target; public float Speed, Phase, Wait, Heading;
            public int Step;
            public float Down; public Vector2 FallDir;   // knocked over: seconds left on the snow
        }

        PisteCourse _c;
        readonly List<Citizen> _people = new List<Citizen>();
        Vector2[] _spots;
        Rng _rng;
        /// <summary>Where the player is down the slope; the village sleeps while they are far above it.</summary>
        public float ViewerZ;

        public static VillageLife Create(PisteCourse c, Transform parent)
        {
            var go = new GameObject("Village life");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<VillageLife>();
            v.Build(c);
            return v;
        }

        void Build(PisteCourse c)
        {
            _c = c;
            _rng = new Rng(c.Seed * 5 + 77);
            float x0 = c.VillageX, z0 = c.FinishZ;
            // Places people go: the tavern porch, the lift queue, the far end of the plaza, the sides.
            _spots = new[]
            {
                new Vector2(c.TavernZone.X - 5f, c.TavernZone.Z + 3f), new Vector2(c.LiftZone.X + 7f, c.LiftZone.Z + 5f),
                new Vector2(x0 + 4f, z0 + 118f), new Vector2(x0 - 14f, z0 + 104f), new Vector2(x0 + 18f, z0 + 70f),
                new Vector2(x0 - 20f, z0 + 58f), new Vector2(x0 + 12f, z0 + 104f), new Vector2(x0 - 6f, z0 + 92f),
            };
            for (int i = 0; i < 7; i++)
            {
                var look = PersonLook.Random(_rng, false);
                look.Gear = GearKind.None; look.Poles = false; look.GogglesDown = false;
                look.HasHelmet = _rng.Chance(0.4f); look.Beanie = !look.HasHelmet && _rng.Chance(0.7f);
                var cz = Add(look, Kind.Walker, _spots[_rng.Range(0, _spots.Length)]);
                cz.Speed = _rng.Range(1.1f, 1.6f);
                cz.Target = _spots[_rng.Range(0, _spots.Length)];
            }
            for (int i = 0; i < 4; i++)
            {
                var look = PersonLook.Random(_rng, false);
                var cz = Add(look, Kind.Glider, new Vector2(x0 + _rng.Range(-9f, 9f), z0 + 6f + i * 16f));
                cz.Speed = _rng.Range(2.4f, 3.6f);
                cz.Target = new Vector2(c.LiftZone.X + 6f + i, c.LiftZone.Z - 2f + i);
            }
            // Little groups standing in the snow, talking.
            foreach (var centre in new[] { new Vector2(x0 + 14f, z0 + 86f), new Vector2(x0 - 12f, z0 + 68f), new Vector2(x0 + 2f, z0 + 108f) })
            {
                int n = _rng.Range(2, 4);
                for (int i = 0; i < n; i++)
                {
                    var look = PersonLook.Random(_rng, false);
                    look.Gear = GearKind.None; look.Poles = false; look.GogglesDown = false; look.HasHelmet = false; look.Beanie = _rng.Chance(0.6f);
                    float a = i * Mathf.PI * 2f / n + centre.x;
                    var cz = Add(look, Kind.Stander, centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.85f);
                    cz.Heading = Mathf.Atan2(-Mathf.Cos(a), -Mathf.Sin(a));
                }
            }
            Place(0f);
            gameObject.SetActive(false);
        }

        Citizen Add(PersonLook look, Kind kind, Vector2 at)
        {
            var p = Person.Create(look, transform, kind.ToString());
            p.Crowd = true;
            p.SetGogglesUp(true, true);
            var cz = new Citizen { P = p, Kind = kind, Pos = at, Phase = _rng.Range(0f, 6f) };
            p.Snap(kind == Kind.Glider ? Pose.Ski : Pose.Stand);
            _people.Add(cz);
            return cz;
        }

        public static Pose Walk(float phase)
        {
            var p = Pose.Stand;
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            p.ThighL = 4f + 24f * s; p.KneeL = 8f + 34f * Mathf.Max(0f, -c);
            p.ThighR = 4f - 24f * s; p.KneeR = 8f + 34f * Mathf.Max(0f, c);
            p.ArmFwdL = -22f * s; p.ArmFwdR = 22f * s; p.ElbowL = 22f + 10f * Mathf.Max(0f, -s); p.ElbowR = 22f + 10f * Mathf.Max(0f, s);
            p.TorsoPitch = 5f; p.TorsoYaw = -6f * s; p.TorsoRoll = 2.5f * c; p.LegOut = 2f;
            return p;
        }

        /// <summary>
        /// A person standing where the skier is: they are knocked over (true) and the skier falls.
        /// </summary>
        public bool Hit(Vector3 pos, Vector3 vel, float radius)
        {
            if (!gameObject.activeSelf) return false;
            foreach (var cz in _people)
            {
                if (cz.Down > 0f) continue;
                Vector2 d = cz.Pos - new Vector2(pos.x, pos.z);
                float rr = radius + 0.32f;
                if (d.sqrMagnitude > rr * rr) continue;
                Vector2 v = new Vector2(vel.x, vel.z);
                cz.FallDir = v.sqrMagnitude > 0.01f ? v.normalized : (d.sqrMagnitude > 1e-4f ? d.normalized : Vector2.up);
                cz.Down = 3.2f;
                cz.P.PoseSpeed = 12f;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Somebody standing about is never quite still: weight from foot to foot, a look round,
        /// stamping for warmth, a stretch, a wave, a laugh. <paramref name="style"/> picks the habit.
        /// </summary>
        public static Pose Idle(float t, float phase, int style)
        {
            var p = Pose.Stand;
            float slow = Mathf.Sin(t * 0.7f + phase), mid = Mathf.Sin(t * 1.9f + phase * 1.7f);
            p.TorsoRoll = slow * 3.5f;
            p.LegOut = 3f + Mathf.Max(0f, slow) * 3f;
            p.HeadYaw = Mathf.Sin(t * 0.45f + phase * 2.3f) * 35f;
            p.HeadPitch = Mathf.Sin(t * 0.3f + phase) * 6f;
            // A habit every few seconds, held for a moment.
            float cycle = Mathf.Repeat(t * 0.16f + phase * 0.13f, 1f);
            float on = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.62f, cycle)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 0.97f, cycle)));
            switch (style % 5)
            {
                case 0: // stamping the cold out of the feet
                    float stamp = Mathf.Max(0f, Mathf.Sin(t * 7f + phase)) * on;
                    p.ThighL += stamp * 28f; p.KneeL += stamp * 40f;
                    p.ArmFwdL = 25f * on; p.ArmFwdR = 25f * on; p.ElbowL = 90f * on; p.ElbowR = 90f * on;   // hands tucked in
                    break;
                case 1: // a big wave at someone across the plaza
                    p.ArmOutR = Mathf.Lerp(p.ArmOutR, 150f + mid * 20f, on); p.ElbowR = Mathf.Lerp(p.ElbowR, 20f, on);
                    break;
                case 2: // a stretch, arms up
                    p.ArmOutL = Mathf.Lerp(p.ArmOutL, 165f, on); p.ArmOutR = Mathf.Lerp(p.ArmOutR, 165f, on);
                    p.TorsoPitch = -8f * on; p.HeadPitch -= 14f * on;
                    break;
                case 3: // laughing, bent over a little
                    p.TorsoPitch = (14f + Mathf.Abs(Mathf.Sin(t * 9f)) * 8f) * on;
                    p.ArmFwdL = 40f * on; p.ElbowL = 60f * on;
                    break;
                default: // hands on hips
                    p.ArmOutL = Mathf.Lerp(p.ArmOutL, 40f, on); p.ArmOutR = Mathf.Lerp(p.ArmOutR, 40f, on);
                    p.ElbowL = 95f * on; p.ElbowR = 95f * on; p.ArmTwistL = 60f * on; p.ArmTwistR = 60f * on;
                    break;
            }
            return p;
        }

        /// <summary>Call every frame with the player's position down the slope.</summary>
        public void Tick(float viewerZ, float dt)
        {
            bool awake = viewerZ > _c.FinishZ - 260f;
            if (gameObject.activeSelf != awake) gameObject.SetActive(awake);
            if (awake) Place(dt);
        }

        void Place(float dt)
        {
            float t = Time.time;
            int index = 0;
            foreach (var cz in _people)
            {
                var p = cz.P;
                index++;
                if (cz.Down > 0f)
                {
                    // On their back in the snow, then up again.
                    cz.Down -= dt;
                    float fall = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, (3.2f - cz.Down) / 0.35f)) * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, cz.Down / 0.7f));
                    var flat = Pose.Stand;
                    flat.ArmOutL = 70f; flat.ArmOutR = 60f; flat.LegOut = 12f; flat.KneeL = 25f;
                    p.Target = Pose.Lerp(Pose.Stand, flat, fall);
                    Vector3 axis = Vector3.Cross(Vector3.up, new Vector3(cz.FallDir.x, 0f, cz.FallDir.y)).normalized;
                    float yy = _c.Height(cz.Pos.x, cz.Pos.y);
                    p.transform.SetPositionAndRotation(new Vector3(cz.Pos.x, yy + 0.12f * fall, cz.Pos.y), Quaternion.AngleAxis(84f * fall, axis) * Quaternion.Euler(0f, cz.Heading * Mathf.Rad2Deg, 0f));
                    if (cz.Down <= 0f) p.PoseSpeed = 6f;
                    continue;
                }
                if (cz.Kind == Kind.Stander)
                {
                    // Shifting their weight, a hand moving as they talk, glancing about.
                    var pose = Pose.Stand;
                    float k = Mathf.Max(0f, Mathf.Sin(t * 1.9f + cz.Phase));
                    pose.ArmFwdR = 10f + k * 38f; pose.ElbowR = 30f + k * 50f; pose.TorsoRoll = Mathf.Sin(t * 0.6f + cz.Phase) * 3f;
                    pose.HeadYaw = Mathf.Sin(t * 0.4f + cz.Phase * 2f) * 28f;
                    p.Target = pose;
                }
                else
                {
                    if (cz.Wait > 0f)
                    {
                        cz.Wait -= dt;
                        if (cz.Kind == Kind.Glider) { var sp = Pose.Ski; sp.HeadYaw = Mathf.Sin(t * 0.5f + cz.Phase) * 30f; sp.TorsoRoll = Mathf.Sin(t * 0.8f + cz.Phase) * 3f; p.Target = sp; }
                        else p.Target = Idle(t, cz.Phase, index);
                    }
                    else
                    {
                        Vector2 to = cz.Target - cz.Pos;
                        float dist = to.magnitude;
                        if (dist < 1.2f)
                        {
                            // Arrived: a pause, then somewhere else. Skiers reaching the lift start again from the finish.
                            cz.Wait = _rng.Range(1.5f, 6f);
                            if (cz.Kind == Kind.Glider)
                            {
                                cz.Pos = new Vector2(_c.VillageX + _rng.Range(-10f, 10f), _c.FinishZ + _rng.Range(2f, 10f));
                                cz.Wait = _rng.Range(0f, 5f);
                            }
                            else cz.Target = _spots[_rng.Range(0, _spots.Length)];
                        }
                        else
                        {
                            float want = Mathf.Atan2(to.x, to.y);
                            cz.Heading = Mathf.LerpAngle(cz.Heading * Mathf.Rad2Deg, want * Mathf.Rad2Deg, 1f - Mathf.Exp(-3f * dt)) * Mathf.Deg2Rad;
                            float speed = cz.Kind == Kind.Glider ? Mathf.Min(cz.Speed, 0.8f + dist * 0.4f) : cz.Speed;
                            cz.Pos += new Vector2(Mathf.Sin(cz.Heading), Mathf.Cos(cz.Heading)) * speed * dt;
                            cz.Phase += dt * speed * (cz.Kind == Kind.Glider ? 1.4f : 4.4f);
                            if (cz.Kind == Kind.Walker) p.Target = Walk(cz.Phase);
                            else
                            {
                                // Skating along on the flat: one ski out, then the other, poles swinging.
                                var pose = Pose.Ski;
                                float s = Mathf.Sin(cz.Phase);
                                pose.TorsoPitch = 14f; pose.TorsoRoll = s * 7f; pose.LegOut = 7f + Mathf.Abs(s) * 5f;
                                pose.ThighL = 18f + s * 10f; pose.ThighR = 18f - s * 10f; pose.KneeL = 36f + s * 14f; pose.KneeR = 36f - s * 14f;
                                pose.ArmFwdL = 30f + s * 24f; pose.ArmFwdR = 30f - s * 24f;
                                p.Target = pose;
                            }
                        }
                    }
                }
                float y = _c.Height(cz.Pos.x, cz.Pos.y);
                p.transform.SetPositionAndRotation(new Vector3(cz.Pos.x, y, cz.Pos.y), Quaternion.Euler(0f, cz.Heading * Mathf.Rad2Deg, 0f));
            }
        }
    }
}
