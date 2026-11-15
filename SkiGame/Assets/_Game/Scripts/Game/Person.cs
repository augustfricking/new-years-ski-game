using System.Collections.Generic;
using Ski.Core;
using UnityEngine;
using Ring = Ski.Game.MeshBuilder.Ring;

namespace Ski.Game
{
    public enum GearKind { None, Skis, Board }

    /// <summary>Where the ski poles are: fixed in the hands, aimed while skiing (down and back), hanging straight down, or resting across the lap.</summary>
    public enum PoleMode { InHands, Ski, Hang, Lap }

    /// <summary>What a person looks like: size, face, clothes and gear.</summary>
    public sealed class PersonLook
    {
        public float Height = 1.75f;
        public bool Kid;
        public bool Hero;            // separate face parts that animate (blink, talk, brows) and finer meshes
        public bool Female;
        public Color Jacket, Trim, Pants, Gloves, Boots, Helmet, Goggles, Hair, Skin;
        public Color Eyes = Palette.Bark;
        public bool HasHelmet = true, GogglesDown = true, HasGoggles = true, LongHair, Braid, Beanie, Stubble, Hood = true;
        public float JawWidth = 1f, NoseSize = 1f;
        public GearKind Gear = GearKind.Skis;
        public float GearLength = 1.6f;
        public bool TwinTip, Poles = true;
        public Color GearColour, GearAccent;

        public static PersonLook For(CharacterId id)
        {
            var st = CharacterStyle.Get(id);
            var info = SkierInfo.Get(id);
            var look = new PersonLook
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
            switch (id)
            {
                case CharacterId.Jonte: look.Stubble = true; look.JawWidth = 1.1f; look.NoseSize = 1.15f; look.Eyes = Palette.Hex("#6A4A2E"); break;
                case CharacterId.Maja: look.Female = true; look.JawWidth = 0.9f; look.NoseSize = 0.82f; look.Eyes = Palette.Hex("#3A78E0"); look.Braid = true; break;
                case CharacterId.Erik: look.JawWidth = 1.04f; look.NoseSize = 1.05f; look.Eyes = Palette.Hex("#3F8E5A"); break;
                case CharacterId.Linn: look.Female = true; look.JawWidth = 0.93f; look.NoseSize = 0.85f; look.Eyes = Palette.Hex("#5E8F86"); break;
            }
            return look;
        }

        public static PersonLook Random(Rng rng, bool kid, float size = 1f)
        {
            Color jacket = Palette.Jackets[rng.Range(0, Palette.Jackets.Length)];
            Color other = Palette.Jackets[rng.Range(0, Palette.Jackets.Length)];
            bool female = rng.Chance(0.5f);
            return new PersonLook
            {
                Height = (kid ? 1.75f * size : female ? rng.Range(1.6f, 1.76f) : rng.Range(1.7f, 1.9f)), Kid = kid, Female = female,
                Jacket = jacket, Trim = rng.Chance(0.5f) ? Palette.Mul(jacket, 0.7f) : other, Pants = rng.Chance(0.6f) ? Palette.Pants[rng.Range(0, Palette.Pants.Length)] : other,
                Gloves = Palette.Ink, Boots = Palette.Pants[rng.Range(0, Palette.Pants.Length)], Helmet = rng.Chance(0.5f) ? other : Palette.White,
                Goggles = Palette.Jackets[rng.Range(0, Palette.Jackets.Length)], Hair = Palette.Hairs[rng.Range(0, Palette.Hairs.Length)],
                Skin = rng.Chance(0.75f) ? Palette.Skin : Palette.SkinDark,
                LongHair = female && rng.Chance(0.7f), Braid = rng.Chance(0.4f), Stubble = !female && !kid && rng.Chance(0.35f),
                JawWidth = kid ? 1.12f : female ? rng.Range(0.88f, 0.98f) : rng.Range(0.98f, 1.12f), NoseSize = kid ? 0.7f : rng.Range(0.8f, 1.2f),
                GearLength = kid ? 0.95f * size / 0.6f * 0.75f : rng.Range(1.5f, 1.75f),
                Poles = !kid, GearColour = other, GearAccent = Palette.White,
            };
        }
    }

    /// <summary>
    /// A posable skier: sculpted parts on a joint hierarchy. Poses are joint angles
    /// (<see cref="Pose"/>); the person breathes, blinks, looks about and can talk. Faces +Z; the
    /// root is at the feet (or at the seat for seated poses).
    /// </summary>
    public sealed class Person : MonoBehaviour
    {
        public PersonLook Look { get; private set; }
        public Pose Target = Pose.Stand;
        public float PoseSpeed = 6f;
        public bool Talking;
        public float BrowRaise;        // -1 = frown, +1 = surprised
        public float MouthOpen;        // 0..1 base openness (shouting kids)
        public float Smile;            // 0..1
        public Vector2 LookOffset;     // extra head yaw / pitch in degrees
        public bool Alive = true;      // breathing, blinking, small head movement
        public PoleMode Poles = PoleMode.Ski;
        /// <summary>Which way the poles point in <see cref="PoleMode.Ski"/>, in the person's own space.</summary>
        public Vector3 PoleAim = new Vector3(0f, -0.82f, -0.57f);
        /// <summary>1 = a snowboarder riding: body turned across the direction of travel, head looking ahead.</summary>
        public float SideStance;
        /// <summary>How the board hangs from the front foot when seated on the lift (yaw in degrees).</summary>
        public float BoardHangYaw = -24f;

        /// <summary>Kept for older callers: poles pointing at the ground.</summary>
        public bool PolesHang { get => Poles == PoleMode.Hang; set => Poles = value ? PoleMode.Hang : PoleMode.Ski; }

        public Transform HandL => _handL;
        public Transform HandR => _handR;
        public Transform Head => _head;
        public Transform Hips => _hips;
        public Transform Torso => _torso;
        public Transform FootL => _footL;
        public Transform FootR => _footR;
        public Transform Board => _board;
        public Transform GogglesPivot => _goggles;
        public float Scale => _s;
        /// <summary>Where the eyes are, for a first-person camera.</summary>
        public Vector3 EyeWorld => _head.TransformPoint(new Vector3(0f, _hr * EyeY, _hr * 0.55f));

        Pose _pose = Pose.Stand;
        float _s, _thigh, _shin, _ankle, _torsoLen, _upperArm, _forearm, _hr;
        Transform _hips, _torso, _head, _shL, _elL, _handL, _shR, _elR, _handR, _hipL, _kneeL, _footL, _hipR, _kneeR, _footR, _board;
        Transform _lidL, _lidR, _browL, _browR, _mouth, _lipLow, _goggles;
        readonly List<Transform> _poles = new List<Transform>();
        readonly List<Renderer> _headParts = new List<Renderer>();
        float _time, _blink, _nextBlink = 2f, _seed, _goggleUp, _goggleUpTarget, _talk;
        Vector2 _wander, _wanderTarget; float _nextWander;
        PoleMode _poleMode = PoleMode.InHands;
        bool _headVisible = true;
        static int _count;
        const float EyeX = 0.31f, EyeY = 0.85f, EyeZ = 0.775f, MouthY = 0.24f, MouthZ = 0.765f;   // in head radii

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
        /// <summary>Hides the head (first person: the camera sits inside it).</summary>
        public void SetHeadVisible(bool visible)
        {
            if (visible == _headVisible) return;
            _headVisible = visible;
            var mode = visible ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            foreach (var r in _headParts) if (r != null) r.shadowCastingMode = mode;
        }

        float SkiHalfWidth => 0.0425f * Mathf.Lerp(_s, 1f, 0.5f);
        float LegExtent(in Pose p)
        {
            float t = p.ThighL * Mathf.Deg2Rad, k = p.KneeL * Mathf.Deg2Rad;
            return _thigh * Mathf.Cos(t) + _shin * Mathf.Cos(t - k);
        }

        /// <summary>How far apart the legs must be for the ski tips to almost touch at this toe-in.</summary>
        public float LegOutForPlough(in Pose p)
        {
            float footX = SkiHalfWidth + 0.02f + 0.56f * Look.GearLength * Mathf.Sin(p.FootIn * Mathf.Deg2Rad);
            float sin = (footX - 0.095f * _s) / Mathf.Max(LegExtent(p), 0.1f);
            return Mathf.Asin(Mathf.Clamp(sin, 0f, 0.75f)) * Mathf.Rad2Deg;
        }

        static Transform Node(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        Renderer Skin(MeshBuilder b, Transform t, string name, Material mat = null)
        {
            if (b.VertexCount == 0) return null;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = b.Build(name);
            var r = t.gameObject.AddComponent<MeshRenderer>();
            // Close-up people get the inked comic shading; the crowd stays light and readable.
            r.sharedMaterial = mat != null ? mat : Look.Hero ? Mats.Character : Mats.Props;
            b.Clear();
            return r;
        }

        static void TriOut(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 inside)
        {
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
            if (Vector3.Dot(n, (p0 + p1 + p2) / 3f - inside) < 0f) b.Tri(p0, p2, p1); else b.Tri(p0, p1, p2);
        }

        /// <summary>Front surface (z) of a loft at a height and sideways offset.</summary>
        static float SurfZ(Ring[] rings, float y, float x)
        {
            for (int i = 0; i < rings.Length - 1; i++)
            {
                if (y > rings[i + 1].Y && i < rings.Length - 2) continue;
                float t = Mathf.InverseLerp(rings[i].Y, rings[i + 1].Y, y);
                float rx = Mathf.Lerp(rings[i].Rx, rings[i + 1].Rx, t), rz = Mathf.Lerp(rings[i].Rz, rings[i + 1].Rz, t);
                float k = Mathf.Clamp01(1f - (x * x) / Mathf.Max(rx * rx, 1e-6f));
                return Mathf.Lerp(rings[i].OffZ, rings[i + 1].OffZ, t) + rz * Mathf.Sqrt(k);
            }
            return 0f;
        }

        void Build(PersonLook look)
        {
            Look = look;
            float s = _s = look.Height / 1.75f;
            _ankle = 0.1f * s; _shin = 0.41f * s; _thigh = 0.42f * s; _torsoLen = 0.5f * s;
            _upperArm = 0.28f * s; _forearm = 0.26f * s;
            _hr = look.Kid ? Mathf.Lerp(0.118f * s, 0.125f, 0.75f) : 0.118f * s * (look.Hero ? 1.1f : 1.04f);
            _seed = (++_count & 1023) * 0.37f;
            var b = new MeshBuilder();
            int sides = look.Hero ? 14 : 8;
            float fem = look.Female && !look.Kid ? 1f : 0f;
            float L(float male, float female) => Mathf.Lerp(male, female, fem) * s;

            _hips = Node("Hips", transform, Vector3.zero);
            b.Col = look.Pants;
            b.Sphere(new Vector3(0, -0.03f * s, 0), new Vector3(L(0.17f, 0.176f), 0.12f * s, 0.135f * s), sides, 5);
            Skin(b, _hips, "Pelvis");

            // --- torso: a ski jacket with a waist, a chest and a yoke in the trim colour ---------
            _torso = Node("Torso", _hips, new Vector3(0, 0.02f * s, 0));
            var tr = new[]
            {
                new Ring(-0.045f * s, 0.180f * s, 0.136f * s).Tint(look.Trim),
                new Ring(0.004f * s, 0.187f * s, 0.143f * s).Tint(look.Trim),
                new Ring(0.006f * s, 0.186f * s, 0.142f * s),
                new Ring(0.14f * s, L(0.181f, 0.158f), L(0.135f, 0.122f)),
                new Ring(0.26f * s, L(0.197f, 0.178f), L(0.143f, 0.14f), L(0.004f, 0.018f)),
                new Ring(0.345f * s, L(0.213f, 0.195f), L(0.147f, 0.142f), L(0.006f, 0.014f)),
                new Ring(0.398f * s, L(0.207f, 0.19f), 0.134f * s),
                new Ring(0.4f * s, L(0.207f, 0.19f), 0.134f * s).Tint(look.Trim),
                new Ring(0.462f * s, 0.152f * s, 0.108f * s).Tint(look.Trim),
                new Ring(0.5f * s, 0.086f * s, 0.084f * s).Tint(look.Trim),
            };
            b.Col = look.Jacket;
            b.Loft(tr, sides, true, true);
            // Stand-up collar.
            b.Col = look.Trim;
            b.Loft(new[] { new Ring(0.47f * s, 0.088f * s, 0.088f * s), new Ring(0.525f * s, 0.095f * s, 0.1f * s, 0.004f * s), new Ring(0.548f * s, 0.09f * s, 0.096f * s, 0.004f * s) }, sides, false, false);
            // Zip, following the chest.
            b.Col = Palette.Ink;
            float hw = 0.008f * s;
            for (int i = 2; i < tr.Length - 2; i++)
            {
                float z0 = tr[i].OffZ + tr[i].Rz + 0.004f * s, z1 = tr[i + 1].OffZ + tr[i + 1].Rz + 0.004f * s;
                b.Quad(new Vector3(hw, tr[i].Y, z0), new Vector3(hw, tr[i + 1].Y, z1), new Vector3(-hw, tr[i + 1].Y, z1), new Vector3(-hw, tr[i].Y, z0));
            }
            if (look.Hood)
            {
                b.Col = look.Jacket;
                b.Sphere(new Vector3(0, 0.452f * s, -0.1f * s), new Vector3(0.125f, 0.062f, 0.078f) * s, sides, 4);
            }
            if (look.Hero)
            {
                // Chest pocket and a lift pass.
                float px = -0.095f * s, py = 0.33f * s;
                b.Col = look.Trim;
                b.Box(new Vector3(px, py, SurfZ(tr, py, px) - 0.002f * s), new Vector3(0.075f, 0.05f, 0.012f) * s);
                b.Col = Palette.Ink;
                b.Box(new Vector3(px, py + 0.027f * s, SurfZ(tr, py, px)), new Vector3(0.078f, 0.008f, 0.012f) * s);
                px = 0.115f * s; py = 0.17f * s;
                b.Col = Palette.White;
                b.Box(new Vector3(px, py, SurfZ(tr, py, px)), new Vector3(0.045f, 0.06f, 0.008f) * s);
            }
            Skin(b, _torso, "Jacket");

            BuildHead(b, look, s);
            BuildArms(b, look, s, sides);
            BuildLegs(b, look, s, sides);
            Apply();
        }

        // ---- head ------------------------------------------------------------------------------

        void BuildHead(MeshBuilder b, PersonLook look, float s)
        {
            float hr = _hr;
            int hs = look.Hero ? 16 : 9;
            Ring R(float y, float rx, float rz, float oz = 0f) => new Ring(y * hr, rx * hr, rz * hr, oz * hr);
            Vector3 V(float x, float y, float z) => new Vector3(x * hr, y * hr, z * hr);

            _head = Node("Head", _torso, new Vector3(0, _torsoLen + 0.078f * s, 0));
            float j = look.JawWidth;
            Color low = look.Stubble ? Color.Lerp(look.Skin, look.Hair, 0.3f) : look.Skin;
            b.Col = look.Skin;
            b.Cylinder(new Vector3(0, -0.11f * s, -0.008f * s), 0.052f * s * (look.Female ? 0.86f : 1f), 0.046f * s * (look.Female ? 0.86f : 1f), 0.14f * s, 8, true, false); // neck
            // An egg that narrows to the chin, the chin pushed forward.
            b.Loft(new[]
            {
                R(-0.25f, 0f, 0f, 0.36f).Tint(low),
                R(-0.2f, 0.2f * j, 0.2f, 0.34f).Tint(low),
                R(-0.08f, 0.4f * j, 0.42f, 0.24f).Tint(low),
                R(0.1f, 0.56f * j, 0.6f, 0.14f).Tint(low),
                R(0.3f, 0.66f * (0.4f + 0.6f * j), 0.74f, 0.07f).Tint(low),
                R(0.55f, 0.74f, 0.84f, 0.03f),
                R(0.85f, 0.79f, 0.9f),
                R(1.1f, 0.81f, 0.93f, -0.02f),
                R(1.4f, 0.79f, 0.91f, -0.04f),
                R(1.65f, 0.66f, 0.79f, -0.05f),
                R(1.84f, 0.4f, 0.5f, -0.05f),
                R(1.93f, 0f, 0f, -0.05f),
            }, hs, false, false);

            // Nose: a flat-sided wedge, which the ink lines pick out.
            float n = look.NoseSize;
            Vector3 bridge = V(0, 1.0f, 0.89f), tip = V(0, 0.55f, 0.9f + 0.2f * n), nl = V(-0.14f * n, 0.5f, 0.8f), nr = V(0.14f * n, 0.5f, 0.8f), under = V(0, 0.44f, 0.8f);
            Vector3 inside = V(0, 0.7f, 0.4f);
            TriOut(b, bridge, nl, tip, inside); TriOut(b, bridge, tip, nr, inside);
            TriOut(b, tip, nl, under, inside); TriOut(b, tip, under, nr, inside);

            bool bare = !look.HasHelmet && !look.Beanie;
            if (bare)
            {
                b.Sphere(V(-0.8f, 0.82f, -0.05f), V(0.09f, 0.2f, 0.13f), 6, 4);
                b.Sphere(V(0.8f, 0.82f, -0.05f), V(0.09f, 0.2f, 0.13f), 6, 4);
            }
            Color lip = Color.Lerp(look.Skin, Palette.Hex("#B4485A"), look.Female ? 0.55f : 0.22f);
            if (!look.Hero)
            {
                // A simple face baked into the head, for people seen from a distance.
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Col = Palette.White;
                    b.Sphere(V(side * 0.31f, 0.85f, 0.78f), V(0.16f, 0.13f, 0.07f), 6, 4);
                    b.Col = Palette.Ink;
                    b.Sphere(V(side * 0.31f, 0.84f, 0.82f), V(0.085f, 0.095f, 0.05f), 5, 3);
                    b.Col = Color.Lerp(look.Hair, Palette.Ink, 0.4f);
                    b.Box(V(side * 0.32f, 1.1f, 0.84f), V(0.32f, 0.06f, 0.06f));
                }
                b.Col = Palette.Hex("#4A1A2A");
                b.Sphere(V(0, 0.22f, 0.745f), V(0.19f, 0.04f, 0.05f), 6, 3);
            }
            else
            {
                // Upper lip (the lower one moves with the mouth).
                b.Col = lip;
                b.Sphere(V(0, 0.262f, 0.762f), V(0.17f, 0.03f, 0.05f), 8, 4);
            }

            // --- headwear and hair -----------------------------------------------------------
            if (look.HasHelmet)
            {
                b.Col = look.Helmet;
                b.Loft(new[]
                {
                    R(1.27f, 0.99f, 1.1f, -0.03f).Tint(Palette.Ink),
                    R(1.315f, 1.02f, 1.13f, -0.03f).Tint(Palette.Ink),
                    R(1.32f, 1.02f, 1.13f, -0.03f),
                    R(1.5f, 1.0f, 1.11f, -0.04f),
                    R(1.72f, 0.88f, 0.99f, -0.05f),
                    R(1.92f, 0.64f, 0.72f, -0.05f),
                    R(2.05f, 0.34f, 0.38f, -0.05f),
                    R(2.1f, 0f, 0f, -0.05f),
                }, hs, true, false);
                // Ear pads and the shell round the back of the head.
                b.Sphere(V(-0.82f, 0.82f, -0.08f), V(0.22f, 0.54f, 0.44f), 8, 5);
                b.Sphere(V(0.82f, 0.82f, -0.08f), V(0.22f, 0.54f, 0.44f), 8, 5);
                b.Sphere(V(0, 0.98f, -0.4f), V(0.96f, 0.66f, 0.76f), look.Hero ? 12 : 8, 5);
                b.Col = Palette.Ink;
                b.Tube(V(-0.78f, 0.42f, 0.14f), V(-0.24f, -0.2f, 0.36f), 0.028f * hr, 4);  // chin strap
                b.Tube(V(0.78f, 0.42f, 0.14f), V(0.24f, -0.2f, 0.36f), 0.028f * hr, 4);
                b.Tube(V(-0.24f, -0.2f, 0.36f), V(0.24f, -0.2f, 0.36f), 0.028f * hr, 4);
            }
            else
            {
                // Hair cap: sits back from the face, with a hairline above the brows.
                b.Col = look.Hair;
                b.Loft(new[]
                {
                    R(0.5f, 0.72f, 0.66f, -0.34f),
                    R(0.9f, 0.87f, 0.8f, -0.25f),
                    R(1.3f, 0.89f, 0.9f, -0.15f),
                    R(1.46f, 0.88f, 1.0f, -0.05f),
                    R(1.7f, 0.8f, 0.93f, -0.05f),
                    R(1.9f, 0.58f, 0.68f, -0.06f),
                    R(2.03f, 0.26f, 0.3f, -0.06f),
                    R(2.07f, 0f, 0f, -0.06f),
                }, hs, true, false);
                // A side-swept fringe.
                b.Sphere(V(-0.26f, 1.5f, 0.8f), V(0.56f, 0.2f, 0.22f), 7, 4);
                if (look.Beanie)
                {
                    b.Col = look.Helmet;
                    b.Loft(new[] { R(1.36f, 0.95f, 1.06f, -0.04f), R(1.6f, 0.94f, 1.05f, -0.05f), R(1.9f, 0.76f, 0.86f, -0.08f), R(2.12f, 0.46f, 0.52f, -0.12f), R(2.22f, 0f, 0f, -0.14f) }, hs, true, false);
                    b.Col = look.Trim;
                    b.Loft(new[] { R(1.3f, 0.98f, 1.09f, -0.04f), R(1.36f, 1.01f, 1.12f, -0.04f), R(1.52f, 1.01f, 1.12f, -0.04f), R(1.56f, 0.97f, 1.08f, -0.04f) }, hs, true, true);
                    b.Sphere(V(0, 2.26f, -0.14f), V(0.24f, 0.24f, 0.24f), 6, 4);
                }
            }
            if (look.LongHair)
            {
                b.Col = look.Hair;
                if (look.Braid)
                {
                    // A braid down the back.
                    for (int i = 0; i < 7; i++)
                    {
                        float t = i / 6f;
                        b.Sphere(V((i & 1) == 0 ? 0.04f : -0.04f, 0.25f - t * 1.9f, -0.86f + t * 0.12f), V(0.2f - t * 0.08f, 0.2f, 0.17f - t * 0.05f), 6, 4);
                    }
                }
                else
                {
                    // Loose hair falling over the shoulders.
                    b.Loft(new[] { R(-1.4f, 0.32f, 0.12f, -0.82f), R(-0.9f, 0.54f, 0.2f, -0.82f), R(-0.3f, 0.68f, 0.26f, -0.72f), R(0.4f, 0.78f, 0.3f, -0.6f), R(0.9f, 0.8f, 0.3f, -0.52f) }, 9, true, true);
                }
                for (int side = -1; side <= 1; side += 2)   // strands beside the face
                    b.Loft(new[] { new Ring(-0.75f * hr, 0f, 0f, 0.36f * hr, side * 0.7f * hr), new Ring(-0.3f * hr, 0.07f * hr, 0.1f * hr, 0.3f * hr, side * 0.76f * hr), new Ring(0.4f * hr, 0.09f * hr, 0.15f * hr, 0.24f * hr, side * 0.8f * hr), new Ring(1.0f * hr, 0.08f * hr, 0.16f * hr, 0.22f * hr, side * 0.8f * hr) }, 8, false, true);
            }
            _headParts.Add(Skin(b, _head, "Head"));

            if (look.Hero) BuildFace(b, look, lip);

            // Goggles: a frame, a lens and a strap; over the eyes or pushed up on the helmet.
            _goggles = Node("Goggles", _head, Vector3.zero);
            if (look.HasGoggles)
            {
                b.Col = Palette.Ink;
                b.Sphere(Vector3.zero, V(0.7f, 0.34f, 0.2f), 12, 6);
                b.Col = look.Goggles;
                b.Sphere(V(0, 0, 0.06f), V(0.62f, 0.27f, 0.17f), 12, 6);
                b.Col = Color.Lerp(look.Goggles, Palette.White, 0.65f);
                b.Push(V(-0.22f, 0.1f, 0.2f), Quaternion.Euler(0, -14f, 18f));
                b.Sphere(Vector3.zero, V(0.24f, 0.045f, 0.03f), 6, 3);       // sheen
                b.Pop();
                b.Col = Color.Lerp(look.Goggles, Palette.Ink, 0.55f);
                b.Loft(new[] { R(-0.11f, 1.07f, 1.3f, -1.05f), R(0.11f, 1.07f, 1.3f, -1.05f) }, hs, false, false);
                _headParts.Add(Skin(b, _goggles, "Goggles", Mats.Props));
            }
            _goggleUp = _goggleUpTarget = look.GogglesDown ? 0f : 1f;
            PlaceGoggles();
        }

        void BuildFace(MeshBuilder b, PersonLook look, Color lip)
        {
            float hr = _hr;
            Vector3 V(float x, float y, float z) => new Vector3(x * hr, y * hr, z * hr);
            Color browCol = Color.Lerp(look.Hair, Palette.Ink, 0.45f);
            for (int side = -1; side <= 1; side += 2)
            {
                // Eye: white, coloured iris, pupil and a highlight.
                var eye = Node(side < 0 ? "EyeL" : "EyeR", _head, V(side * EyeX, EyeY, EyeZ));
                b.Col = Palette.White;
                b.Sphere(Vector3.zero, V(0.175f, 0.125f, 0.06f), 10, 6);
                b.Col = look.Eyes;
                b.Sphere(V(0, -0.005f, 0.034f), V(0.09f, 0.1f, 0.038f), 8, 5);
                b.Col = Palette.Ink;
                b.Sphere(V(0, -0.005f, 0.056f), V(0.044f, 0.05f, 0.024f), 6, 4);
                b.Col = Palette.White;
                b.Sphere(V(0.036f, 0.032f, 0.074f), V(0.02f, 0.02f, 0.011f), 4, 3);
                // Lower lash line.
                b.Col = Color.Lerp(look.Skin, Palette.Ink, 0.45f);
                b.Box(V(0, -0.118f, 0.03f), V(0.26f, 0.016f, 0.04f));
                _headParts.Add(Skin(b, eye, "Eye", Mats.PropsNoOutline));

                // Upper lid: slides down over the eye to blink, lifts when surprised.
                var lid = Node(side < 0 ? "LidL" : "LidR", _head, Vector3.zero);
                b.Col = Color.Lerp(look.Skin, Palette.SkinDark, 0.25f);
                b.Sphere(Vector3.zero, V(0.2f, 0.14f, 0.082f), 10, 6);
                b.Col = Palette.Ink;
                b.Box(V(0, -0.09f, 0.06f), V(look.Female ? 0.4f : 0.36f, look.Female ? 0.042f : 0.028f, 0.04f));
                if (look.Female) { b.Push(V(side * 0.21f, -0.06f, 0.05f), Quaternion.Euler(0, 0, side * 28f)); b.Box(Vector3.zero, V(0.14f, 0.035f, 0.03f)); b.Pop(); }
                _headParts.Add(Skin(b, lid, "Lid", Mats.PropsNoOutline));
                if (side < 0) _lidL = lid; else _lidR = lid;

                var brow = Node(side < 0 ? "BrowL" : "BrowR", _head, Vector3.zero);
                b.Col = browCol;
                float th = look.Female ? 0.05f : 0.075f;
                b.Box(V(-side * 0.07f, 0, 0), V(0.22f, th, 0.05f));
                b.Push(V(side * 0.16f, -0.015f, -0.015f), Quaternion.Euler(0, side * 16f, -side * 12f));
                b.Box(Vector3.zero, V(0.18f, th * 0.7f, 0.045f));
                b.Pop();
                _headParts.Add(Skin(b, brow, "Brow", Mats.PropsNoOutline));
                if (side < 0) _browL = brow; else _browR = brow;
            }
            // Mouth: a dark interior with teeth that opens downward, and a lower lip that follows it.
            _mouth = Node("Mouth", _head, V(0, MouthY, MouthZ));
            b.Col = Palette.Hex("#4A1A2A");
            b.Sphere(V(0, -0.13f, 0), V(0.18f, 0.13f, 0.05f), 10, 6);
            b.Col = Palette.White;
            b.Box(V(0, -0.045f, 0.032f), V(0.22f, 0.055f, 0.03f));
            _headParts.Add(Skin(b, _mouth, "Mouth", Mats.PropsNoOutline));
            _lipLow = Node("LipLow", _head, Vector3.zero);
            b.Col = lip;
            b.Sphere(Vector3.zero, V(0.145f, 0.036f, 0.05f), 8, 4);
            _headParts.Add(Skin(b, _lipLow, "Lip", Mats.PropsNoOutline));
            AnimateFace(0f);
        }

        void PlaceGoggles()
        {
            float hr = _hr, k = _goggleUp;
            bool hard = Look.HasHelmet;
            Vector3 up = hard ? new Vector3(0, hr * 1.62f, hr * 1.02f) : new Vector3(0, hr * 1.58f, hr * 0.94f);
            _goggles.localPosition = Vector3.Lerp(new Vector3(0, hr * 0.85f, hr * 0.88f), up, k);
            _goggles.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -30f, k), 0f, 0f);
        }

        // ---- limbs -----------------------------------------------------------------------------

        void BuildArms(MeshBuilder b, PersonLook look, float s, int sides)
        {
            float shx = (look.Female && !look.Kid ? 0.192f : 0.207f) * s;
            for (int side = -1; side <= 1; side += 2)
            {
                var sh = Node(side < 0 ? "ShoulderL" : "ShoulderR", _torso, new Vector3(side * shx, 0.43f * s, 0));
                b.Col = look.Jacket;
                b.Loft(new[]
                {
                    new Ring(-_upperArm, 0.054f * s, 0.056f * s),
                    new Ring(-_upperArm * 0.6f, 0.062f * s, 0.064f * s),
                    new Ring(-_upperArm * 0.22f, 0.072f * s, 0.073f * s),
                    new Ring(0f, 0.07f * s, 0.07f * s),
                }, sides, false, false);
                b.Sphere(new Vector3(0, -_upperArm, 0), 0.056f * s, sides, 5);            // elbow
                b.Col = look.Trim;
                b.Sphere(Vector3.zero, new Vector3(0.078f, 0.074f, 0.08f) * s, sides, 5);   // shoulder cap
                Skin(b, sh, "UpperArm");

                var el = Node(side < 0 ? "ElbowL" : "ElbowR", sh, new Vector3(0, -_upperArm, 0));
                b.Col = look.Jacket;
                b.Loft(new[]
                {
                    new Ring(-_forearm, 0.047f * s, 0.049f * s),
                    new Ring(-_forearm * 0.55f, 0.057f * s, 0.058f * s),
                    new Ring(0f, 0.055f * s, 0.056f * s),
                }, sides, false, false);
                b.Col = look.Trim;                                                           // cuff
                b.Loft(new[] { new Ring(-_forearm - 0.02f * s, 0.052f * s, 0.054f * s), new Ring(-_forearm + 0.012f * s, 0.057f * s, 0.058f * s), new Ring(-_forearm + 0.04f * s, 0.055f * s, 0.056f * s) }, sides, true, false);
                Skin(b, el, "Forearm");

                var hand = Node(side < 0 ? "HandL" : "HandR", el, new Vector3(0, -_forearm, 0));
                b.Col = look.Gloves;
                b.Sphere(new Vector3(0, -0.05f * s, 0), new Vector3(0.046f, 0.052f, 0.034f) * s, 8, 5);              // palm
                b.Sphere(new Vector3(0, -0.092f * s, 0.018f * s), new Vector3(0.043f, 0.04f, 0.037f) * s, 8, 5);     // curled fingers
                b.Sphere(new Vector3(-side * 0.042f * s, -0.045f * s, 0.024f * s), new Vector3(0.02f, 0.034f, 0.022f) * s, 6, 4); // thumb
                b.Col = Color.Lerp(look.Gloves, Palette.White, 0.18f);
                b.Box(new Vector3(0, -0.058f * s, -0.03f * s), new Vector3(0.06f, 0.05f, 0.016f) * s);                // knuckle guard
                Skin(b, hand, "Glove");
                if (side < 0) { _shL = sh; _elL = el; _handL = hand; } else { _shR = sh; _elR = el; _handR = hand; }

                if (look.Poles && look.Gear == GearKind.Skis)
                {
                    var pole = Node("Pole", hand, new Vector3(0, -0.06f * s, 0.02f * s));
                    pole.localRotation = Quaternion.Euler(-62f, 0f, 0f);
                    GearModels.Pole(b, 1.12f * s, look.GearAccent);
                    Skin(b, pole, "Pole", Mats.Props);
                    _poles.Add(pole);
                }
            }
        }

        void BuildLegs(MeshBuilder b, PersonLook look, float s, int sides)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = Node(side < 0 ? "HipL" : "HipR", _hips, new Vector3(side * 0.095f * s, -0.04f * s, 0));
                b.Col = look.Pants;
                b.Loft(new[]
                {
                    new Ring(-_thigh, 0.07f * s, 0.075f * s),
                    new Ring(-_thigh * 0.55f, 0.083f * s, 0.087f * s),
                    new Ring(-_thigh * 0.15f, 0.093f * s, 0.096f * s),
                    new Ring(0f, 0.088f * s, 0.092f * s),
                }, sides, false, false);
                b.Sphere(Vector3.zero, 0.09f * s, sides, 5);
                b.Sphere(new Vector3(0, -_thigh, 0.004f * s), 0.074f * s, sides, 5);   // knee
                Skin(b, hip, "Thigh");

                var knee = Node(side < 0 ? "KneeL" : "KneeR", hip, new Vector3(0, -_thigh, 0));
                b.Col = look.Pants;
                b.Loft(new[]
                {
                    new Ring(-_shin + 0.03f * s, 0.083f * s, 0.092f * s, 0.012f * s),   // flares over the boot
                    new Ring(-_shin + 0.14f * s, 0.071f * s, 0.077f * s, 0.002f * s),
                    new Ring(-_shin * 0.5f, 0.069f * s, 0.076f * s, -0.006f * s),
                    new Ring(0f, 0.071f * s, 0.074f * s),
                }, sides, true, false);
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
                Skin(b, foot, "Foot", Mats.Props);
                if (side < 0) { _hipL = hip; _kneeL = knee; _footL = foot; } else { _hipR = hip; _kneeR = knee; _footR = foot; }
            }
            if (look.Gear == GearKind.Board)
            {
                _board = Node("Board", _footL, Vector3.zero);
                GearModels.Board(b, look.GearLength, look.GearColour, look.GearAccent);
                Skin(b, _board, "Board", Mats.Props);
            }
        }

        // ---- animation -------------------------------------------------------------------------

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
            float hr = _hr;
            // Blink.
            if (_time > _nextBlink) { _blink = 0.13f; _nextBlink = _time + 1.8f + Mathf.PerlinNoise(_seed, _time) * 4f; }
            _blink -= dt;
            float lidUp = _blink > 0f ? -0.02f : 0.215f + (BrowRaise > 0f ? BrowRaise * 0.08f : BrowRaise * 0.06f) - Smile * 0.03f;
            _lidL.localPosition = new Vector3(-hr * EyeX, hr * (EyeY + lidUp), hr * EyeZ);
            _lidR.localPosition = new Vector3(hr * EyeX, hr * (EyeY + lidUp), hr * EyeZ);
            // Brows: raised and arched when surprised, pulled down and together when angry.
            float by = 1.12f + BrowRaise * 0.09f;
            float tilt = BrowRaise > 0f ? BrowRaise * 8f : BrowRaise * 20f;
            _browL.localPosition = new Vector3(-hr * 0.33f, hr * by, hr * 0.85f);
            _browR.localPosition = new Vector3(hr * 0.33f, hr * by, hr * 0.85f);
            _browL.localRotation = Quaternion.Euler(0, 0, -tilt);
            _browR.localRotation = Quaternion.Euler(0, 0, tilt);
            // Mouth: opens in syllables while talking.
            float target = 0f;
            if (Talking)
            {
                float syll = Mathf.Abs(Mathf.Sin(_time * 11f + _seed)) * (0.35f + 0.65f * Mathf.PerlinNoise(_time * 5f, _seed));
                target = Mathf.PerlinNoise(_time * 1.3f, _seed + 9f) > 0.24f ? 0.15f + syll * 0.75f : 0.05f; // short pauses between phrases
            }
            _talk = Mathf.Lerp(_talk, target, 1f - Mathf.Exp(-26f * dt));
            float m = Mathf.Clamp01(Mathf.Max(MouthOpen, _talk));
            float wide = 1f + Smile * 0.3f - m * 0.12f;
            float open = 0.1f + m * 0.9f + Smile * 0.12f;
            _mouth.localScale = new Vector3(wide, open, 1f);
            _lipLow.localPosition = new Vector3(0, hr * (MouthY - 0.26f * open - 0.012f), hr * (MouthZ - 0.005f));
            _lipLow.localScale = new Vector3(wide, 1f, 1f);
        }

        void Apply()
        {
            var p = _pose;
            float breath = Alive ? Mathf.Sin(_time * 1.7f + _seed) : 0f;
            float side = SideStance * (1f - p.Seated);

            // Hips: high enough that the feet reach the ground, or at the seat.
            float tL = p.ThighL * Mathf.Deg2Rad, kL = p.KneeL * Mathf.Deg2Rad;
            float ext = _thigh * Mathf.Cos(tL) + _shin * Mathf.Cos(tL - kL);
            float standY = _ankle + ext * Mathf.Cos(p.LegOut * Mathf.Deg2Rad) + 0.04f * _s;
            // Snowplough: the toes turn in at the ankle, never so far that the ski tips cross.
            float footIn = p.FootIn;
            if (Look.Gear == GearKind.Skis && footIn > 0f)
            {
                float footX = 0.095f * _s + ext * Mathf.Sin(p.LegOut * Mathf.Deg2Rad);
                float maxSin = (footX - SkiHalfWidth - 0.012f) / (0.56f * Look.GearLength);
                footIn = Mathf.Min(footIn, Mathf.Asin(Mathf.Clamp(maxSin, 0f, 0.6f)) * Mathf.Rad2Deg);
            }
            float footZ = _thigh * Mathf.Sin(tL) + _shin * Mathf.Sin(tL - kL);
            var hipRot = Quaternion.Euler(0f, side * 82f, 0f);
            _hips.localRotation = hipRot;
            _hips.localPosition = hipRot * new Vector3(0, 0, Mathf.Lerp(-footZ, 0f, p.Seated)) + new Vector3(0, Mathf.Lerp(standY, 0.1f * _s, p.Seated) - p.HipDrop, 0);

            _torso.localRotation = Quaternion.Euler(p.TorsoPitch + breath * 0.8f, p.TorsoYaw - side * 22f, -p.TorsoRoll);
            _head.localRotation = Quaternion.Euler(p.HeadPitch + _wander.y - LookOffset.y, p.HeadYaw + _wander.x + LookOffset.x - side * 56f, p.HeadRoll);

            _shL.localRotation = Quaternion.AngleAxis(-p.ArmOutL, Vector3.forward) * Quaternion.AngleAxis(-p.ArmFwdL, Vector3.right) * Quaternion.AngleAxis(p.ArmTwistL, Vector3.up);
            _shR.localRotation = Quaternion.AngleAxis(p.ArmOutR, Vector3.forward) * Quaternion.AngleAxis(-p.ArmFwdR, Vector3.right) * Quaternion.AngleAxis(-p.ArmTwistR, Vector3.up);
            _elL.localRotation = Quaternion.Euler(-p.ElbowL, 0, 0);
            _elR.localRotation = Quaternion.Euler(-p.ElbowR, 0, 0);

            _hipL.localRotation = Quaternion.AngleAxis(-p.LegOut, Vector3.forward) * Quaternion.AngleAxis(-p.ThighL, Vector3.right);
            _hipR.localRotation = Quaternion.AngleAxis(p.LegOut, Vector3.forward) * Quaternion.AngleAxis(-p.ThighR, Vector3.right);
            _kneeL.localRotation = Quaternion.Euler(p.KneeL, 0, 0);
            _kneeR.localRotation = Quaternion.Euler(p.KneeR, 0, 0);
            // Keep the feet flat on the ground (seated: let them hang a little toe-down).
            float hang = p.Seated * 12f;
            _footL.localRotation = Quaternion.Euler(p.ThighL - p.KneeL + hang, 0, 0) * Quaternion.AngleAxis(p.LegOut * (1f - p.Seated), Vector3.forward) * Quaternion.Euler(0f, footIn, -footIn * 0.35f);
            _footR.localRotation = Quaternion.Euler(p.ThighR - p.KneeR + hang, 0, 0) * Quaternion.AngleAxis(-p.LegOut * (1f - p.Seated), Vector3.forward) * Quaternion.Euler(0f, -footIn, footIn * 0.35f);

            if (_board != null)
            {
                // Riding: across the body, under both feet. On the lift: hanging from the front
                // foot, pointing ahead, most of it under the rider's own seat.
                var rot = Quaternion.Euler(0f, Mathf.Lerp(90f, BoardHangYaw, p.Seated), 0f);
                _board.localRotation = rot;
                _board.localPosition = new Vector3(0, -_ankle - 0.025f, 0) + rot * new Vector3(0, 0, Mathf.Lerp(0.095f * _s + (_thigh + _shin) * Mathf.Sin(p.LegOut * Mathf.Deg2Rad), -0.26f, p.Seated));
            }
            if (_poleMode == PoleMode.Lap && Poles != PoleMode.Lap)
                foreach (var pole in _poles) { pole.localPosition = new Vector3(0, -0.06f * _s, 0.02f * _s); pole.localRotation = Quaternion.Euler(-62f, 0f, 0f); }
            _poleMode = Poles;
            if (Poles == PoleMode.Ski)
            {
                // Tucked low the poles lie back under the arms; otherwise they trail down and back.
                Vector3 aim = Vector3.Lerp(PoleAim, new Vector3(0f, -0.06f, -1f), Mathf.Clamp01((p.TorsoPitch - 30f) / 30f)).normalized;
                for (int i = 0; i < _poles.Count; i++)
                    _poles[i].rotation = transform.rotation * Quaternion.FromToRotation(Vector3.down, new Vector3(aim.x + (i == 0 ? -0.1f : 0.1f), aim.y, aim.z));
            }
            else if (Poles == PoleMode.Hang)
            {
                for (int i = 0; i < _poles.Count; i++)
                    _poles[i].rotation = transform.rotation * Quaternion.Euler(-14f, 0f, i == 0 ? 5f : -5f);
            }
            else if (Poles == PoleMode.Lap)
            {
                // Both poles rest along the thighs, baskets ahead of the knees.
                for (int i = 0; i < _poles.Count; i++)
                {
                    float sx = i == 0 ? -1f : 1f;
                    _poles[i].position = _hips.TransformPoint(new Vector3(sx * 0.035f * _s, 0.105f * _s, 0.02f * _s));
                    _poles[i].rotation = _hips.rotation * Quaternion.Euler(-84f, -sx * 3f, 0f);
                }
            }
        }
    }
}
