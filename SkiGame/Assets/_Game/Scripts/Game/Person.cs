using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    public enum GearKind { None, Skis, Board }

    /// <summary>What a person looks like: size, clothes and gear.</summary>
    public sealed class PersonLook
    {
        public float Height = 1.75f;
        public bool Kid;
        public bool Hero;            // separate face parts that animate (blink, talk, brows)
        public Color Jacket, Trim, Pants, Gloves, Boots, Helmet, Goggles, Hair, Skin;
        public bool HasHelmet = true, GogglesDown = true, LongHair, Beanie;
        public GearKind Gear = GearKind.Skis;
        public float GearLength = 1.6f;
        public bool TwinTip, Poles = true;
        public Color GearColour, GearAccent;

        public static PersonLook For(CharacterId id)
        {
            var st = CharacterStyle.Get(id);
            var info = SkierInfo.Get(id);
            return new PersonLook
            {
                Height = 1.75f * st.Height, Hero = true,
                Jacket = st.Jacket, Trim = st.JacketTrim, Pants = st.Pants, Gloves = st.Gloves, Boots = st.Boots,
                Helmet = st.Helmet, Goggles = st.Goggles, Hair = st.Hair, Skin = st.Skin,
                LongHair = st.LongHair, GogglesDown = false,
                Gear = info.Gear == Ski.Core.Gear.Snowboard ? GearKind.Board : GearKind.Skis,
                GearLength = st.SkiLength, TwinTip = info.Gear == Ski.Core.Gear.TwinTips || info.Gear == Ski.Core.Gear.Blades,
                Poles = info.Gear != Ski.Core.Gear.Snowboard,
                GearColour = st.Gear, GearAccent = st.GearAccent,
            };
        }

        public static PersonLook Random(Rng rng, bool kid, float size = 1f)
        {
            Color jacket = Palette.Jackets[rng.Range(0, Palette.Jackets.Length)];
            Color other = Palette.Jackets[rng.Range(0, Palette.Jackets.Length)];
            return new PersonLook
            {
                Height = (kid ? 1.75f * size : rng.Range(1.62f, 1.9f)), Kid = kid,
                Jacket = jacket, Trim = Palette.Mul(jacket, 0.7f), Pants = rng.Chance(0.6f) ? Palette.Pants[rng.Range(0, Palette.Pants.Length)] : other,
                Gloves = Palette.Ink, Boots = Palette.Pants[rng.Range(0, Palette.Pants.Length)], Helmet = rng.Chance(0.5f) ? other : Palette.White,
                Goggles = Palette.Jackets[rng.Range(0, Palette.Jackets.Length)], Hair = Palette.Bark,
                Skin = rng.Chance(0.75f) ? Palette.Skin : Palette.SkinDark,
                LongHair = rng.Chance(0.35f), GearLength = kid ? 0.95f * size / 0.6f * 0.75f : rng.Range(1.5f, 1.75f),
                Poles = !kid, GearColour = other, GearAccent = Palette.White,
            };
        }
    }

    /// <summary>
    /// A posable skier made of rigid parts on a joint hierarchy. Poses are joint angles
    /// (<see cref="Pose"/>); the person breathes, blinks and can talk. Faces +Z; the root is at the
    /// feet (or at the seat for seated poses).
    /// </summary>
    public sealed class Person : MonoBehaviour
    {
        public PersonLook Look { get; private set; }
        public Pose Target = Pose.Stand;
        public float PoseSpeed = 6f;
        public bool Talking;
        public float BrowRaise;        // -1 = frown, +1 = surprised
        public float MouthOpen;        // 0..1 base openness (shouting kids)
        public Vector2 LookOffset;     // extra head yaw / pitch in degrees
        public bool Alive = true;      // breathing, blinking, small head movement
        /// <summary>Poles point at the ground whatever the arms do (sitting on the lift, standing about).</summary>
        public bool PolesHang;
        readonly System.Collections.Generic.List<Transform> _poles = new System.Collections.Generic.List<Transform>();
        public Transform HandL => _handL;
        public Transform HandR => _handR;
        public Transform Head => _head;
        public Transform GogglesPivot => _goggles;

        Pose _pose = Pose.Stand;
        float _s, _thigh, _shin, _ankle, _torsoLen, _upperArm, _forearm, _hr;
        Transform _hips, _torso, _head, _shL, _elL, _handL, _shR, _elR, _handR, _hipL, _kneeL, _footL, _hipR, _kneeR, _footR;
        Transform _eyeL, _eyeR, _browL, _browR, _mouth, _goggles;
        float _time, _blink, _nextBlink = 2f, _seed, _goggleUp, _goggleUpTarget;
        Vector2 _wander, _wanderTarget; float _nextWander;
        static int _count;

        public static Person Create(PersonLook look, Transform parent, string name = "Person")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<Person>();
            p.Build(look);
            return p;
        }

        /// <summary>Sets the pose at once, without blending.</summary>
        public void Snap(in Pose pose) { Target = pose; _pose = pose; Apply(); }
        /// <summary>Goggles on the helmet (1) or over the eyes (0).</summary>
        public void SetGogglesUp(bool up, bool instant = false) { _goggleUpTarget = up ? 1f : 0f; if (instant) { _goggleUp = _goggleUpTarget; PlaceGoggles(); } }

        static Transform Node(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        static void Skin(MeshBuilder b, Transform t, string name)
        {
            if (b.VertexCount == 0) return;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = b.Build(name);
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.Props;
            b.Clear();
        }

        void Build(PersonLook look)
        {
            Look = look;
            float s = _s = look.Height / 1.75f;
            _ankle = 0.1f * s; _shin = 0.41f * s; _thigh = 0.42f * s; _torsoLen = 0.5f * s;
            _upperArm = 0.28f * s; _forearm = 0.26f * s;
            _hr = look.Kid ? Mathf.Lerp(0.118f * s, 0.125f, 0.75f) : 0.118f * s * (look.Hero ? 1.2f : 1f);
            _seed = (++_count & 1023) * 0.37f;
            var b = new MeshBuilder();
            int sides = look.Hero ? 12 : 7;

            _hips = Node("Hips", transform, Vector3.zero);
            b.Col = look.Pants;
            b.Sphere(new Vector3(0, -0.03f * s, 0), new Vector3(0.17f, 0.12f, 0.135f) * s, sides, 5);
            Skin(b, _hips, "Pelvis");

            // --- torso: a puffy jacket ---------------------------------------------------------
            _torso = Node("Torso", _hips, new Vector3(0, 0.02f * s, 0));
            b.Push(Vector3.zero, Quaternion.identity, new Vector3(1f, 1f, 0.76f));
            b.Col = look.Jacket;
            b.Lathe(Vector3.zero, new[] { new Vector2(0.168f, 0.03f) * s, new Vector2(0.19f, 0.12f) * s, new Vector2(0.196f, 0.26f) * s, new Vector2(0.205f, 0.37f) * s, new Vector2(0.16f, 0.465f) * s, new Vector2(0.085f, 0.5f) * s }, sides, true, false);
            b.Col = look.Trim;
            b.Lathe(Vector3.zero, new[] { new Vector2(0.175f, -0.03f) * s, new Vector2(0.186f, 0.0f) * s, new Vector2(0.186f, 0.05f) * s, new Vector2(0.17f, 0.06f) * s }, sides, false, true);
            b.Lathe(Vector3.zero, new[] { new Vector2(0.092f, 0.47f) * s, new Vector2(0.1f, 0.52f) * s, new Vector2(0.085f, 0.55f) * s }, sides, false, false);
            b.Pop();
            b.Col = Palette.Ink;
            b.Box(new Vector3(0, 0.26f * s, 0.15f * s), new Vector3(0.016f, 0.4f, 0.012f) * s); // zip
            if (look.Hero)
            {
                // Chest pocket and a lift pass on the sleeve side.
                b.Col = look.Trim;
                b.Box(new Vector3(-0.09f * s, 0.34f * s, 0.146f * s), new Vector3(0.075f, 0.055f, 0.012f) * s);
                b.Col = Palette.White;
                b.Box(new Vector3(0.11f * s, 0.2f * s, 0.146f * s), new Vector3(0.045f, 0.06f, 0.008f) * s);
            }
            Skin(b, _torso, "Jacket");

            // --- head -------------------------------------------------------------------------
            float hr = _hr;
            _head = Node("Head", _torso, new Vector3(0, _torsoLen + 0.035f * s, 0));
            b.Col = look.Skin;
            b.Sphere(new Vector3(0, hr * 0.92f, 0), new Vector3(hr * 0.9f, hr, hr * 0.94f), look.Hero ? 14 : 8, look.Hero ? 10 : 6);
            b.Cylinder(new Vector3(0, -0.04f * s, 0), 0.05f * s, 0.05f * s, 0.07f * s, 6, true, false); // neck
            b.TaperedBox(new Vector3(0, hr * 0.56f, hr * 0.88f), new Vector3(hr * 0.17f, 0, hr * 0.2f), new Vector2(0.5f, 0.4f), hr * 0.26f); // nose
            if (!look.Hero) Face(b, hr, Vector3.zero, true, true, true);
            if (look.HasHelmet)
            {
                b.Col = look.Helmet;
                var prof = new Vector2[7];
                for (int i = 0; i < 7; i++)
                {
                    float a = Mathf.Lerp(0.14f, Mathf.PI * 0.5f, i / 6f);
                    prof[i] = new Vector2(Mathf.Cos(a) * hr * 1.12f, hr * 1.02f + Mathf.Sin(a) * hr * 1.08f);
                }
                b.Lathe(Vector3.zero, prof, look.Hero ? 14 : 8, false, true);
                // Ear pads and a back shell.
                b.Sphere(new Vector3(-hr * 0.93f, hr * 0.75f, -hr * 0.05f), new Vector3(hr * 0.26f, hr * 0.42f, hr * 0.38f), 7, 4);
                b.Sphere(new Vector3(hr * 0.93f, hr * 0.75f, -hr * 0.05f), new Vector3(hr * 0.26f, hr * 0.42f, hr * 0.38f), 7, 4);
                b.Sphere(new Vector3(0, hr * 0.8f, -hr * 0.45f), new Vector3(hr * 0.98f, hr * 0.62f, hr * 0.7f), 8, 4);
                b.Col = Palette.Ink;
                b.Box(new Vector3(0, hr * 0.12f, hr * 0.3f), new Vector3(hr * 1.5f, hr * 0.07f, hr * 0.07f)); // chin strap
            }
            else if (look.Beanie)
            {
                b.Col = look.Helmet;
                b.Lathe(Vector3.zero, new[] { new Vector2(hr * 1.0f, hr * 1.05f), new Vector2(hr * 1.02f, hr * 1.35f), new Vector2(hr * 0.8f, hr * 1.8f), new Vector2(hr * 0.3f, hr * 2.05f) }, 10, true, false);
                b.Col = look.Trim;
                b.Lathe(Vector3.zero, new[] { new Vector2(hr * 1.06f, hr * 1.0f), new Vector2(hr * 1.08f, hr * 1.25f) }, 10, false, false);
                b.Sphere(new Vector3(0, hr * 2.12f, 0), hr * 0.22f, 6, 4);
            }
            else
            {
                b.Col = look.Hair;
                b.Sphere(new Vector3(0, hr * 1.12f, -hr * 0.1f), new Vector3(hr * 0.96f, hr * 0.92f, hr * 0.98f), 10, 6);
            }
            if (look.LongHair)
            {
                b.Col = look.Hair;
                b.Sphere(new Vector3(0, hr * 0.15f, -hr * 0.72f), new Vector3(hr * 0.72f, hr * 0.95f, hr * 0.32f), 8, 5);
                b.Sphere(new Vector3(-hr * 0.78f, hr * 0.3f, hr * 0.25f), new Vector3(hr * 0.2f, hr * 0.6f, hr * 0.22f), 6, 4);
                b.Sphere(new Vector3(hr * 0.78f, hr * 0.3f, hr * 0.25f), new Vector3(hr * 0.2f, hr * 0.6f, hr * 0.22f), 6, 4);
            }
            Skin(b, _head, "Head");

            if (look.Hero)
            {
                _eyeL = Node("EyeL", _head, new Vector3(-hr * 0.36f, hr * 0.84f, hr * 0.8f));
                Eye(b, hr); Skin(b, _eyeL, "Eye");
                _eyeR = Node("EyeR", _head, new Vector3(hr * 0.36f, hr * 0.84f, hr * 0.8f));
                Eye(b, hr); Skin(b, _eyeR, "Eye");
                _browL = Node("BrowL", _head, new Vector3(-hr * 0.38f, hr * 1.16f, hr * 0.88f));
                b.Col = Palette.Ink; b.Box(Vector3.zero, new Vector3(hr * 0.4f, hr * 0.09f, hr * 0.06f)); Skin(b, _browL, "Brow");
                _browR = Node("BrowR", _head, new Vector3(hr * 0.38f, hr * 1.16f, hr * 0.88f));
                b.Col = Palette.Ink; b.Box(Vector3.zero, new Vector3(hr * 0.4f, hr * 0.09f, hr * 0.06f)); Skin(b, _browR, "Brow");
                _mouth = Node("Mouth", _head, new Vector3(0, hr * 0.34f, hr * 0.84f));
                b.Col = Palette.Ink; b.Sphere(Vector3.zero, new Vector3(hr * 0.26f, hr * 0.13f, hr * 0.08f), 8, 5); Skin(b, _mouth, "Mouth");
            }

            // Goggles: a lens and a strap that can sit over the eyes or up on the helmet.
            _goggles = Node("Goggles", _head, Vector3.zero);
            b.Col = Palette.Ink;
            b.Sphere(Vector3.zero, new Vector3(hr * 0.74f, hr * 0.34f, hr * 0.2f), 10, 5);
            b.Col = look.Goggles;
            b.Sphere(new Vector3(0, 0, hr * 0.06f), new Vector3(hr * 0.66f, hr * 0.27f, hr * 0.17f), 10, 5);
            b.Col = Palette.Ink;
            b.Lathe(new Vector3(0, -hr * 0.1f, -hr * 0.92f), new[] { new Vector2(hr * 1.14f, 0f), new Vector2(hr * 1.14f, hr * 0.2f) }, look.Hero ? 14 : 8, false, false);
            Skin(b, _goggles, "Goggles");
            _goggleUp = _goggleUpTarget = look.GogglesDown ? 0f : 1f;
            PlaceGoggles();

            // --- arms -------------------------------------------------------------------------
            for (int side = -1; side <= 1; side += 2)
            {
                var sh = Node(side < 0 ? "ShoulderL" : "ShoulderR", _torso, new Vector3(side * 0.205f * s, 0.43f * s, 0));
                b.Col = look.Jacket;
                b.Capsule(Vector3.zero, new Vector3(0, -_upperArm, 0), 0.072f * s, 0.06f * s, sides);
                Skin(b, sh, "UpperArm");
                var el = Node(side < 0 ? "ElbowL" : "ElbowR", sh, new Vector3(0, -_upperArm, 0));
                b.Col = look.Jacket;
                b.Capsule(Vector3.zero, new Vector3(0, -_forearm, 0), 0.06f * s, 0.05f * s, sides);
                b.Col = look.Trim;
                b.Cylinder(new Vector3(0, -_forearm - 0.01f * s, 0), 0.056f * s, 0.056f * s, 0.045f * s, sides, true, false);
                Skin(b, el, "Forearm");
                var hand = Node(side < 0 ? "HandL" : "HandR", el, new Vector3(0, -_forearm, 0));
                b.Col = look.Gloves;
                b.Sphere(new Vector3(0, -0.055f * s, 0.005f * s), new Vector3(0.05f, 0.068f, 0.056f) * s, 8, 5);
                b.Sphere(new Vector3(-side * 0.04f * s, -0.035f * s, 0.03f * s), 0.026f * s, 6, 4); // thumb
                Skin(b, hand, "Glove");
                if (side < 0) { _shL = sh; _elL = el; _handL = hand; } else { _shR = sh; _elR = el; _handR = hand; }
                if (look.Poles && look.Gear == GearKind.Skis)
                {
                    var pole = Node("Pole", hand, new Vector3(0, -0.06f * s, 0.02f * s));
                    pole.localRotation = Quaternion.Euler(-62f, 0f, 0f);
                    b.Push(Vector3.zero, Quaternion.identity);
                    GearModels.Pole(b, 1.12f * s, look.GearAccent);
                    b.Pop();
                    Skin(b, pole, "Pole");
                    _poles.Add(pole);
                }
            }

            // --- legs -------------------------------------------------------------------------
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = Node(side < 0 ? "HipL" : "HipR", _hips, new Vector3(side * 0.095f * s, -0.04f * s, 0));
                b.Col = look.Pants;
                b.Capsule(Vector3.zero, new Vector3(0, -_thigh, 0), 0.09f * s, 0.075f * s, sides);
                Skin(b, hip, "Thigh");
                var knee = Node(side < 0 ? "KneeL" : "KneeR", hip, new Vector3(0, -_thigh, 0));
                b.Col = look.Pants;
                b.Capsule(Vector3.zero, new Vector3(0, -_shin + 0.05f * s, 0), 0.075f * s, 0.068f * s, sides);
                Skin(b, knee, "Shin");
                var foot = Node(side < 0 ? "FootL" : "FootR", knee, new Vector3(0, -_shin, 0));
                b.Push(new Vector3(0, -_ankle, 0), Quaternion.identity, Vector3.one * s);
                GearModels.Boot(b, look.Boots);
                b.Pop();
                if (look.Gear == GearKind.Skis)
                {
                    b.Push(new Vector3(0, -_ankle - 0.022f, 0));
                    GearModels.Ski(b, look.GearLength, look.TwinTip, look.GearColour, look.GearAccent, 0.085f * Mathf.Lerp(s, 1f, 0.5f));
                    b.Pop();
                }
                Skin(b, foot, "Foot");
                if (side < 0) { _hipL = hip; _kneeL = knee; _footL = foot; } else { _hipR = hip; _kneeR = knee; _footR = foot; }
            }
            if (look.Gear == GearKind.Board)
            {
                // The board hangs from the left foot, across the body.
                var board = Node("Board", _footL, new Vector3(0.095f * s, -_ankle - 0.025f, 0));
                board.localRotation = Quaternion.Euler(0, 90f, 0);
                GearModels.Board(b, look.GearLength, look.GearColour, look.GearAccent);
                Skin(b, board, "Board");
            }
            Apply();
        }

        void Eye(MeshBuilder b, float hr)
        {
            b.Col = Palette.White;
            b.Sphere(Vector3.zero, new Vector3(hr * 0.25f, hr * 0.29f, hr * 0.1f), 8, 5);
            b.Col = Palette.Ink;
            b.Sphere(new Vector3(0, -hr * 0.02f, hr * 0.07f), new Vector3(hr * 0.13f, hr * 0.17f, hr * 0.06f), 6, 4);
            b.Col = Palette.White;
            b.Sphere(new Vector3(hr * 0.05f, hr * 0.06f, hr * 0.11f), hr * 0.04f, 4, 3);
        }

        /// <summary>A simple face baked straight into the head mesh, for people seen from a distance.</summary>
        void Face(MeshBuilder b, float hr, Vector3 o, bool eyes, bool brows, bool mouth)
        {
            b.Col = Palette.Ink;
            if (eyes)
            {
                b.Sphere(o + new Vector3(-hr * 0.36f, hr * 0.84f, hr * 0.84f), new Vector3(hr * 0.1f, hr * 0.14f, hr * 0.07f), 5, 3);
                b.Sphere(o + new Vector3(hr * 0.36f, hr * 0.84f, hr * 0.84f), new Vector3(hr * 0.1f, hr * 0.14f, hr * 0.07f), 5, 3);
            }
            if (mouth) b.Sphere(o + new Vector3(0, hr * 0.34f, hr * 0.84f), new Vector3(hr * 0.18f, hr * 0.1f, hr * 0.07f), 5, 3);
        }

        void PlaceGoggles()
        {
            float hr = _hr, k = _goggleUp;
            _goggles.localPosition = Vector3.Lerp(new Vector3(0, hr * 0.86f, hr * 0.9f), new Vector3(0, hr * 1.66f, hr * 0.7f), k);
            _goggles.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -32f, k), 0f, 0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _time += dt;
            _pose = Pose.Lerp(_pose, Target, 1f - Mathf.Exp(-PoseSpeed * dt));
            if (Mathf.Abs(_goggleUp - _goggleUpTarget) > 0.001f)
            {
                _goggleUp = Mathf.MoveTowards(_goggleUp, _goggleUpTarget, dt * 2.2f);
                PlaceGoggles();
            }
            if (Alive)
            {
                if (_time > _nextWander)
                {
                    _nextWander = _time + 1.5f + Mathf.PerlinNoise(_time, _seed) * 3.5f;
                    _wanderTarget = new Vector2((Mathf.PerlinNoise(_time * 0.7f, _seed) - 0.5f) * 44f, (Mathf.PerlinNoise(_seed, _time * 0.7f) - 0.5f) * 14f);
                }
                _wander = Vector2.Lerp(_wander, _wanderTarget, 1f - Mathf.Exp(-2.5f * dt));
            }
            Apply();
            if (Look.Hero) AnimateFace(dt);
        }

        void AnimateFace(float dt)
        {
            // Blink.
            if (_time > _nextBlink) { _blink = 0.13f; _nextBlink = _time + 1.8f + Mathf.PerlinNoise(_seed, _time) * 4f; }
            _blink -= dt;
            float open = _blink > 0f ? 0.12f : 1f + Mathf.Max(BrowRaise, 0f) * 0.2f;
            _eyeL.localScale = _eyeR.localScale = new Vector3(1f, open, 1f);
            // Brows: raised and arched when surprised, pulled together when angry.
            float hr = _hr;
            _browL.localPosition = new Vector3(-hr * 0.38f, hr * (1.16f + BrowRaise * 0.1f), hr * 0.88f);
            _browR.localPosition = new Vector3(hr * 0.38f, hr * (1.16f + BrowRaise * 0.1f), hr * 0.88f);
            _browL.localRotation = Quaternion.Euler(0, 0, -BrowRaise * 14f + (BrowRaise < 0f ? BrowRaise * 10f : 0f));
            _browR.localRotation = Quaternion.Euler(0, 0, BrowRaise * 14f - (BrowRaise < 0f ? BrowRaise * 10f : 0f));
            // Mouth: flaps while talking.
            float talk = Talking ? Mathf.Abs(Mathf.Sin(_time * 13f + _seed)) * (0.5f + 0.5f * Mathf.PerlinNoise(_time * 6f, _seed)) : 0f;
            float m = Mathf.Max(MouthOpen, talk);
            _mouth.localScale = new Vector3(1f - m * 0.25f + (Talking ? 0f : 0.25f), 0.35f + m * 1.9f, 1f);
        }

        void Apply()
        {
            var p = _pose;
            float breath = Alive ? Mathf.Sin(_time * 1.7f + _seed) : 0f;

            // Hips: high enough that the feet reach the ground, or at the seat.
            float tL = p.ThighL * Mathf.Deg2Rad, kL = p.KneeL * Mathf.Deg2Rad;
            float standY = _ankle + (_thigh * Mathf.Cos(tL) + _shin * Mathf.Cos(tL - kL)) * Mathf.Cos(p.LegOut * Mathf.Deg2Rad) + 0.04f * _s;
            float footZ = _thigh * Mathf.Sin(tL) + _shin * Mathf.Sin(tL - kL);
            _hips.localPosition = new Vector3(0, Mathf.Lerp(standY, 0.1f * _s, p.Seated) - p.HipDrop, Mathf.Lerp(-footZ, 0f, p.Seated));

            _torso.localRotation = Quaternion.Euler(p.TorsoPitch + breath * 0.8f, p.TorsoYaw, -p.TorsoRoll);
            _head.localRotation = Quaternion.Euler(p.HeadPitch + _wander.y - LookOffset.y, p.HeadYaw + _wander.x + LookOffset.x, p.HeadRoll);

            _shL.localRotation = Quaternion.AngleAxis(-p.ArmOutL, Vector3.forward) * Quaternion.AngleAxis(-p.ArmFwdL, Vector3.right) * Quaternion.AngleAxis(p.ArmTwistL, Vector3.up);
            _shR.localRotation = Quaternion.AngleAxis(p.ArmOutR, Vector3.forward) * Quaternion.AngleAxis(-p.ArmFwdR, Vector3.right) * Quaternion.AngleAxis(-p.ArmTwistR, Vector3.up);
            _elL.localRotation = Quaternion.Euler(-p.ElbowL, 0, 0);
            _elR.localRotation = Quaternion.Euler(-p.ElbowR, 0, 0);

            _hipL.localRotation = Quaternion.AngleAxis(-p.LegOut, Vector3.forward) * Quaternion.AngleAxis(-p.ThighL, Vector3.right) * Quaternion.AngleAxis(p.FootIn, Vector3.up);
            _hipR.localRotation = Quaternion.AngleAxis(p.LegOut, Vector3.forward) * Quaternion.AngleAxis(-p.ThighR, Vector3.right) * Quaternion.AngleAxis(-p.FootIn, Vector3.up);
            _kneeL.localRotation = Quaternion.Euler(p.KneeL, 0, 0);
            _kneeR.localRotation = Quaternion.Euler(p.KneeR, 0, 0);
            // Keep the feet flat on the ground (seated: let them hang a little toe-down).
            float hang = p.Seated * 12f;
            _footL.localRotation = Quaternion.Euler(p.ThighL - p.KneeL + hang, 0, 0) * Quaternion.AngleAxis(p.LegOut * (1f - p.Seated), Vector3.forward);
            _footR.localRotation = Quaternion.Euler(p.ThighR - p.KneeR + hang, 0, 0) * Quaternion.AngleAxis(-p.LegOut * (1f - p.Seated), Vector3.forward);
            if (PolesHang)
                for (int i = 0; i < _poles.Count; i++)
                    _poles[i].rotation = transform.rotation * Quaternion.Euler(-14f, 0f, i == 0 ? 5f : -5f);
        }
    }
}
