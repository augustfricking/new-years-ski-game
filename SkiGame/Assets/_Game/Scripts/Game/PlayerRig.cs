using Ski.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ski.Game
{
    /// <summary>
    /// The player: a full <see cref="Person"/> posed from the simulation state each frame, and the
    /// camera. In first person the camera sits at the eyes (the head only casts its shadow), so
    /// looking down shows the real body: the snowplough V, the tuck, pole pushes, edging, grabs.
    /// In third person the camera follows from behind, which is how the snow park is ridden.
    /// </summary>
    public sealed class PlayerRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public Person Body { get; private set; }
        /// <summary>Mouse look in degrees: x = yaw (right positive), y = pitch (up positive).</summary>
        public Vector2 Look;
        public float BaseFov = 74f;
        public bool ThirdPerson { get; private set; }

        bool _board;
        Vector3 _up = Vector3.up, _heading = Vector3.forward, _tpPos;
        float _poleAnim = 1f, _shake, _shakeTime, _fov, _crashSpin, _lift, _third, _visualYaw, _hopSide = 1f;
        bool _tpInit;
        Pose _pose;
        // A crash taken by the body (physics), and how each of its parts was moving just before.
        CrashDoll _doll;
        BoneMotion _motion;
        Quaternion _crashCam;

        /// <summary>Whether the body is down in a physical crash right now.</summary>
        public bool Limp => _doll != null;

        /// <summary>
        /// The run says the skier crashed: the body goes limp and takes the fall its own way, by what
        /// brought it down (see <see cref="CrashDoll"/>). <paramref name="world"/> holds what the fall needs.
        /// </summary>
        public void Crash(SkiSim sim, Transform world)
        {
            if (_doll != null || Body == null) return;
            _doll = CrashDoll.Begin(Body, world, sim.Course, _motion, sim.Vel, sim.Pos);
            _crashCam = Cam.transform.rotation;
        }

        /// <summary>Gives the body back to its animation at once (leaving the run, or a replay starting).</summary>
        public void EndCrash()
        {
            if (_doll == null) return;
            _doll.End();
            _doll = null;
            _motion?.Reset();      // back on its feet somewhere else: no motion from the fall
        }

        public static PlayerRig Create(CharacterId who, Transform parent)
        {
            var go = new GameObject("Player " + SkierInfo.Get(who).Name);
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<PlayerRig>();
            rig.Build(who, parent);
            return rig;
        }

        void Build(CharacterId who, Transform parent)
        {
            _board = SkierInfo.Get(who).Gear == Gear.Snowboard;

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(parent, false);
            camGo.tag = "MainCamera";
            Cam = camGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.06f; Cam.farClipPlane = GraphicsQuality.Far; GraphicsQuality.Outdoor = Cam; Cam.fieldOfView = _fov = BaseFov;
            Cam.clearFlags = CameraClearFlags.Skybox;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGo.AddComponent<AudioListener>();

            _who = who;
            MakeBody();
            _pose = _board ? BoardBase() : Pose.Ski;
            Body.Snap(_pose);
        }

        CharacterId _who;

        void MakeBody()
        {
            var look = PersonLook.For(_who);
            look.GogglesDown = true;
            Body = Person.Create(look, transform, "Body");
            Body.Alive = false;
            Body.SideStance = _board ? 1f : 0f;
            Body.Snap(_pose);
            Body.SetHeadVisible(_third > 0.2f);
        }

        /// <summary>Puts the body together again (after a visit to the shop).</summary>
        public void RebuildBody()
        {
            if (Body != null) Destroy(Body.gameObject);
            MakeBody();
        }

        // Leaving the run mid-crash: what the fall made goes with the run's world (nothing to hand back).
        void OnDestroy() { _doll = null; if (Cam != null) Destroy(Cam.gameObject); }

        /// <summary>Adds a camera shake impulse (landing, crash, bump).</summary>
        public void Kick(float amount) { _shake = Mathf.Max(_shake, amount); }
        public void PolePush() { _poleAnim = 0f; _hopSide = -_hopSide; }
        public void SetThirdPerson(bool on) { ThirdPerson = on; }

        static Pose BoardBase()
        {
            var p = Pose.Ski;
            p.LegOut = 13f; p.TorsoPitch = 12f; p.HeadPitch = -6f;
            p.ArmFwdL = 12f; p.ArmOutL = 42f; p.ElbowL = 24f;
            p.ArmFwdR = -4f; p.ArmOutR = 40f; p.ElbowR = 28f;
            return p;
        }

        Pose TargetPose(SkiSim sim, bool air, out float airTuck)
        {
            float tuck = sim.Tuck, plough = sim.Plough, lean = sim.Lean;
            airTuck = air ? Mathf.Clamp01(sim.AirTime * 4f) * 0.5f : 0f;
            if (sim.ActiveGrab != 0 && sim.ActiveGrab != 4) airTuck = 1f;
            if (sim.Crashed)
            {
                var f = Pose.Fallen;
                f.ArmOutL = 70f; f.ArmOutR = 80f; f.ArmFwdL = 20f; f.ArmFwdR = -10f;
                return f;
            }
            Pose p;
            if (_board)
            {
                p = Pose.Lerp(BoardBase(), Pose.Tuck, tuck * 0.7f);
                p.LegOut = 13f; p.FootIn = 0f;
                p.ThighL += plough * 14f; p.KneeL += plough * 28f; p.ThighR += plough * 14f; p.KneeR += plough * 28f;
                if (tuck > 0f) { p.ArmOutL = Mathf.Lerp(42f, 14f, tuck); p.ArmOutR = Mathf.Lerp(40f, 14f, tuck); }
            }
            else
            {
                p = Pose.Lerp(Pose.Ski, Pose.Tuck, tuck);
                p = Pose.Lerp(p, Pose.Plough, plough);
                p.FootIn = plough * 17f;
                p.LegOut = Mathf.Lerp(5f, 4f, tuck);
            }
            // Knees come up in the air.
            p.ThighL += airTuck * 34f; p.KneeL += airTuck * 62f; p.ThighR += airTuck * 34f; p.KneeR += airTuck * 62f;
            p.TorsoPitch += airTuck * 8f;
            // Leaning into a turn: the body angulates, the inside hand drops toward the snow.
            p.TorsoRoll = -lean * 7f; p.HeadRoll = lean * 5f;
            float hands = 1f - tuck;
            p.ArmOutR += lean * 12f * hands; p.ArmFwdR -= lean * 10f * hands;
            p.ArmOutL -= lean * 12f * hands; p.ArmFwdL += lean * 10f * hands;
            if (_board && _poleAnim < 1f)
            {
                // The hop: knees snap up, arms swing for balance.
                float hopUp = Mathf.Sin(Mathf.PI * Mathf.Clamp01(_poleAnim / 0.8f));
                p.ThighL += hopUp * 22f; p.KneeL += hopUp * 40f; p.ThighR += hopUp * 22f; p.KneeR += hopUp * 40f;
                p.ArmOutL += hopUp * 26f; p.ArmOutR += hopUp * 26f; p.TorsoYaw += _hopSide * hopUp * 14f;
            }

            if (!_board && _poleAnim < 1f)
            {
                // Reach forward, plant, pull back.
                float t = _poleAnim;
                float reach = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
                float pull = Mathf.Clamp01((t - 0.3f) / 0.7f);
                float fwd = 30f * reach - 58f * pull, elbow = -22f * reach - 26f * pull;
                p.ArmFwdL += fwd; p.ArmFwdR += fwd; p.ElbowL += elbow; p.ElbowR += elbow;
                p.TorsoPitch += 6f * reach;
            }
            if (air)
            {
                switch (sim.ActiveGrab)
                {
                    case 1: // safety: right hand to the outside of the right ski
                        p.TorsoPitch = 42f; p.TorsoRoll = 12f; p.ArmFwdR = 34f; p.ArmOutR = 14f; p.ElbowR = 6f;
                        p.ArmFwdL = 10f; p.ArmOutL = 78f; p.ElbowL = 20f; break;
                    case 2: // mute: left hand across to the right ski
                        p.TorsoPitch = 44f; p.TorsoYaw = 20f; p.ArmFwdL = 52f; p.ArmOutL = 0f; p.ElbowL = 14f; p.ArmTwistL = 46f;
                        p.ArmFwdR = -10f; p.ArmOutR = 80f; p.ElbowR = 20f; break;
                    case 3: // stalefish: knees up, the right hand behind the leg to the tail
                        p.ThighL = p.ThighR = 78f; p.KneeL = p.KneeR = 118f; p.TorsoPitch = 30f; p.TorsoYaw = -14f;
                        p.ArmFwdR = -38f; p.ArmOutR = 22f; p.ElbowR = 34f; p.ArmTwistR = -30f;
                        p.ArmFwdL = 30f; p.ArmOutL = 70f; p.ElbowL = 24f; break;
                    case 4: // spread eagle
                        p.ThighL = p.ThighR = 4f; p.KneeL = p.KneeR = 8f; p.LegOut = 34f; p.TorsoPitch = 2f;
                        p.ArmFwdL = p.ArmFwdR = 8f; p.ArmOutL = p.ArmOutR = 108f; p.ElbowL = p.ElbowR = 8f; break;
                    default:
                        p.ArmOutL += 16f; p.ArmOutR += 16f; break;
                }
            }
            return p;
        }

        public void Sync(SkiSim sim, float dt)
        {
            var c = sim.Course;
            float tuck = sim.Tuck, plough = sim.Plough, lean = sim.Lean;
            bool air = !sim.Contact;
            float speed = sim.Speed;

            // --- root: stand on the snow, aligned with the slope ---------------------------
            Vector3 targetUp = air ? Vector3.Lerp(Vector3.up, _up, 0.3f) : c.Normal(sim.Pos.x, sim.Pos.z);
            _up = Vector3.Slerp(_up, targetUp, 1f - Mathf.Exp(-9f * dt)).normalized;
            Vector3 fwd = sim.Forward;
            fwd = (fwd - Vector3.Dot(fwd, _up) * _up).normalized;
            Quaternion rot = Quaternion.LookRotation(fwd, _up);
            // Flips and corks turn the whole body around its middle.
            const float Pivot = 0.9f;
            Quaternion trick = Quaternion.Euler(sim.Flip * Mathf.Rad2Deg, 0f, -sim.Cork * Mathf.Rad2Deg);
            if (sim.Crashed)
            {
                _crashSpin += dt;
                float k = Mathf.Clamp01(sim.CrashTimer / sim.P.CrashTime);
                trick = Quaternion.Euler(Mathf.Sin(_crashSpin * 9f) * 55f * k + 35f * k, Mathf.Sin(_crashSpin * 5f) * 40f * k, Mathf.Sin(_crashSpin * 7f) * 70f * k);
            }
            else _crashSpin = 0f;
            Quaternion full = rot * trick;
            transform.SetPositionAndRotation(sim.Pos + _up * Pivot - full * Vector3.up * Pivot, full);

            // A physical crash: the body falls on its own until the run stands the skier up again.
            if (_doll != null && !sim.Crashed) EndCrash();
            if (_doll != null) _doll.Follow(sim.Pos, dt);

            // --- body ------------------------------------------------------------------------
            _poleAnim = Mathf.Min(_poleAnim + dt / 0.42f, 1f);
            Pose target = TargetPose(sim, air, out float airTuck);
            _pose = Pose.Lerp(_pose, target, 1f - Mathf.Exp(-18f * dt));
            if (_doll != null) { CameraFollow(sim, dt, speed, tuck, plough, lean, air, c, full); return; }
            // The snowplough V: feet just far enough apart that the tips almost touch.
            if (!_board && _pose.FootIn > 0.01f) _pose.LegOut = Mathf.Max(_pose.LegOut, Body.LegOutForPlough(_pose));
            if (!_board && _poleAnim < 1f)
            {
                float pull = Mathf.Clamp01((_poleAnim - 0.3f) / 0.7f);
                Body.PoleAim = Vector3.Lerp(new Vector3(0f, -1f, 0.35f), new Vector3(0f, -0.62f, -0.78f), pull);   // plant ahead, push back
            }
            else Body.PoleAim = new Vector3(0f, -0.82f, -0.57f);
            Body.Snap(_pose);
            _lift = Mathf.Lerp(_lift, airTuck * 0.22f, 1f - Mathf.Exp(-12f * dt));
            // No poles on a board: the push is a little hop to the side, both feet off the snow.
            float hop = _board && _poleAnim < 1f ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(_poleAnim / 0.8f)) : 0f;
            Body.transform.localPosition = new Vector3(_hopSide * hop * 0.14f, _lift + hop * 0.2f, hop * 0.08f);
            // Riding switch: the body faces up the hill while the travel, the controls and the camera stay as they are.
            // In the air and on landing this follows the heading exactly; turning back round at a standstill takes a moment.
            float headingDeg = sim.Heading * Mathf.Rad2Deg;
            _visualYaw = Mathf.MoveTowardsAngle(_visualYaw, headingDeg + (sim.Switch ? 180f : 0f), 1100f * dt);
            float turned = Mathf.DeltaAngle(headingDeg, _visualYaw);
            Body.transform.localRotation = Quaternion.Euler(0f, turned + (_board ? -plough * 55f - _hopSide * hop * 16f : 0f), -lean * 12f * (Mathf.Abs(turned) > 90f ? -1f : 1f));

            // Just back on your feet: you blink, and nothing can knock you down for a moment.
            Body.SetVisible(sim.Shield <= 0f || Mathf.Repeat(sim.Shield * 7f, 1f) < 0.6f);

            // How every part of the body moves, for the moment a crash comes.
            _motion ??= new BoneMotion(Body);
            _motion.Sample(dt);

            CameraFollow(sim, dt, speed, tuck, plough, lean, air, c, full);
        }

        /// <summary>The camera: at the eyes, or behind and above. In a physical crash it goes with the body.</summary>
        void CameraFollow(SkiSim sim, float dt, float speed, float tuck, float plough, float lean, bool air, Course c, Quaternion full)
        {
            _third = Mathf.MoveTowards(_third, ThirdPerson ? 1f : 0f, dt * 3.2f);
            Body.SetHeadVisible(_third > 0.2f);

            _shake = Mathf.MoveTowards(_shake, 0f, dt * 2.2f);
            _shakeTime += dt * (18f + speed);
            float rumble = Mathf.Clamp01(speed / 34f) * 0.25f + sim.SkidAmount * 0.35f * Mathf.Clamp01(speed / 10f) + sim.OverLean * 0.2f;
            if (air) rumble *= 0.15f;
            float amp = (_shake + rumble * 0.035f) * Mathf.Lerp(1f, 0.35f, _third);
            Vector3 jitter = new Vector3(Mathf.PerlinNoise(_shakeTime, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, _shakeTime) - 0.5f, 0f) * amp;
            float roll = -lean * 5f + (Mathf.PerlinNoise(_shakeTime * 0.7f, 5f) - 0.5f) * amp * 60f;

            // First person: at the eyes, turning with the body.
            Vector3 fpPos = Body.EyeWorld + full * jitter;
            Quaternion fpRot = full * Quaternion.Euler(tuck * 6f + plough * 3f, 0f, roll) * Quaternion.Euler(-Look.y, Look.x, 0f);
            if (_doll != null)
            {
                // Down in a crash: the view goes with the falling head, steadied, and keeps some of the horizon.
                Quaternion head = Body.Head.rotation * Quaternion.Euler(-Look.y, Look.x, 0f);
                _crashCam = Quaternion.Slerp(_crashCam, head, 1f - Mathf.Exp(-7f * dt));
                Vector3 f = _crashCam * Vector3.forward; f.y = 0f;
                Quaternion level = f.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(f) : _crashCam;
                fpRot = Quaternion.Slerp(_crashCam, level, 0.35f);
            }

            // Third person: behind and above, following the direction of travel, not the spins.
            Vector3 flat = speed > 2.5f ? sim.Vel : sim.Forward;
            flat.y = 0f;
            if (flat.sqrMagnitude > 1e-4f) _heading = Vector3.Slerp(_heading, flat.normalized, 1f - Mathf.Exp(-4f * dt)).normalized;
            Quaternion orbit = Quaternion.LookRotation(_heading) * Quaternion.Euler(Mathf.Clamp(15f - Look.y * 0.6f, -8f, 62f), Look.x, 0f);
            Vector3 focus = (_doll != null ? Vector3.Lerp(sim.Pos, _doll.Centre, 0.7f) : sim.Pos) + Vector3.up * 1.2f;
            Vector3 want = focus - orbit * Vector3.forward * (4.7f + speed * 0.05f);
            want.y = Mathf.Max(want.y, c.Height(want.x, want.z) + 0.8f);
            if (!_tpInit) { _tpPos = want; _tpInit = true; }
            _tpPos = Vector3.Lerp(_tpPos, want, 1f - Mathf.Exp(-9f * dt));
            Quaternion tpRot = Quaternion.LookRotation(focus + Vector3.up * 0.35f - _tpPos) * Quaternion.Euler(0f, 0f, roll * 0.4f);

            float e = Mathf.SmoothStep(0f, 1f, _third);
            Cam.transform.SetPositionAndRotation(Vector3.Lerp(fpPos, _tpPos + jitter, e), Quaternion.Slerp(fpRot, tpRot, e));
            float targetFov = Mathf.Lerp(BaseFov, 62f, e) + Mathf.Clamp01(speed / 36f) * Mathf.Lerp(16f, 9f, e) + tuck * 3f;
            _fov = Mathf.Lerp(_fov, targetFov, 1f - Mathf.Exp(-4f * dt));
            Cam.fieldOfView = _fov;
        }
    }
}
