using Ski.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ski.Game
{
    /// <summary>
    /// The first-person body: camera, arms, gloves, poles, legs, boots and skis (or a board).
    /// Everything is posed procedurally from the simulation state each frame, so looking down
    /// shows the snowplough V, the tuck, pole pushes, edging and grabs.
    /// </summary>
    public sealed class PlayerRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        /// <summary>Mouse look in degrees: x = yaw (right positive), y = pitch (up positive).</summary>
        public Vector2 Look;
        public float BaseFov = 74f;

        CharacterStyle _style;
        bool _board;
        Transform _camPivot;
        Transform _skiL, _skiR, _bootL, _bootR, _torso, _poleL, _poleR;
        Transform[] _limbs;   // 0-1 left leg, 2-3 right leg, 4-5 left arm, 6-7 right arm
        Transform[] _joints;  // knees, elbows, hands
        Vector3 _up = Vector3.up;
        float _poleAnim = 1f, _shake, _shakeTime, _fov, _crashSpin;
        Vector3 _handL, _handR;
        static Mesh _tube, _ball;

        const float Thigh = 0.44f, Shin = 0.44f, UpperArm = 0.3f, Forearm = 0.3f;

        public static PlayerRig Create(CharacterId who, Transform parent)
        {
            var go = new GameObject("Player " + SkierInfo.Get(who).Name);
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<PlayerRig>();
            rig.Build(who);
            return rig;
        }

        void Build(CharacterId who)
        {
            _style = CharacterStyle.Get(who);
            var info = SkierInfo.Get(who);
            _board = info.Gear == Gear.Snowboard;
            EnsureUnitMeshes();

            // Camera.
            _camPivot = new GameObject("Head").transform;
            _camPivot.SetParent(transform, false);
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(_camPivot, false);
            camGo.tag = "MainCamera";
            Cam = camGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.06f; Cam.farClipPlane = 2500f; Cam.fieldOfView = _fov = BaseFov;
            Cam.clearFlags = CameraClearFlags.Skybox;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGo.AddComponent<AudioListener>();

            // Gear.
            var b = new MeshBuilder();
            if (_board)
            {
                GearModels.Board(b, _style.SkiLength, _style.Gear, _style.GearAccent);
                _skiL = b.ToObject("Board", Mats.Props, transform).transform;
                _skiR = null;
            }
            else
            {
                bool twin = info.Gear == Gear.TwinTips || info.Gear == Gear.Blades;
                GearModels.Ski(b, _style.SkiLength, twin, _style.Gear, _style.GearAccent);
                _skiL = b.ToObject("Ski L", Mats.Props, transform).transform;
                _skiR = Instantiate(_skiL.gameObject, transform).transform;
                _skiR.name = "Ski R";
                b.Clear();
                GearModels.Pole(b, 1.15f * _style.Height, _style.GearAccent);
                _poleL = b.ToObject("Pole L", Mats.Props, transform).transform;
                _poleR = Instantiate(_poleL.gameObject, transform).transform;
            }
            b.Clear();
            GearModels.Boot(b, _style.Boots);
            _bootL = b.ToObject("Boot L", Mats.Props, transform).transform;
            _bootR = Instantiate(_bootL.gameObject, transform).transform;

            // Body parts: stretched unit tubes and balls.
            var pants = Mats.Colour(_style.Pants, 1.6f);
            var jacket = Mats.Colour(_style.Jacket, 1.6f);
            var gloves = Mats.Colour(_style.Gloves, 1.6f);
            _limbs = new Transform[8];
            for (int i = 0; i < 8; i++) _limbs[i] = Part("Limb" + i, _tube, i < 4 ? pants : jacket);
            _joints = new Transform[6];
            for (int i = 0; i < 6; i++) _joints[i] = Part("Joint" + i, _ball, i < 2 ? pants : i < 4 ? jacket : gloves);
            _torso = Part("Torso", _ball, jacket);
        }

        Transform Part(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        static void EnsureUnitMeshes()
        {
            if (_tube != null) return;
            var b = new MeshBuilder();
            b.Push(Vector3.zero, Quaternion.Euler(90, 0, 0)); // +Y -> +Z
            b.Cylinder(Vector3.zero, 1f, 1f, 1f, 10, true, false);
            b.Pop();
            _tube = b.Build("UnitTube");
            b.Clear();
            b.Sphere(Vector3.zero, 1f, 12, 8);
            _ball = b.Build("UnitBall");
        }

        /// <summary>Adds a camera shake impulse (landing, crash, bump).</summary>
        public void Kick(float amount) { _shake = Mathf.Max(_shake, amount); }
        public void PolePush() { _poleAnim = 0f; }

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

            // --- body pose (local space: y up from the snow, z forward) ---------------------
            float h = _style.Height;
            float crouch = Mathf.Max(tuck, plough * 0.25f);
            float airTuck = air ? Mathf.Clamp01(sim.AirTime * 4f) * 0.5f : 0f;
            if (sim.ActiveGrab != 0) airTuck = 1f;
            float hipY = Mathf.Lerp(0.9f, 0.56f, crouch) * h;
            float hipZ = Mathf.Lerp(-0.02f, -0.16f, crouch);
            float lift = airTuck * 0.26f;                       // knees come up in the air
            float eyeY = sim.P.EyeHeight * h * Mathf.Lerp(1f, 0.64f, crouch) - (sim.Crashed ? 0.7f : 0f);
            float eyeZ = 0.1f + crouch * 0.26f;
            Vector3 chest = new Vector3(lean * 0.06f, eyeY - 0.3f, Mathf.Lerp(hipZ + 0.02f, eyeZ - 0.24f, 0.8f));
            Vector3 hip = new Vector3(0f, hipY, hipZ);

            // Feet and gear.
            float spread = sim.ActiveGrab == 4 ? 0.42f : 0f;
            float stance = 0.13f + plough * 0.1f + spread;
            Quaternion edge = Quaternion.Euler(0f, 0f, -lean * 24f);
            Vector3 footL, footR;
            if (_board)
            {
                Quaternion skid = Quaternion.Euler(0f, -plough * 62f - 8f, 0f);
                _skiL.localPosition = new Vector3(0f, lift, 0f);
                _skiL.localRotation = skid * edge;
                footL = _skiL.localPosition + skid * new Vector3(0f, 0.03f, 0.26f);
                footR = _skiL.localPosition + skid * new Vector3(0f, 0.03f, -0.26f);
                Quaternion bootRot = skid * Quaternion.Euler(0f, -78f, 0f);
                _bootL.localPosition = footL; _bootL.localRotation = bootRot;
                _bootR.localPosition = footR; _bootR.localRotation = bootRot;
            }
            else
            {
                // Snowplough: tips together, tails apart.
                float v = plough * 17f;
                float grabTilt = sim.ActiveGrab == 1 || sim.ActiveGrab == 2 ? 18f : 0f;
                Quaternion air1 = Quaternion.Euler(-airTuck * 12f - grabTilt, 0f, 0f);
                footL = new Vector3(-stance, lift, lean * 0.05f);
                footR = new Vector3(stance, lift, -lean * 0.05f);
                _skiL.localPosition = footL; _skiL.localRotation = Quaternion.Euler(0f, v, 0f) * edge * air1;
                _skiR.localPosition = footR; _skiR.localRotation = Quaternion.Euler(0f, -v, 0f) * edge * air1;
                _bootL.localPosition = footL + Vector3.up * 0.045f; _bootL.localRotation = _skiL.localRotation;
                _bootR.localPosition = footR + Vector3.up * 0.045f; _bootR.localRotation = _skiR.localRotation;
            }

            // Legs.
            Vector3 hipL = hip + new Vector3(-0.11f, 0, 0), hipR = hip + new Vector3(0.11f, 0, 0);
            Vector3 ankleL = _bootL.localPosition + Vector3.up * 0.3f, ankleR = _bootR.localPosition + Vector3.up * 0.3f;
            Vector3 kneeHint = new Vector3(0f, 0.2f, 1f);
            Vector3 kneeL = Bend(hipL, ankleL, Thigh * h, Shin * h, kneeHint + Vector3.left * 0.15f);
            Vector3 kneeR = Bend(hipR, ankleR, Thigh * h, Shin * h, kneeHint + Vector3.right * 0.15f);
            Limb(0, hipL, kneeL, 0.085f); Limb(1, kneeL, ankleL, 0.07f);
            Limb(2, hipR, kneeR, 0.085f); Limb(3, kneeR, ankleR, 0.07f);
            Joint(0, kneeL, 0.085f); Joint(1, kneeR, 0.085f);

            // Torso: one squashed ball between hips and chest.
            Vector3 mid = (hip + chest) * 0.5f;
            _torso.localPosition = mid;
            _torso.localRotation = Quaternion.FromToRotation(Vector3.up, (chest - hip).normalized);
            _torso.localScale = new Vector3(0.2f, (chest - hip).magnitude * 0.62f + 0.08f, 0.15f);

            // Hands.
            _poleAnim = Mathf.Min(_poleAnim + dt / 0.42f, 1f);
            Vector3 shL = chest + new Vector3(-0.19f, 0.04f, 0f), shR = chest + new Vector3(0.19f, 0.04f, 0f);
            Vector3 baseL = new Vector3(-0.29f, hipY + 0.1f, hipZ + 0.44f), baseR = new Vector3(0.29f, hipY + 0.1f, hipZ + 0.44f);
            Vector3 tuckL = new Vector3(-0.075f, eyeY - 0.4f, eyeZ + 0.36f), tuckR = new Vector3(0.075f, eyeY - 0.4f, eyeZ + 0.36f);
            Vector3 tl = Vector3.Lerp(baseL, tuckL, tuck), tr = Vector3.Lerp(baseR, tuckR, tuck);
            // Leaning into a turn: inside hand drops toward the snow, outside hand comes up.
            tl += new Vector3(0f, lean * 0.12f, 0f) * (1f - tuck); tr += new Vector3(0f, -lean * 0.12f, 0f) * (1f - tuck);
            tl += new Vector3(-plough * 0.1f, 0f, 0.05f * plough); tr += new Vector3(plough * 0.1f, 0f, 0.05f * plough);
            Vector3 poleDirL = Vector3.Lerp(new Vector3(-0.1f, -0.78f, -0.6f), new Vector3(-0.22f, 0.08f, -1f), tuck).normalized;
            Vector3 poleDirR = new Vector3(-poleDirL.x, poleDirL.y, poleDirL.z);
            if (_poleAnim < 1f)
            {
                // Reach forward, plant, pull back.
                float p = _poleAnim;
                float reach = p < 0.3f ? p / 0.3f : 1f - (p - 0.3f) / 0.7f;
                float pull = Mathf.Clamp01((p - 0.3f) / 0.7f);
                Vector3 off = new Vector3(0f, 0.1f * reach - 0.12f * pull, 0.3f * reach - 0.32f * pull);
                tl += off; tr += off;
                Vector3 plant = Vector3.Lerp(new Vector3(0f, -1f, 0.35f), new Vector3(0f, -0.62f, -0.78f), pull).normalized;
                poleDirL = poleDirR = plant;
            }
            if (air)
            {
                switch (sim.ActiveGrab)
                {
                    case 1: tr = footR + new Vector3(0.1f, 0.12f, 0.15f); tl += new Vector3(-0.25f, 0.2f, -0.1f); break;       // safety: outside of the ski
                    case 2: tl = footR + new Vector3(-0.02f, 0.14f, 0.35f); tr += new Vector3(0.3f, 0.25f, -0.15f); break;     // mute: across to the other ski
                    case 4: tl = shL + new Vector3(-0.5f, 0.12f, 0.15f); tr = shR + new Vector3(0.5f, 0.12f, 0.15f); break;    // spread eagle
                    default: tl += new Vector3(-0.12f, 0.1f, 0f); tr += new Vector3(0.12f, 0.1f, 0f); break;
                }
            }
            float hs = 1f - Mathf.Exp(-16f * dt);
            _handL = Vector3.Lerp(_handL, tl, hs); _handR = Vector3.Lerp(_handR, tr, hs);
            Vector3 elbowHint = new Vector3(0f, -1f, -0.35f);
            Vector3 elL = Bend(shL, _handL, UpperArm * h, Forearm * h, elbowHint + Vector3.left * 0.7f);
            Vector3 elR = Bend(shR, _handR, UpperArm * h, Forearm * h, elbowHint + Vector3.right * 0.7f);
            Limb(4, shL, elL, 0.058f); Limb(5, elL, _handL, 0.05f);
            Limb(6, shR, elR, 0.058f); Limb(7, elR, _handR, 0.05f);
            Joint(2, elL, 0.058f); Joint(3, elR, 0.058f);
            Joint(4, _handL, 0.06f); Joint(5, _handR, 0.06f);
            if (_poleL != null)
            {
                _poleL.localPosition = _handL; _poleL.localRotation = Quaternion.FromToRotation(Vector3.down, poleDirL);
                _poleR.localPosition = _handR; _poleR.localRotation = Quaternion.FromToRotation(Vector3.down, poleDirR);
            }

            // --- camera ---------------------------------------------------------------------
            _shake = Mathf.MoveTowards(_shake, 0f, dt * 2.2f);
            _shakeTime += dt * (18f + speed);
            float rumble = Mathf.Clamp01(speed / 34f) * 0.25f + sim.SkidAmount * 0.35f * Mathf.Clamp01(speed / 10f) + sim.OverLean * 0.2f;
            if (air) rumble *= 0.15f;
            float amp = _shake + rumble * 0.035f;
            Vector3 jitter = new Vector3(Mathf.PerlinNoise(_shakeTime, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, _shakeTime) - 0.5f, 0f) * amp;
            _camPivot.localPosition = new Vector3(lean * 0.1f, eyeY, eyeZ) + jitter;
            float roll = -lean * 5f + (Mathf.PerlinNoise(_shakeTime * 0.7f, 5f) - 0.5f) * amp * 60f;
            float pitchDown = tuck * 6f + plough * 3f;
            _camPivot.localRotation = Quaternion.Euler(pitchDown, 0f, roll) * Quaternion.Euler(-Look.y, Look.x, 0f);
            float targetFov = BaseFov + Mathf.Clamp01(speed / 36f) * 16f + tuck * 3f;
            _fov = Mathf.Lerp(_fov, targetFov, 1f - Mathf.Exp(-4f * dt));
            Cam.fieldOfView = _fov;
        }

        /// <summary>Middle joint of a two-bone chain from a to b, bending toward the hint.</summary>
        static Vector3 Bend(Vector3 a, Vector3 b, float l1, float l2, Vector3 hint)
        {
            Vector3 d = b - a;
            float dist = Mathf.Clamp(d.magnitude, 0.02f, l1 + l2 - 0.005f);
            Vector3 dir = d.normalized;
            float along = (l1 * l1 - l2 * l2 + dist * dist) / (2f * dist);
            float side = Mathf.Sqrt(Mathf.Max(l1 * l1 - along * along, 0f));
            Vector3 perp = (hint - Vector3.Dot(hint, dir) * dir).normalized;
            return a + dir * along + perp * side;
        }

        void Limb(int i, Vector3 a, Vector3 b, float radius)
        {
            var t = _limbs[i];
            Vector3 d = b - a;
            float len = d.magnitude;
            t.localPosition = a;
            if (len > 1e-4f) t.localRotation = Quaternion.LookRotation(d / len, Mathf.Abs(d.y / len) > 0.98f ? Vector3.forward : Vector3.up);
            t.localScale = new Vector3(radius, radius, len);
        }

        void Joint(int i, Vector3 p, float radius)
        {
            _joints[i].localPosition = p;
            _joints[i].localScale = Vector3.one * radius;
        }
    }
}
