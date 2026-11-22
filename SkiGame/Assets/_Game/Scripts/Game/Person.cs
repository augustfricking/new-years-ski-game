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
        /// <summary>The sculpted body in Resources/Models to wear (null: simple shapes, as for kids).</summary>
        public string Model;
        /// <summary>From the shop: a printed pattern, special headgear, a whole suit (see <see cref="Unlocks"/>).</summary>
        public int Pattern, Headgear, Suit, GearDesign;
        public GearKind Gear = GearKind.Skis;
        public float GearLength = 1.6f;
        public bool TwinTip, Poles = true;
        public Color GearColour, GearAccent;

        /// <summary>A friend as dressed on this device, or in the outfit given (another player's).</summary>
        public static PersonLook For(CharacterId id, Outfit outfit)
        {
            var look = For(id, false);
            look.Apply(outfit ?? SaveData.Outfit(id));
            return look;
        }

        public static PersonLook For(CharacterId id) => For(id, null);

        static readonly Color GoldA = Palette.Hex("#E8B93A"), GoldB = Palette.Hex("#9C6B1E"), BananaA = Palette.Hex("#F5D33B"), BananaB = Palette.Hex("#7A5A20");

        /// <summary>Dresses the look in an outfit from the shop.</summary>
        public void Apply(Outfit o)
        {
            if (o == null) return;
            Color Pick(int i, Color own) => i >= 0 && i < OutfitPaint.Colours.Length ? OutfitPaint.Colours[i] : own;
            Jacket = Pick(o.jacket, Jacket); Trim = Pick(o.trim, Trim); Pants = Pick(o.pants, Pants); Helmet = Pick(o.helmet, Helmet);
            Pattern = o.pattern; Headgear = o.headgear; Suit = o.suit;
            GearDesign = o.gear;
            GearColour = Pick(o.gearColour, GearColour); GearAccent = Pick(o.gearAccent, GearAccent);
            if (Suit == 1) { Jacket = GoldA; Trim = GoldB; Pants = GoldA; Gloves = GoldB; Boots = GoldA; if (o.helmet < 0) Helmet = GoldA; }
            if (Suit == 2) { Jacket = BananaA; Trim = BananaB; Pants = BananaA; Gloves = BananaB; if (o.helmet < 0) Helmet = BananaA; }
            Color H(string hex) => Palette.Hex(hex);
            switch (Suit)
            {
                case 3: Jacket = H("#A9632F"); Pants = H("#A9632F"); Trim = H("#F7F4EE"); Gloves = H("#F7F4EE"); if (Pattern == 0) Pattern = 3; break;          // gingerbread, icing buttons
                case 4: Jacket = Pants = H("#F7F4EE"); Trim = H("#6C58C9"); if (Pattern == 0) Pattern = Unlocks.PatternRainbow; break;                          // rainbow
                case 5: Jacket = H("#D9342B"); Pants = H("#D9342B"); Trim = H("#F7F4EE"); Gloves = H("#22202E"); if (Pattern == 0) Pattern = 9; if (o.helmet < 0) Helmet = H("#F7F4EE"); break; // race suit
                case 6: Jacket = H("#1A1726"); Pants = H("#1A1726"); Trim = H("#E8B93A"); Gloves = H("#E8B93A"); Boots = H("#1A1726"); if (o.helmet < 0) Helmet = H("#1A1726"); break; // midnight
                case 7: Jacket = Pants = H("#EEF0F5"); Trim = H("#B9C2D6"); Gloves = H("#B9C2D6"); if (Pattern == 0) Pattern = Unlocks.PatternFur; if (o.helmet < 0) Helmet = H("#EEF0F5"); break; // yeti
            }
            switch (Headgear)
            {
                case 1: HasHelmet = false; Beanie = true; break;                                   // beanie
                case 2: if (o.helmet < 0) Helmet = Palette.Hex("#8C8E98"); break;                 // viking: steel
                case 3: if (o.helmet < 0) Helmet = Palette.Hex("#F7F4EE"); HasGoggles = false; break; // goalie
                case 4: if (o.helmet < 0) Helmet = Palette.Hex("#D9342B"); break;                 // boxer
                case 5: HasHelmet = false; Beanie = false; break;                                  // crown on the hair
                case 6: if (o.helmet < 0) Helmet = Palette.Hex("#F7F4EE"); break;                 // unicorn
                case 7: HasHelmet = false; Beanie = true; break;                                   // bunny ears on a beanie
                case 8: HasHelmet = false; Beanie = false; HasGoggles = false; break;              // snapback on the hair
                case 9: HasGoggles = false; break;                                                 // aero helmet with its own visor
            }
            if (Suit == 2 && Headgear == 0) { HasHelmet = false; Beanie = true; }                  // the banana keeps its hat
        }

        static PersonLook For(CharacterId id, bool unused)
        {
            var st = CharacterStyle.Get(id);
            var info = SkierInfo.Get(id);
            var look = new PersonLook
            {
                Height = 1.75f * st.Height, Hero = true, Model = id.ToString().ToLowerInvariant(),
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
                Model = kid ? null : female ? "female" : "male",
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
        public bool EyesClosed;
        public Vector2 LookOffset;     // extra head yaw / pitch in degrees
        public bool Alive = true;      // breathing, blinking, small head movement
        public PoleMode Poles = PoleMode.Ski;
        /// <summary>Which way the poles point in <see cref="PoleMode.Ski"/>, in the person's own space.</summary>
        public Vector3 PoleAim = new Vector3(0f, -0.82f, -0.57f);
        /// <summary>1 = a snowboarder riding: body turned across the direction of travel, head looking ahead.</summary>
        public float SideStance;
        /// <summary>How the board hangs from the front foot when seated on the lift (yaw in degrees).</summary>
        public float BoardHangYaw = -24f;
        /// <summary>On the lift the board hangs from this foot (the one away from the neighbour).</summary>
        public bool BoardOnRightFoot { get => _boardRight; set { _boardRight = value; if (_board != null) _board.SetParent(value ? _footR : _footL, false); } }
        bool _boardRight;

        /// <summary>Kept for older callers: poles pointing at the ground.</summary>
        public bool PolesHang { get => Poles == PoleMode.Hang; set => Poles = value ? PoleMode.Hang : PoleMode.Ski; }

        public Transform HandL => _handL;
        public Transform ShoulderL => _shL;
        public Transform ShoulderR => _shR;
        public Transform ElbowL => _elL;
        public Transform ElbowR => _elR;
        /// <summary>Extra forward raise of each arm (degrees), on top of the pose: lifts an arm clear of something in the way.</summary>
        public float ArmLiftL, ArmLiftR;
        public Transform HandR => _handR;
        public Transform Head => _head;
        public Transform Hips => _hips;
        public Transform Torso => _torso;
        public Transform FootL => _footL;
        public Transform FootR => _footR;
        public Transform ThighL => _hipL;
        public Transform ThighR => _hipR;
        public Transform KneeL => _kneeL;
        public Transform KneeR => _kneeR;
        /// <summary>The pose being shown right now (it eases toward <see cref="Target"/>).</summary>
        public Pose Current => _pose;
        public Transform Board => _board;
        public Transform GogglesPivot => _goggles;
        public float Scale => _s;
        /// <summary>Where the eyes are, for a first-person camera.</summary>
        public Vector3 EyeWorld => _head.TransformPoint(_eyeLocal);

        Pose _pose = Pose.Stand;
        float _s, _thigh, _shin, _ankle, _torsoLen, _upperArm, _forearm, _hr, _hipX, _hipDrop;
        SkinModel _model;
        SkinnedMeshRenderer _face;
        int _kBlink, _kMouth, _kSmile, _kBrowUp, _kBrowDown;
        float _blinkW;
        Vector3 _eyeLocal, _gogDown, _gogUp;
        readonly List<Vector3> _poleHome = new List<Vector3>();
        readonly List<Mesh> _ownMeshes = new List<Mesh>();
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
        Renderer[] _all;
        bool _visible = true;
        /// <summary>Shows or hides the whole figure (for blinking while protected after a crash).</summary>
        public void SetVisible(bool on)
        {
            if (on == _visible) return;
            _visible = on;
            _all ??= GetComponentsInChildren<Renderer>(true);
            foreach (var r in _all) if (r != null) r.enabled = on;
        }

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
            float sin = (footX - _hipX) / Mathf.Max(LegExtent(p), 0.1f);
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
            r.sharedMaterial = mat != null ? mat : Look.Hero ? Mats.Character : Mats.People;
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

        /// <summary>A point on a loft's skin: y along it, the angle round it from the front (+Z) toward +X.</summary>
        static Vector3 OnLoft(Ring[] r, float y, float deg, out Vector3 normal)
        {
            int i = 0;
            while (i < r.Length - 2 && y > r[i + 1].Y) i++;
            float t = Mathf.InverseLerp(r[i].Y, r[i + 1].Y, y);
            float rx = Mathf.Lerp(r[i].Rx, r[i + 1].Rx, t), rz = Mathf.Lerp(r[i].Rz, r[i + 1].Rz, t);
            float e = Mathf.Lerp(r[i].Exponent, r[i + 1].Exponent, t);
            float a = deg * Mathf.Deg2Rad, sn = Mathf.Sin(a), cs = Mathf.Cos(a);
            float sx = Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), e), cz = Mathf.Sign(cs) * Mathf.Pow(Mathf.Abs(cs), e);
            normal = new Vector3(sn / Mathf.Max(rx, 1e-4f), 0f, cs / Mathf.Max(rz, 1e-4f)).normalized;
            return new Vector3(Mathf.Lerp(r[i].OffX, r[i + 1].OffX, t) + rx * sx, y, Mathf.Lerp(r[i].OffZ, r[i + 1].OffZ, t) + rz * cz);
        }

        static readonly List<Vector3> StrokePts = new List<Vector3>(), StrokeNrm = new List<Vector3>();

        /// <summary>
        /// Draws one pen stroke on a loft, from (y0, angle0) to (y1, angle1): the creases, folds and
        /// face lines that a comic artist inks over the colour. Sag bends the line up or down.
        /// </summary>
        void Ink(MeshBuilder b, Ring[] r, float y0, float d0, float y1, float d1, float width, float sag = 0f, int n = 7)
        {
            StrokePts.Clear(); StrokeNrm.Clear();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector3 p = OnLoft(r, Mathf.Lerp(y0, y1, t) + sag * Mathf.Sin(Mathf.PI * t), Mathf.Lerp(d0, d1, t), out Vector3 nrm);
                StrokePts.Add(p + nrm * (0.0035f * _s)); StrokeNrm.Add(nrm);
            }
            Color keep = b.Col;
            b.Col = Palette.Ink;
            b.Stroke(StrokePts, StrokeNrm, width);
            b.Col = keep;
        }

        void Build(PersonLook look)
        {
            Look = look;
            _model = look.Kid ? null : SkinModel.Load(look.Model);
            if (_model != null) { BuildSkinned(look); Apply(); if (!look.Hero) Merge(); return; }
            float s = _s = look.Height / 1.75f;
            _hipX = 0.095f * s; _hipDrop = 0.04f * s;
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
                // Inked folds: the cloth pulls from the armpits to the zip and bunches at the waist.
                float w = 0.011f * s;
                for (int side = -1; side <= 1; side += 2)
                {
                    Ink(b, tr, 0.37f * s, side * 62f, 0.28f * s, side * 20f, w, -0.012f * s);
                    Ink(b, tr, 0.31f * s, side * 74f, 0.215f * s, side * 34f, w * 0.85f, -0.01f * s);
                    Ink(b, tr, 0.115f * s, side * 18f, 0.1f * s, side * 62f, w, 0.014f * s);
                    Ink(b, tr, 0.07f * s, side * 30f, 0.062f * s, side * 78f, w * 0.8f, 0.01f * s);
                    Ink(b, tr, 0.2f * s, side * 96f, 0.11f * s, side * 112f, w * 0.9f, 0f);
                    Ink(b, tr, 0.3f * s, side * 150f, 0.18f * s, side * 168f, w, 0f);              // down the back
                }
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
            if (!look.Hero) Merge();
        }

        /// <summary>
        /// Someone in the background (the kids' slope, the chairs, the village): drawn only within the
        /// people distance of the graphics settings, and casting no shadow when shadows are low.
        /// </summary>
        public bool Crowd;
        int _gfx = -1;
        bool _far;

        /// <summary>Whether crowd figures are merged (off only to compare the two in a diagnostic view).</summary>
        public static bool MergeCrowd = true;

        /// <summary>A figure in the crowd becomes one skinned mesh: a draw call per material, not one per part.</summary>
        void Merge()
        {
            if (MergeCrowd && MeshMerge.Figure(transform, _hips, _ownMeshes) != null) { _probe = null; _all = null; }
        }

        void Cull()
        {
            _all ??= GetComponentsInChildren<Renderer>(true);
            if (_gfx != GraphicsQuality.Version)
            {
                _gfx = GraphicsQuality.Version;
                var mode = GraphicsQuality.CrowdShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                foreach (var r in _all) if (r != null && !(r is LineRenderer)) r.shadowCastingMode = mode;
            }
            else if (((Time.frameCount + (int)(_seed * 10f)) & 7) != 0) return;
            var cam = Camera.main;
            float reach = GraphicsQuality.PeopleReach;
            bool far = cam != null && (cam.transform.position - transform.position).sqrMagnitude > reach * reach;
            if (far == _far) return;
            _far = far;
            foreach (var r in _all) if (r != null) r.forceRenderingOff = far;
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
            // An egg that narrows to the chin, the chin pushed forward; the jaw is squared off, more so on men.
            float sq = look.Female || look.Kid ? 2.2f : 2.7f;
            var hd = new[]
            {
                R(-0.25f, 0f, 0f, 0.36f).Tint(low),
                R(-0.2f, 0.22f * j, 0.2f, 0.34f).Tint(low).Square(sq),
                R(-0.08f, 0.42f * j, 0.42f, 0.24f).Tint(low).Square(sq),
                R(0.1f, 0.57f * j, 0.6f, 0.14f).Tint(low).Square(sq),
                R(0.3f, 0.67f * (0.4f + 0.6f * j), 0.74f, 0.07f).Tint(low).Square(sq),
                R(0.55f, 0.76f, 0.84f, 0.03f).Square(2.3f),
                R(0.85f, 0.79f, 0.9f),
                R(1.1f, 0.81f, 0.93f, -0.02f),
                R(1.4f, 0.79f, 0.91f, -0.04f),
                R(1.65f, 0.66f, 0.79f, -0.05f),
                R(1.84f, 0.4f, 0.5f, -0.05f),
                R(1.93f, 0f, 0f, -0.05f),
            };
            b.Loft(hd, hs, false, false);
            if (look.Hero)
            {
                // The lines an inker puts on a face: jaw, the fold from nose to mouth, under the eyes, the chin.
                float fw = 0.03f * hr;
                for (int side = -1; side <= 1; side += 2)
                {
                    Ink(b, hd, 0.3f * hr, side * 68f, -0.12f * hr, side * 26f, fw * 1.25f, -0.03f * hr);          // jaw
                    Ink(b, hd, 0.5f * hr, side * 14f, 0.2f * hr, side * 25f, fw * (look.Female ? 0.6f : 1f), 0f);   // nose to mouth
                    Ink(b, hd, 0.735f * hr, side * 12f, 0.71f * hr, side * 34f, fw * 0.7f, -0.018f * hr);          // under the eye
                    if (!look.Female) Ink(b, hd, 0.62f * hr, side * 50f, 0.36f * hr, side * 41f, fw, 0f);          // cheekbone
                }
                Ink(b, hd, 0.02f * hr, -11f, 0.02f * hr, 11f, fw * 0.9f, -0.02f * hr);                              // under the lip
                if (look.Stubble)
                {
                    // Stubble: a scatter of short pen ticks over the jaw and lip.
                    var rng = new Rng(91);
                    for (int i = 0; i < 90; i++)
                    {
                        float y = rng.Range(-0.16f, 0.42f) * hr, deg = rng.Range(-66f, 66f);
                        if (y > 0.12f * hr && y < 0.3f * hr && Mathf.Abs(deg) < 17f) continue;   // not across the mouth
                        Ink(b, hd, y, deg, y + 0.035f * hr, deg + rng.Range(-1.5f, 1.5f), 0.016f * hr, 0f, 2);
                    }
                }
            }

            // Nose: a flat-sided wedge, which the ink lines pick out.
            float n = look.NoseSize;
            Vector3 bridge = V(0, 1.0f, 0.89f), tip = V(0, 0.55f, 0.9f + 0.2f * n), nl = V(-0.14f * n, 0.5f, 0.8f), nr = V(0.14f * n, 0.5f, 0.8f), under = V(0, 0.44f, 0.8f);
            Vector3 inside = V(0, 0.7f, 0.4f);
            TriOut(b, bridge, nl, tip, inside); TriOut(b, bridge, tip, nr, inside);
            TriOut(b, tip, nl, under, inside); TriOut(b, tip, under, nr, inside);
            if (look.Hero)
            {
                Color skinCol = b.Col;
                b.Col = Palette.Ink;
                b.Sphere(V(-0.075f * n, 0.475f, 0.86f), V(0.04f, 0.022f, 0.03f), 5, 3);   // nostrils
                b.Sphere(V(0.075f * n, 0.475f, 0.86f), V(0.04f, 0.022f, 0.03f), 5, 3);
                b.Col = skinCol;
            }

            bool bare = !look.HasHelmet && !look.Beanie;
            if (bare)
            {
                b.Sphere(V(-0.8f, 0.82f, -0.05f), V(0.09f, 0.2f, 0.13f), 6, 4);
                b.Sphere(V(0.8f, 0.82f, -0.05f), V(0.09f, 0.2f, 0.13f), 6, 4);
            }
            Color lip = Color.Lerp(look.Skin, Palette.Hex("#C0392B"), look.Female ? 0.82f : 0.24f);
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
                // Upper lip: two lobes with a dip between them (the lower one moves with the mouth).
                b.Col = lip;
                float lw = look.Female ? 1f : 0.8f;
                if (look.Female)
                {
                    b.Sphere(V(-0.07f, 0.27f, 0.762f), V(0.105f, 0.04f, 0.05f), 7, 4);
                    b.Sphere(V(0.07f, 0.27f, 0.762f), V(0.105f, 0.04f, 0.05f), 7, 4);
                }
                else b.Sphere(V(0, 0.262f, 0.76f), V(0.16f, 0.026f * lw, 0.046f), 8, 4);
            }

            BuildHeadwear(b, look, hs, hr);
            _headParts.Add(Skin(b, _head, "Head"));

            if (look.Hero) BuildFace(b, look, lip);

            // Goggles: a frame, a lens and a strap; over the eyes or pushed up on the helmet.
            _goggles = Node("Goggles", _head, Vector3.zero);
            if (look.HasGoggles)
            {
                BuildGoggles(b, look, hr, look.Hero ? 26 : 14);
                // The strap: from one outrigger round the back of the head to the other (never across the lens).
                b.Col = Color.Lerp(look.Goggles, Palette.Ink, 0.35f);
                const int seg = 18;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = Mathf.Lerp(48f, 312f, i / (float)seg) * Mathf.Deg2Rad, a1 = Mathf.Lerp(48f, 312f, (i + 1) / (float)seg) * Mathf.Deg2Rad;
                    Vector3 p0 = V(Mathf.Sin(a0) * 1.07f, 0f, Mathf.Cos(a0) * 1.16f - 1.02f), p1 = V(Mathf.Sin(a1) * 1.07f, 0f, Mathf.Cos(a1) * 1.16f - 1.02f);
                    Vector3 up = V(0f, 0.13f, 0f);
                    b.DoubleQuad(p0 - up, p0 + up, p1 + up, p1 - up);
                }
                _headParts.Add(Skin(b, _goggles, "Goggles", Mats.People));
            }
            _eyeLocal = new Vector3(0f, hr * EyeY, hr * 0.55f);
            _gogDown = new Vector3(0, hr * 0.86f, hr * 0.99f);
            _gogUp = look.HasHelmet ? new Vector3(0, hr * 1.62f, hr * 0.97f) : new Vector3(0, hr * 1.56f, hr * 1.03f);
            _goggleUp = _goggleUpTarget = look.GogglesDown ? 0f : 1f;
            PlaceGoggles();
        }

        /// <summary>Helmet or hair (and a beanie, long hair, a braid), in head radii round the head joint.</summary>
        void BuildHeadwear(MeshBuilder b, PersonLook look, int hs, float hr)
        {
            Ring R(float y, float rx, float rz, float oz = 0f) => new Ring(y * hr, rx * hr, rz * hr, oz * hr);
            Vector3 V(float x, float y, float z) => new Vector3(x * hr, y * hr, z * hr);
            // --- headwear and hair -----------------------------------------------------------
            BuildHeadgear(b, look, hr);
            if (look.HasHelmet) BuildHelmet(b, look, hr, hs);
            else
            {
                // Hair cap: sits back from the face, with a hairline above the brows.
                b.Col = look.Hair;
                var hc = new[]
                {
                    R(0.5f, 0.72f, 0.66f, -0.34f),
                    R(0.9f, 0.87f, 0.8f, -0.25f),
                    R(1.3f, 0.89f, 0.9f, -0.15f),
                    R(1.46f, 0.88f, 1.0f, -0.05f),
                    R(1.7f, 0.8f, 0.93f, -0.05f),
                    R(1.9f, 0.58f, 0.68f, -0.06f),
                    R(2.03f, 0.26f, 0.3f, -0.06f),
                    R(2.07f, 0f, 0f, -0.06f),
                };
                b.Loft(hc, hs, true, false);
                if (look.Hero && !look.Beanie)
                {
                    // Strands: long pen lines sweeping back from the hairline and down the sides.
                    for (int k = -3; k <= 3; k++) Ink(b, hc, 1.5f * hr, k * 21f + 4f, 1.98f * hr, k * 34f, 0.03f * hr, 0f, 6);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Ink(b, hc, 1.4f * hr, side * 82f, 0.62f * hr, side * 96f, 0.03f * hr, 0f, 6);
                        Ink(b, hc, 1.5f * hr, side * 118f, 0.6f * hr, side * 132f, 0.03f * hr, 0f, 6);
                        Ink(b, hc, 1.6f * hr, side * 158f, 0.56f * hr, side * 166f, 0.03f * hr, 0f, 6);
                    }
                }
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
                    var lh = new[] { R(-1.4f, 0.32f, 0.12f, -0.82f), R(-0.9f, 0.54f, 0.2f, -0.82f), R(-0.3f, 0.68f, 0.26f, -0.72f), R(0.4f, 0.78f, 0.3f, -0.6f), R(0.9f, 0.8f, 0.3f, -0.52f) };
                    b.Loft(lh, 9, true, true);
                    if (look.Hero)
                        for (int k = -2; k <= 2; k++) Ink(b, lh, -1.25f * hr, 180f + k * 26f, 0.8f * hr, 180f + k * 30f, 0.03f * hr, 0f, 6);
                }
                for (int side = -1; side <= 1; side += 2)   // strands beside the face
                    b.Loft(new[] { new Ring(-0.75f * hr, 0f, 0f, 0.36f * hr, side * 0.7f * hr), new Ring(-0.3f * hr, 0.07f * hr, 0.1f * hr, 0.3f * hr, side * 0.76f * hr), new Ring(0.4f * hr, 0.09f * hr, 0.15f * hr, 0.24f * hr, side * 0.8f * hr), new Ring(1.0f * hr, 0.08f * hr, 0.16f * hr, 0.22f * hr, side * 0.8f * hr) }, 8, false, true);
            }
        }

        /// <summary>
        /// Ski goggles as they are: one wide lens wrapped round the face, flat on top, and along the
        /// bottom two rounded lobes with a notch between them for the nose (a "B" lying on its back).
        /// A foam-backed frame runs round it; the lens carries a tint that shifts from top to bottom.
        /// </summary>
        void BuildGoggles(MeshBuilder b, PersonLook look, float hr, int cols)
        {
            const float halfW = 0.76f, halfH = 0.3f, wrap = 62f * Mathf.Deg2Rad;
            float radius = halfW / Mathf.Sin(wrap);
            // Outline at u (-1 left .. +1 right), scaled by k (1 = frame, less = lens inside it).
            Vector3 Edge(float u, bool top, float k, float forward)
            {
                float au = Mathf.Abs(u);
                float round = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(au, 7f)), 0.42f);            // rounded outer corners
                float notch = Mathf.Exp(-(u * u) / (0.19f * 0.19f));                              // the nose
                float y = top ? halfH * (1f - 0.16f * au * au) * round
                              : -halfH * (0.96f + 0.2f * Mathf.Sin(Mathf.PI * Mathf.Min(au * 1.15f, 1f))) * round + halfH * 1.05f * notch;
                // Wrapped round the face: the depth follows from how far out the point is, so the lens
                // (drawn smaller) stays just in front of the frame all the way across.
                float x = radius * Mathf.Sin(u * wrap) * k;
                float z = Mathf.Sqrt(Mathf.Max(radius * radius - x * x, 0f)) - radius + forward;
                return new Vector3(x, y * (top ? k : Mathf.Lerp(1f, k, 0.9f)), z) * hr;
            }
            Color frame = Color.Lerp(look.Helmet, Palette.Ink, 0.72f);
            Color lensTop = Color.Lerp(look.Goggles, Palette.White, 0.18f), lensLow = Color.Lerp(look.Goggles, Palette.Ink, 0.38f);
            for (int i = 0; i < cols; i++)
            {
                float u0 = Mathf.Lerp(-1f, 1f, i / (float)cols), u1 = Mathf.Lerp(-1f, 1f, (i + 1) / (float)cols);
                // Frame face, then its walls going back to the foam on the face.
                Vector3 t0 = Edge(u0, true, 1f, 0f), t1 = Edge(u1, true, 1f, 0f), b0 = Edge(u0, false, 1f, 0f), b1 = Edge(u1, false, 1f, 0f);
                b.Col = frame;
                b.DoubleQuad(b0, t0, t1, b1);
                Vector3 back = new Vector3(0f, 0f, -0.17f * hr);
                b.DoubleQuad(t0, t0 * 0.97f + back, t1 * 0.97f + back, t1);
                b.DoubleQuad(b0 * 0.97f + back, b0, b1, b1 * 0.97f + back);
                // Lens, a little proud of the frame, darker toward the bottom.
                float l0 = u0 * 0.9f, l1 = u1 * 0.9f;
                Vector3 lt0 = Edge(l0, true, 0.84f, 0.03f), lt1 = Edge(l1, true, 0.84f, 0.03f), lb0 = Edge(l0, false, 0.84f, 0.03f), lb1 = Edge(l1, false, 0.84f, 0.03f);
                Vector3 lm0 = (lt0 + lb0) * 0.5f, lm1 = (lt1 + lb1) * 0.5f;
                b.Col = lensTop; b.DoubleQuad(lm0, lt0, lt1, lm1);
                b.Col = lensLow; b.DoubleQuad(lb0, lm0, lm1, lb1);
            }
            // Reflections: two slanted streaks of light across one lobe.
            b.Col = Color.Lerp(look.Goggles, Palette.White, 0.75f);
            for (int k = 0; k < 2; k++)
            {
                float u = -0.62f + k * 0.2f, wid = k == 0 ? 0.09f : 0.045f;
                Vector3 p0 = Edge(u, false, 0.62f, 0.04f), p1 = Edge(u + 0.2f, true, 0.7f, 0.04f), p2 = Edge(u + 0.2f + wid, true, 0.7f, 0.04f), p3 = Edge(u + wid, false, 0.62f, 0.04f);
                b.DoubleQuad(p0, p1, p2, p3);
            }
            // Outriggers: where the strap takes hold of the frame.
            b.Col = frame;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 at = Edge(side * 0.97f, true, 1f, 0f); at.y = 0f;
                b.Box(at + new Vector3(side * 0.04f * hr, 0f, -0.12f * hr), new Vector3(0.1f, 0.3f, 0.26f) * hr);
            }
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
                b.Sphere(Vector3.zero, V(0.185f, 0.125f, 0.06f), 10, 6);
                b.Col = Palette.Ink;                                                    // dark rim round the iris
                b.Sphere(V(0, -0.005f, 0.03f), V(0.108f, 0.116f, 0.038f), 10, 5);
                b.Col = look.Eyes;
                b.Sphere(V(0, -0.005f, 0.04f), V(0.09f, 0.098f, 0.036f), 10, 5);
                b.Col = Color.Lerp(look.Eyes, Palette.White, 0.45f);                    // lighter lower half, as painted eyes have
                b.Sphere(V(0, -0.04f, 0.048f), V(0.06f, 0.045f, 0.03f), 6, 4);
                b.Col = Palette.Ink;
                b.Sphere(V(0, -0.005f, 0.06f), V(0.042f, 0.048f, 0.024f), 6, 4);
                b.Col = Palette.White;
                b.Sphere(V(0.036f, 0.034f, 0.078f), V(0.022f, 0.022f, 0.011f), 4, 3);
                // Lower lash line and the corner of the eye.
                b.Col = Palette.Ink;
                b.Box(V(side * 0.03f, -0.118f, 0.03f), V(0.24f, look.Female ? 0.022f : 0.016f, 0.04f));
                b.Box(V(side * 0.165f, -0.02f, 0.02f), V(0.03f, 0.1f, 0.04f));
                _headParts.Add(Skin(b, eye, "Eye", Mats.PropsNoOutline));

                // Upper lid: slides down over the eye to blink, lifts when surprised.
                var lid = Node(side < 0 ? "LidL" : "LidR", _head, Vector3.zero);
                b.Col = Color.Lerp(look.Skin, Palette.SkinDark, 0.25f);
                b.Sphere(Vector3.zero, V(0.2f, 0.14f, 0.082f), 10, 6);
                b.Col = Palette.Ink;
                b.Box(V(0, -0.09f, 0.06f), V(look.Female ? 0.42f : 0.38f, look.Female ? 0.06f : 0.04f, 0.04f));     // lash line
                b.Push(V(side * 0.22f, -0.06f, 0.05f), Quaternion.Euler(0, 0, side * 30f));                             // its outer flick
                b.Box(Vector3.zero, V(look.Female ? 0.17f : 0.1f, look.Female ? 0.045f : 0.03f, 0.03f));
                b.Pop();
                b.Box(V(0, 0.075f, 0.075f), V(0.3f, 0.016f, 0.03f));                                                    // crease of the lid
                _headParts.Add(Skin(b, lid, "Lid", Mats.PropsNoOutline));
                if (side < 0) _lidL = lid; else _lidR = lid;

                var brow = Node(side < 0 ? "BrowL" : "BrowR", _head, Vector3.zero);
                // Brows drawn like brush strokes: heavy at the nose, arching, thinning to a point.
                b.Col = Color.Lerp(look.Hair, Palette.Ink, 0.82f);
                float th = look.Female ? 0.06f : 0.11f;
                b.Push(V(-side * 0.13f, -0.01f, 0.005f), Quaternion.Euler(0, 0, side * 10f));
                b.Box(Vector3.zero, V(0.16f, th, 0.05f));
                b.Pop();
                b.Box(V(side * 0.01f, 0.012f, 0), V(0.16f, th * 0.85f, 0.05f));
                b.Push(V(side * 0.15f, -0.012f, -0.02f), Quaternion.Euler(0, side * 18f, -side * 20f));
                b.Box(Vector3.zero, V(0.17f, th * 0.55f, 0.045f));
                b.Pop();
                _headParts.Add(Skin(b, brow, "Brow", Mats.PropsNoOutline));
                if (side < 0) _browL = brow; else _browR = brow;
            }
            // Mouth: a dark interior with teeth that opens downward, and a lower lip that follows it.
            _mouth = Node("Mouth", _head, V(0, MouthY, MouthZ));
            b.Col = Palette.Hex("#4A1A2A");
            b.Sphere(V(0, -0.13f, 0), V(0.2f, 0.13f, 0.05f), 10, 6);
            b.Col = Palette.White;
            b.Box(V(0, -0.05f, 0.018f), V(0.16f, 0.05f, 0.03f));
            _headParts.Add(Skin(b, _mouth, "Mouth", Mats.PropsNoOutline));
            _lipLow = Node("LipLow", _head, Vector3.zero);
            b.Col = lip;
            b.Sphere(Vector3.zero, V(0.15f, look.Female ? 0.052f : 0.04f, 0.055f), 8, 4);
            _headParts.Add(Skin(b, _lipLow, "Lip", Mats.PropsNoOutline));
            AnimateFace(0f);
        }

        // ---- sculpted bodies -------------------------------------------------------------------

        void OnDestroy()
        {
            foreach (var m in _ownMeshes) if (m != null) Destroy(m);
        }

        /// <summary>
        /// The body from Blender: skin, jacket, pants and gloves bound to the same joints the simple
        /// figure uses, so every pose works on both. Headwear, goggles, boots, skis and poles are
        /// still built here and fitted to the sculpted head and hands.
        /// </summary>
        void BuildSkinned(PersonLook look)
        {
            var m = _model;
            var J = m.Joints;
            float k = look.Height / m.Height;
            float s = _s = look.Height / 1.75f;
            _hr = m.HeadRadius * k;
            _ankle = 0.1f * s;
            _upperArm = (J[SkinModel.ElbowL] - J[SkinModel.ShoulderL]).magnitude * k;
            _forearm = (J[SkinModel.HandL] - J[SkinModel.ElbowL]).magnitude * k;
            _thigh = (J[SkinModel.KneeL] - J[SkinModel.HipL]).magnitude * k;
            _shin = (J[SkinModel.FootL] - J[SkinModel.KneeL]).magnitude * k;
            _torsoLen = (J[SkinModel.Head].y - J[SkinModel.Torso].y) * k;
            _hipX = Mathf.Abs(J[SkinModel.HipL].x) * k;
            _hipDrop = (J[SkinModel.Hips].y - J[SkinModel.HipL].y) * k;
            _seed = (++_count & 1023) * 0.37f;
            var b = new MeshBuilder();

            // Joints, placed as the sculpt stands (arms out a little, legs apart): its bind pose.
            _hips = Node("Hips", transform, J[SkinModel.Hips] * k);
            _torso = Node("Torso", _hips, (J[SkinModel.Torso] - J[SkinModel.Hips]) * k);
            _head = Node("Head", _torso, (J[SkinModel.Head] - J[SkinModel.Torso]) * k);
            Transform Limb(string name, Transform parent, int root, int mid, int end, out Transform midT, out Transform endT, bool levelEnd)
            {
                var a = Node(name, parent, (J[root] - J[parent == _torso ? SkinModel.Torso : SkinModel.Hips]) * k);
                Quaternion q0 = Quaternion.FromToRotation(Vector3.down, J[mid] - J[root]);
                a.localRotation = q0;
                midT = Node(name + "Mid", a, Vector3.down * ((J[mid] - J[root]).magnitude * k));
                Quaternion q1 = Quaternion.FromToRotation(Vector3.down, Quaternion.Inverse(q0) * (J[end] - J[mid]));
                midT.localRotation = q1;
                endT = Node(name + "End", midT, Vector3.down * ((J[end] - J[mid]).magnitude * k));
                if (levelEnd) endT.localRotation = Quaternion.Inverse(q0 * q1);     // feet stand flat
                return a;
            }
            _shL = Limb("ArmL", _torso, SkinModel.ShoulderL, SkinModel.ElbowL, SkinModel.HandL, out _elL, out _handL, false);
            _shR = Limb("ArmR", _torso, SkinModel.ShoulderR, SkinModel.ElbowR, SkinModel.HandR, out _elR, out _handR, false);
            _hipL = Limb("LegL", _hips, SkinModel.HipL, SkinModel.KneeL, SkinModel.FootL, out _kneeL, out _footL, true);
            _hipR = Limb("LegR", _hips, SkinModel.HipR, SkinModel.KneeR, SkinModel.FootR, out _kneeR, out _footR, true);
            var bones = new[] { _hips, _torso, _head, _shL, _elL, _handL, _shR, _elR, _handR, _hipL, _kneeL, _footL, _hipR, _kneeR, _footR };
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * transform.localToWorldMatrix;

            // Each character paints the sculpt's colour slots its own way.
            var slots = new Color[SkinModel.SlotCount];
            slots[SkinModel.SlotSkin] = look.Skin;
            slots[SkinModel.SlotLip] = Color.Lerp(look.Skin, Palette.Hex("#C0392B"), look.Female ? 0.62f : 0.2f);
            slots[SkinModel.SlotMouth] = Palette.Hex("#4A1A2A");
            slots[SkinModel.SlotBrow] = Color.Lerp(look.Hair, Palette.Ink, 0.7f);
            slots[SkinModel.SlotJacket] = look.Jacket; slots[SkinModel.SlotTrim] = look.Trim;
            slots[SkinModel.SlotPants] = look.Pants; slots[SkinModel.SlotGlove] = look.Gloves;
            slots[SkinModel.SlotInk] = Palette.Ink; slots[SkinModel.SlotTeeth] = Palette.White;
            float chinY = J[SkinModel.Head].y - 0.25f * m.HeadRadius, earZ = m.EarL.z;
            Color beard = Color.Lerp(look.Skin, look.Hair, 0.34f);
            float hipY = J[SkinModel.Hips].y;
            System.Func<Vector3, int, Color, Color> tint = null;
            if (look.Stubble)
                tint = (pos, slot, col) =>
                {
                    if (slot != SkinModel.SlotSkin) return col;
                    float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(m.Nose.y + 0.004f, m.Nose.y - 0.022f, pos.y))
                            * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(earZ - 0.005f, earZ + 0.035f, pos.z))
                            * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(chinY - 0.07f, chinY - 0.025f, pos.y));
                    return Color.Lerp(col, beard, w);
                };
            foreach (var part in m.Parts)
            {
                bool skin = part.Name == "skin";
                var go = new GameObject(part.Name);
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SkinnedMeshRenderer>();
                r.skinnedMotionVectors = false;
                r.quality = look.Hero ? SkinQuality.Bone4 : SkinQuality.Bone2;
                if (!look.Hero && GraphicsQuality.Light) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // A printed pattern: the jacket (and for some prints the pants) gets its own textured material.
                int pattern = look.Pattern;
                bool prints = !skin && pattern > 0 && (part.Name == "jacket" || part.Name == "pants");
                System.Func<int, bool> printed = prints ? (slot => OutfitPaint.Prints(pattern, slot)) : (System.Func<int, bool>)null;
                System.Func<Vector3, int, Vector2> printUv = prints ? ((pos, slot) => OutfitPaint.Uv(pattern, pos, slot, hipY)) : (System.Func<Vector3, int, Vector2>)null;
                var mesh = m.Build(part, slots, k, bind, skin && look.Hero, tint, printed, printUv);
                _ownMeshes.Add(mesh);
                r.sharedMesh = mesh;
                r.bones = bones;
                r.rootBone = _hips;
                r.localBounds = new Bounds(new Vector3(0f, 0.1f * s, 0f), new Vector3(2.4f, 2.6f, 2.4f) * s);
                r.sharedMaterial = look.Hero ? (skin ? Mats.CharacterSkin : Mats.CharacterCloth) : (skin ? Mats.CrowdSkin : Mats.CrowdCloth);
                if (m.Texture != null && part.Uv != null && !prints) r.sharedMaterial = Mats.Painted(r.sharedMaterial, m.Texture);
                if (prints)
                {
                    bool legs = part.Name == "pants";
                    r.sharedMaterials = new[] { r.sharedMaterial, OutfitPaint.Material(r.sharedMaterial, pattern, legs ? look.Pants : look.Jacket, look.Trim) };
                }
                if (skin)
                {
                    _headParts.Add(r);
                    if (look.Hero && mesh.blendShapeCount > 0)
                    {
                        _face = r;
                        _kBlink = mesh.GetBlendShapeIndex("Blink"); _kMouth = mesh.GetBlendShapeIndex("MouthOpen"); _kSmile = mesh.GetBlendShapeIndex("Smile");
                        _kBrowUp = mesh.GetBlendShapeIndex("BrowUp"); _kBrowDown = mesh.GetBlendShapeIndex("BrowDown");
                    }
                }
            }

            // Poles, in the closed hands.
            if (look.Poles && look.Gear == GearKind.Skis)
                for (int side = -1; side <= 1; side += 2)
                {
                    var hand = side < 0 ? _handL : _handR;
                    Vector3 grip = new Vector3(m.GripL.x * -side, m.GripL.y, m.GripL.z) * k;
                    var pole = Node("Pole", hand, hand.InverseTransformPoint(transform.TransformPoint(grip)));
                    pole.localRotation = Quaternion.Euler(-62f, 0f, 0f);
                    b.Push(new Vector3(0f, 0.045f * s, 0f));          // the fist closes below the top of the grip
                    GearModels.Pole(b, 1.12f * s, look.GearAccent);
                    b.Pop();
                    Skin(b, pole, "Pole", Mats.Props);
                    _poles.Add(pole); _poleHome.Add(pole.localPosition);
                }

            // Boots, skis, board.
            for (int side = -1; side <= 1; side += 2)
            {
                var foot = side < 0 ? _footL : _footR;
                b.Push(new Vector3(0, -_ankle, 0), Quaternion.identity, Vector3.one * s);
                if (look.Gear == GearKind.Board) GearModels.BoardBoot(b, look.Boots); else GearModels.Boot(b, look.Boots);
                b.Pop();
                if (look.Gear == GearKind.Skis)
                {
                    b.Push(new Vector3(0, -_ankle - 0.022f, 0));
                    GearModels.Ski(b, look.GearLength, look.TwinTip, look.GearColour, look.GearAccent, 0.085f * Mathf.Lerp(s, 1f, 0.5f), look.GearDesign);
                    b.Pop();
                }
                Skin(b, Node("Boot", foot, Vector3.zero), "Foot", Mats.Props);
            }
            if (look.Gear == GearKind.Board)
            {
                _board = Node("Board", _footL, Vector3.zero);
                GearModels.Board(b, look.GearLength, look.GearColour, look.GearAccent, look.GearDesign);
                Skin(b, _board, "Board", Mats.Props);
            }

            BuildSkinnedHead(b, look, k);
        }

        /// <summary>
        /// A ski helmet as they are made: one smooth hard shell, high over the brow where the
        /// goggles sit, sweeping down over the ears and low at the back of the head, with a dark
        /// rim along its edge, vents on top and only the soft ear padding showing underneath.
        /// In head radii, like the head itself.
        /// </summary>
        void BuildHelmet(MeshBuilder b, PersonLook look, float hr, int sides)
        {
            const float Yc = 1.05f, Ry = 1.06f, Rx = 1.04f, Rz = 1.14f, OffZ = -0.05f;
            // How low the shell comes at each angle round the head (0 = the front).
            float Edge(float deg)
            {
                float a = Mathf.Abs(Mathf.DeltaAngle(0f, deg));
                return 1.34f - 0.82f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(34f, 84f, a)) + 0.2f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(112f, 172f, a));
            }
            // Down the shell from the crown: an ellipsoid above its middle, then nearly straight down.
            float PhiOf(float y) => y >= Yc ? Mathf.Acos(Mathf.Clamp((y - Yc) / Ry, -1f, 1f)) : Mathf.PI * 0.5f + (Yc - y) / Ry;
            Vector3 On(float deg, float phi, float grow = 1f)
            {
                float y, k;
                if (phi <= Mathf.PI * 0.5f) { y = Yc + Ry * Mathf.Cos(phi); k = Mathf.Sin(phi); }
                else { float below = (phi - Mathf.PI * 0.5f) * Ry; y = Yc - below; k = 1f - 0.07f * Mathf.Pow(below / 0.55f, 2f); }
                float t = deg * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(t) * Rx * k * grow, y, Mathf.Cos(t) * Rz * k * grow + OffZ) * hr;
            }
            int cols = Mathf.Max(sides * 2, 16), rows = look.Hero ? 14 : 8;
            var outer = new Vector3[rows + 1, cols];
            var inner = new Vector3[2, cols];
            var cOut = new Color[rows + 1, cols];
            var cIn = new Color[2, cols];
            Color shell = look.Helmet, rim = Color.Lerp(look.Helmet, Palette.Ink, 0.78f);
            for (int j = 0; j < cols; j++)
            {
                float deg = j * 360f / cols;
                float phiEdge = PhiOf(Edge(deg));
                for (int i = 0; i <= rows; i++)
                {
                    float f = i / (float)rows;
                    outer[i, j] = On(deg, phiEdge * f);
                    // The last stretch above the edge is the rubber rim.
                    cOut[i, j] = phiEdge * (1f - f) < 0.07f ? rim : shell;
                }
                // The rim turns in under the shell: a band of thickness you see from below.
                inner[0, j] = outer[rows, j];
                inner[1, j] = On(deg, phiEdge - 0.02f, 0.9f);
                cIn[0, j] = cIn[1, j] = rim;
            }
            Vector3 centre = new Vector3(0f, Yc * 0.9f, OffZ) * hr;
            b.Surface(outer, cOut, centre, true);
            b.Surface(inner, cIn, centre + Vector3.down * hr, true);
            if (look.Hero)
            {
                // Vent slots along the top.
                b.Col = rim;
                foreach (float deg in new[] { -24f, 0f, 24f })
                {
                    Vector3 a0 = On(deg - 3.5f, 0.36f, 1.008f), a1 = On(deg + 3.5f, 0.36f, 1.008f), b0 = On(deg - 3.5f, 0.76f, 1.008f), b1 = On(deg + 3.5f, 0.76f, 1.008f);
                    b.DoubleQuad(a0, b0, b1, a1);
                }
            }
            // Soft ear padding, mostly inside the shell, a little showing under its edge.
            b.Col = Color.Lerp(look.Helmet, Palette.Ink, 0.55f);
            for (int side = -1; side <= 1; side += 2)
                b.Sphere(new Vector3(side * 0.9f, 0.5f, -0.06f) * hr, new Vector3(0.1f, 0.2f, 0.27f) * hr, 8, 5);
            // The goggle clip at the back.
            b.Col = rim;
            b.Box(On(180f, PhiOf(1.1f), 1.01f), new Vector3(0.24f, 0.09f, 0.05f) * hr);
            // Chin strap, from under each ear to below the chin.
            b.Col = Palette.Ink;
            float ear = PhiOf(Edge(90f)) - 0.05f;
            Vector3 l = On(-90f, ear, 0.97f), r = On(90f, ear, 0.97f);
            Vector3 cl = new Vector3(-0.24f, -0.2f, 0.36f) * hr, cr = new Vector3(0.24f, -0.2f, 0.36f) * hr;
            b.Tube(l, cl, 0.028f * hr, 4); b.Tube(r, cr, 0.028f * hr, 4); b.Tube(cl, cr, 0.028f * hr, 4);
        }

        /// <summary>The sillier things from the shop, added to the helmet (or the hair). In head radii.</summary>
        void BuildHeadgear(MeshBuilder b, PersonLook look, float hr)
        {
            Vector3 V(float x, float y, float z) => new Vector3(x, y, z) * hr;
            switch (look.Headgear)
            {
                case 2: // Viking: two horns curling out and up from the sides
                    b.Col = Palette.Hex("#EFE6CF");
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 prev = V(side * 0.86f, 1.3f, -0.05f);
                        for (int i = 1; i <= 7; i++)
                        {
                            float t = i / 7f;
                            Vector3 p = V(side * (0.9f + 0.85f * t), 1.25f + 0.35f * t + 1.1f * t * t, -0.05f + 0.35f * t);
                            b.Tube(prev, p, Mathf.Lerp(0.3f, 0.03f, (i - 1) / 7f) * hr, Mathf.Lerp(0.3f, 0.03f, t) * hr, 10, i == 7);
                            prev = p;
                        }
                    }
                    b.Col = Palette.Hex("#C8A040");
                    b.Box(V(0f, 1.95f, 0.1f), V(0.1f, 0.3f, 0.9f));                       // a ridge over the top
                    break;
                case 3: // goalie: a white wire cage over the face
                    b.Col = Palette.Hex("#F7F4EE");
                    float R = 1.14f;
                    for (float x = -0.5f; x <= 0.51f; x += 0.2f)
                    {
                        float z = Mathf.Sqrt(R * R - x * x) - 0.04f;
                        b.Tube(V(x, 1.3f, z - 0.06f), V(x, -0.18f, z - 0.12f), 0.026f * hr, 4);
                    }
                    for (int k = 0; k < 4; k++)
                    {
                        float y = 1.1f - k * 0.4f;
                        Vector3 prev = default;
                        for (int i = 0; i <= 8; i++)
                        {
                            float a = Mathf.Lerp(-62f, 62f, i / 8f) * Mathf.Deg2Rad;
                            Vector3 p = V(Mathf.Sin(a) * R, y, Mathf.Cos(a) * R - 0.06f - k * 0.02f);
                            if (i > 0) b.Tube(prev, p, 0.026f * hr, 4);
                            prev = p;
                        }
                    }
                    b.Col = Palette.Ink;
                    b.Sphere(V(-0.78f, 0.45f, 0.55f), 0.05f * hr, 6, 4); b.Sphere(V(0.78f, 0.45f, 0.55f), 0.05f * hr, 6, 4);   // the clips
                    break;
                case 4: // boxer: padded cheeks and brow
                    b.Col = look.Helmet;
                    b.Sphere(V(-0.66f, 0.38f, 0.5f), V(0.18f, 0.34f, 0.24f), 10, 6);
                    b.Sphere(V(0.66f, 0.38f, 0.5f), V(0.18f, 0.34f, 0.24f), 10, 6);
                    b.Sphere(V(0f, 1.32f, 0.82f), V(0.58f, 0.15f, 0.2f), 12, 6);
                    b.Col = Palette.White;
                    b.Box(V(0f, 1.32f, 1.0f), V(0.5f, 0.05f, 0.05f));
                    break;
                case 5: // crown: gold band, points and jewels, on the hair
                {
                    Color gold = Palette.Hex("#E8B93A");
                    b.Col = gold;
                    b.Cylinder(V(0f, 1.62f, -0.05f), 0.66f * hr, 0.7f * hr, 0.28f * hr, 16, true, false);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI * 2f / 8f;
                        Vector3 bottom = V(Mathf.Sin(a) * 0.7f, 1.88f, Mathf.Cos(a) * 0.7f - 0.05f);
                        b.Tube(bottom, bottom + V(Mathf.Sin(a) * 0.06f, 0.34f, Mathf.Cos(a) * 0.06f), 0.1f * hr, 0.015f * hr, 5, true);
                        b.Col = i % 2 == 0 ? Palette.SignalRed : Palette.Hex("#3A78E0");
                        b.Sphere(V(Mathf.Sin(a) * 0.71f, 1.76f, Mathf.Cos(a) * 0.71f - 0.05f), 0.07f * hr, 6, 4);
                        b.Col = gold;
                    }
                    break;
                }
                case 6: // unicorn: a twisted horn and a rainbow mane
                {
                    b.Col = Palette.Hex("#F3D9F0");
                    Vector3 root = V(0f, 1.5f, 0.88f), dir = new Vector3(0f, 0.8f, 0.6f).normalized;
                    for (int i = 0; i < 6; i++)
                    {
                        float t0 = i / 6f, t1 = (i + 1) / 6f;
                        b.Col = i % 2 == 0 ? Palette.Hex("#F3D9F0") : Palette.Hex("#E8B93A");
                        b.Tube(root + dir * (t0 * 1.5f * hr), root + dir * (t1 * 1.5f * hr), Mathf.Lerp(0.24f, 0.02f, t0) * hr, Mathf.Lerp(0.24f, 0.02f, t1) * hr, 8, i == 5);
                    }
                    Color[] rainbow = { Palette.Hex("#E8455A"), Palette.Hex("#F2803A"), Palette.Hex("#FFC64A"), Palette.Hex("#8FD14F"), Palette.Hex("#3A78E0"), Palette.Hex("#6C58C9") };
                    for (int i = 0; i < rainbow.Length; i++)
                    {
                        b.Col = rainbow[i];
                        float x = (i - 2.5f) * 0.09f;
                        Vector3 a0 = V(x, 2.0f, -0.1f), a1 = V(x * 1.4f, 1.6f, -0.95f), a2 = V(x * 1.6f, 0.6f, -1.1f), a3 = V(x * 1.7f, -0.3f, -0.95f);
                        b.Tube(a0, a1, 0.08f * hr, 4); b.Tube(a1, a2, 0.08f * hr, 4); b.Tube(a2, a3, 0.08f * hr, 0.03f * hr, 4, true);
                    }
                    break;
                }
            }
            switch (look.Headgear)
            {
                case 7: // bunny ears, standing up from the beanie, pink inside
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var rot = Quaternion.Euler(0f, 0f, side * -12f);
                        Vector3 root = V(side * 0.35f, 2.0f, -0.1f);
                        b.Col = look.Helmet;
                        b.Push(root, rot);
                        b.Sphere(new Vector3(0f, 0.55f, 0f) * hr, new Vector3(0.2f, 0.62f, 0.09f) * hr, 10, 6);
                        b.Col = Palette.Hex("#F7A8C8");
                        b.Sphere(new Vector3(0f, 0.55f, 0.06f) * hr, new Vector3(0.12f, 0.48f, 0.05f) * hr, 8, 5);
                        b.Pop();
                    }
                    break;
                case 8: // snapback cap: a flat, stiff brim sticking out over the face
                    b.Col = look.Helmet;
                    b.Sphere(V(0f, 1.5f, -0.06f), V(0.88f, 0.52f, 0.97f), 14, 6);
                    b.Col = Color.Lerp(look.Helmet, Palette.Ink, 0.35f);
                    b.Push(V(0f, 1.36f, 1.12f), Quaternion.Euler(-6f, 0f, 0f));
                    b.Box(Vector3.zero, V(1.15f, 0.07f, 0.75f));                                         // the brim
                    b.Pop();
                    b.Col = Palette.White;
                    b.Box(V(0f, 1.68f, 0.86f), V(0.46f, 0.22f, 0.05f));                                  // a patch on the front
                    b.Col = Palette.Ink;
                    b.Sphere(V(0f, 2.01f, -0.06f), 0.07f * hr, 6, 4);                                    // the button on top
                    break;
                case 9: // aero racing helmet: a long tail and a tinted visor over the eyes
                    b.Col = look.Helmet;
                    b.Tube(V(0f, 1.45f, -0.7f), V(0f, 1.2f, -1.6f), 0.5f * hr, 0.08f * hr, 10, true);
                    b.Col = Color.Lerp(look.Goggles, Palette.Ink, 0.3f);
                    for (int i = 0; i < 10; i++)
                    {
                        float a0 = Mathf.Lerp(-70f, 70f, i / 10f) * Mathf.Deg2Rad, a1 = Mathf.Lerp(-70f, 70f, (i + 1) / 10f) * Mathf.Deg2Rad;
                        Vector3 p0 = V(Mathf.Sin(a0) * 1.08f, 0f, Mathf.Cos(a0) * 1.12f), p1 = V(Mathf.Sin(a1) * 1.08f, 0f, Mathf.Cos(a1) * 1.12f);
                        Vector3 lo = V(0f, 0.62f, 0f), hi = V(0f, 1.32f, 0f);
                        b.DoubleQuad(p0 + lo, p0 + hi, p1 + hi, p1 + lo);
                    }
                    break;
                case 10: // devil horns, small, red, curling forward
                    b.Col = Palette.Hex("#C8242E");
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 prev = V(side * 0.45f, 1.9f, 0.35f);
                        for (int i = 1; i <= 5; i++)
                        {
                            float t = i / 5f;
                            Vector3 p = V(side * (0.45f + 0.18f * t), 1.9f + 0.45f * t, 0.35f + 0.2f * t * t);
                            b.Tube(prev, p, Mathf.Lerp(0.16f, 0.02f, (i - 1) / 5f) * hr, Mathf.Lerp(0.16f, 0.02f, t) * hr, 8, i == 5);
                            prev = p;
                        }
                    }
                    break;
                case 11: // antlers: branching, wide
                    b.Col = Palette.Hex("#8A5A34");
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 a = V(side * 0.6f, 1.8f, -0.1f), bb = V(side * 1.4f, 2.5f, -0.2f), c = V(side * 2.0f, 3.1f, -0.1f);
                        b.Tube(a, bb, 0.09f * hr, 0.07f * hr, 6); b.Tube(bb, c, 0.07f * hr, 0.03f * hr, 6, true);
                        b.Tube(bb, bb + V(side * 0.05f, 0.7f, 0.15f), 0.05f * hr, 0.02f * hr, 6, true);
                        b.Tube(Vector3.Lerp(a, bb, 0.5f), Vector3.Lerp(a, bb, 0.5f) + V(side * -0.1f, 0.6f, 0.2f), 0.05f * hr, 0.02f * hr, 6, true);
                        b.Tube(c, c + V(side * 0.35f, 0.4f, 0f), 0.03f * hr, 0.015f * hr, 6, true);
                    }
                    break;
            }
            if (look.Suit == 2 && look.Headgear == 0)
            {
                // The banana's stalk on top of the hat.
                b.Col = Palette.Hex("#5A4020");
                b.Tube(V(0f, 2.15f, -0.12f), V(0.05f, 2.55f, -0.05f), 0.11f * hr, 0.08f * hr, 6, true);
            }
        }

        /// <summary>The simple head at a few heights, in head radii: height, half width, half depth, offset forward.</summary>
        static readonly float[,] Egg = { { 0.55f, 0.76f, 0.84f, 0.03f }, { 0.85f, 0.79f, 0.9f, 0f }, { 1.1f, 0.81f, 0.93f, -0.02f }, { 1.4f, 0.79f, 0.91f, -0.04f }, { 1.65f, 0.66f, 0.79f, -0.05f } };

        /// <summary>Eyes, headwear and goggles on the sculpted head.</summary>
        void BuildSkinnedHead(MeshBuilder b, PersonLook look, float k)
        {
            var m = _model;
            float hr = _hr;
            Vector3 headJ = m.Joints[SkinModel.Head];
            Vector3 H(Vector3 modelPoint) => (modelPoint - headJ) * k;

            // Eyes: balls in the sockets, so the lids really close over them.
            float er = m.EyeRadius * k;
            int seg = look.Hero ? 16 : 8;
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = Node(side < 0 ? "EyeL" : "EyeR", _head, H(side < 0 ? m.EyeL : m.EyeR));
                b.Col = Palette.White;
                b.Sphere(Vector3.zero, er, seg, seg / 2 + 1);
                b.Col = Palette.Ink;                                                    // dark rim round the iris
                b.Sphere(new Vector3(0, 0, 0.5f) * er, new Vector3(0.7f, 0.7f, 0.52f) * er, seg, 5);
                b.Col = look.Eyes;
                b.Sphere(new Vector3(0, 0, 0.56f) * er, new Vector3(0.6f, 0.6f, 0.47f) * er, seg, 5);
                if (look.Hero)
                {
                    b.Col = Color.Lerp(look.Eyes, Palette.White, 0.4f);                 // lighter lower half, as painted eyes have
                    b.Sphere(new Vector3(0, -0.2f, 0.62f) * er, new Vector3(0.36f, 0.24f, 0.42f) * er, 8, 4);
                }
                b.Col = Palette.Ink;
                b.Sphere(new Vector3(0, 0, 0.78f) * er, new Vector3(0.27f, 0.27f, 0.27f) * er, 8, 4);
                if (look.Hero)
                {
                    b.Col = Palette.White;
                    b.Sphere(new Vector3(0.17f, 0.2f, 0.95f) * er, new Vector3(0.085f, 0.085f, 0.09f) * er, 5, 3);
                }
                eye.localRotation = Quaternion.Euler(9f, side * 2f, 0f);          // the lids sit low on the ball: look where they open
                var eyeR = Skin(b, eye, "Eye", Mats.Eyes);
                eyeR.receiveShadows = false;
                _headParts.Add(eyeR);
            }
            float eyeY = (m.EyeL.y - headJ.y) * k;
            _eyeLocal = new Vector3(0f, eyeY, H(m.EyeL).z - 0.03f * k);

            // The headwear was drawn for an egg-shaped head. Stretch it so that egg encloses this skull.
            float wx = 0f, wf = 0f, wb = 0f;
            for (int i = 0; i < Egg.GetLength(0); i++)
            {
                float y = Egg[i, 0] * m.HeadRadius, unit = m.HeadRadius;
                wx = Mathf.Max(wx, m.HalfWidth(y) / (unit * Egg[i, 1]));
                wf = Mathf.Max(wf, m.Front(y) / (unit * (Egg[i, 3] + Egg[i, 2])));
                wb = Mathf.Max(wb, -m.Back(y) / (unit * (Egg[i, 2] - Egg[i, 3])));
            }
            float wz = (wf + wb) * 0.5f, dz = (wf - wb) * 0.5f * 0.9f * hr;
            var fit = Matrix4x4.TRS(new Vector3(0f, 0f, dz), Quaternion.identity, new Vector3(wx, 1f, wz));
            b.Push(fit);
            BuildHeadwear(b, look, look.Hero ? 16 : 9, hr);
            b.Pop();
            _headParts.Add(Skin(b, Node("Headwear", _head, Vector3.zero), "Headwear"));

            // Goggles: as wide as the face at the eyes, resting on the brow and cheekbones.
            _goggles = Node("Goggles", _head, Vector3.zero);
            float eyeHr = eyeY / hr;
            float faceHalf = m.HalfWidth(eyeHr * m.HeadRadius) * k, faceFront = m.Front(eyeHr * m.HeadRadius) * k;
            _gogDown = new Vector3(0f, eyeY + 0.02f * hr, faceFront + 0.075f * hr);
            _gogUp = fit.MultiplyPoint3x4(look.HasHelmet ? new Vector3(0, hr * 1.62f, hr * 0.97f) : new Vector3(0, hr * 1.64f, hr * 1.0f));
            if (look.HasGoggles)
            {
                float gw = faceHalf / (0.76f * hr);
                b.Push(Matrix4x4.Scale(new Vector3(gw, 1f, 1f)));
                BuildGoggles(b, look, hr, look.Hero ? 26 : 14);
                b.Pop();
                // The strap: from one outrigger round the back of the head (or helmet) to the other.
                b.Col = Color.Lerp(look.Goggles, Palette.Ink, 0.35f);
                float pad = look.HasHelmet ? 1f : 0.84f;
                const int n = 18;
                Vector3 On(float deg)
                {
                    float a = deg * Mathf.Deg2Rad;
                    Vector3 p = fit.MultiplyPoint3x4(new Vector3(Mathf.Sin(a) * 1.07f * pad, 0f, Mathf.Cos(a) * 1.16f * pad - 0.03f) * hr);
                    return new Vector3(p.x, 0f, p.z - _gogDown.z);
                }
                for (int i = 0; i < n; i++)
                {
                    Vector3 p0 = On(Mathf.Lerp(74f, 286f, i / (float)n)), p1 = On(Mathf.Lerp(74f, 286f, (i + 1) / (float)n));
                    Vector3 up = new Vector3(0f, 0.13f * hr, 0f);
                    b.DoubleQuad(p0 - up, p0 + up, p1 + up, p1 - up);
                }
                _headParts.Add(Skin(b, _goggles, "Goggles", Mats.People));
            }
            _goggleUp = _goggleUpTarget = look.GogglesDown ? 0f : 1f;
            PlaceGoggles();
            if (_face != null) AnimateFace(0f);
        }

        void PlaceGoggles()
        {
            float k = _goggleUp;
            _goggles.localPosition = Vector3.Lerp(_gogDown, _gogUp, k);
            _goggles.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -26f, k), 0f, 0f);
        }

        // ---- limbs -----------------------------------------------------------------------------

        void BuildArms(MeshBuilder b, PersonLook look, float s, int sides)
        {
            float shx = (look.Female && !look.Kid ? 0.192f : 0.207f) * s;
            for (int side = -1; side <= 1; side += 2)
            {
                var sh = Node(side < 0 ? "ShoulderL" : "ShoulderR", _torso, new Vector3(side * shx, 0.43f * s, 0));
                b.Col = look.Jacket;
                var ua = new[]
                {
                    new Ring(-_upperArm, 0.054f * s, 0.056f * s),
                    new Ring(-_upperArm * 0.6f, 0.062f * s, 0.064f * s),
                    new Ring(-_upperArm * 0.22f, 0.072f * s, 0.073f * s),
                    new Ring(0f, 0.07f * s, 0.07f * s),
                };
                b.Loft(ua, sides, false, false);
                if (look.Hero)
                {
                    Ink(b, ua, -_upperArm * 0.86f, -60f, -_upperArm * 0.86f, 60f, 0.011f * s, 0.016f * s);
                    Ink(b, ua, -_upperArm * 0.74f, -40f, -_upperArm * 0.76f, 50f, 0.009f * s, 0.012f * s);
                    Ink(b, ua, -_upperArm * 0.3f, side * 80f, -_upperArm * 0.62f, side * 30f, 0.01f * s, 0f);
                }
                b.Sphere(new Vector3(0, -_upperArm, 0), 0.056f * s, sides, 5);            // elbow
                b.Col = look.Trim;
                b.Sphere(Vector3.zero, new Vector3(0.078f, 0.074f, 0.08f) * s, sides, 5);   // shoulder cap
                Skin(b, sh, "UpperArm");

                var el = Node(side < 0 ? "ElbowL" : "ElbowR", sh, new Vector3(0, -_upperArm, 0));
                b.Col = look.Jacket;
                var fa = new[]
                {
                    new Ring(-_forearm, 0.047f * s, 0.049f * s),
                    new Ring(-_forearm * 0.55f, 0.057f * s, 0.058f * s),
                    new Ring(0f, 0.055f * s, 0.056f * s),
                };
                b.Loft(fa, sides, false, false);
                if (look.Hero)
                {
                    Ink(b, fa, -_forearm * 0.12f, -70f, -_forearm * 0.14f, 70f, 0.011f * s, -0.014f * s);
                    Ink(b, fa, -_forearm * 0.3f, side * 100f, -_forearm * 0.72f, side * 60f, 0.009f * s, 0f);
                }
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
                    _poles.Add(pole); _poleHome.Add(pole.localPosition);
                }
            }
        }

        void BuildLegs(MeshBuilder b, PersonLook look, float s, int sides)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = Node(side < 0 ? "HipL" : "HipR", _hips, new Vector3(side * 0.095f * s, -0.04f * s, 0));
                b.Col = look.Pants;
                var th = new[]
                {
                    new Ring(-_thigh, 0.07f * s, 0.075f * s),
                    new Ring(-_thigh * 0.55f, 0.083f * s, 0.087f * s),
                    new Ring(-_thigh * 0.15f, 0.093f * s, 0.096f * s),
                    new Ring(0f, 0.088f * s, 0.092f * s),
                };
                b.Loft(th, sides, false, false);
                if (look.Hero)
                {
                    Ink(b, th, -_thigh * 0.88f, -70f, -_thigh * 0.88f, 70f, 0.012f * s, 0.02f * s);
                    Ink(b, th, -_thigh * 0.76f, -45f, -_thigh * 0.78f, 55f, 0.01f * s, 0.016f * s);
                    Ink(b, th, -_thigh * 0.1f, side * 85f, -_thigh * 0.3f, side * 5f, 0.012f * s, 0f);
                    Ink(b, th, -_thigh * 0.3f, -side * 60f, -_thigh * 0.62f, -side * 20f, 0.01f * s, 0f);
                }
                b.Sphere(Vector3.zero, 0.09f * s, sides, 5);
                b.Sphere(new Vector3(0, -_thigh, 0.004f * s), 0.074f * s, sides, 5);   // knee
                Skin(b, hip, "Thigh");

                var knee = Node(side < 0 ? "KneeL" : "KneeR", hip, new Vector3(0, -_thigh, 0));
                b.Col = look.Pants;
                var sh = new[]
                {
                    new Ring(-_shin + 0.03f * s, 0.083f * s, 0.092f * s, 0.012f * s),   // flares over the boot
                    new Ring(-_shin + 0.14f * s, 0.071f * s, 0.077f * s, 0.002f * s),
                    new Ring(-_shin * 0.5f, 0.069f * s, 0.076f * s, -0.006f * s),
                    new Ring(0f, 0.071f * s, 0.074f * s),
                };
                b.Loft(sh, sides, true, false);
                if (look.Hero)
                {
                    Ink(b, sh, -_shin * 0.14f, -70f, -_shin * 0.14f, 70f, 0.012f * s, -0.018f * s);
                    Ink(b, sh, -_shin + 0.12f * s, -80f, -_shin + 0.13f * s, 80f, 0.011f * s, 0.014f * s);
                    Ink(b, sh, -_shin + 0.2f * s, -50f, -_shin + 0.22f * s, 60f, 0.009f * s, 0.012f * s);
                    Ink(b, sh, -_shin * 0.35f, side * 110f, -_shin * 0.8f, side * 80f, 0.01f * s, 0f);
                }
                Skin(b, knee, "Shin");

                var foot = Node(side < 0 ? "FootL" : "FootR", knee, new Vector3(0, -_shin, 0));
                b.Push(new Vector3(0, -_ankle, 0), Quaternion.identity, Vector3.one * s);
                if (look.Gear == GearKind.Board) GearModels.BoardBoot(b, look.Boots); else GearModels.Boot(b, look.Boots);
                b.Pop();
                if (look.Gear == GearKind.Skis)
                {
                    b.Push(new Vector3(0, -_ankle - 0.022f, 0));
                    GearModels.Ski(b, look.GearLength, look.TwinTip, look.GearColour, look.GearAccent, 0.085f * Mathf.Lerp(s, 1f, 0.5f), look.GearDesign);
                    b.Pop();
                }
                Skin(b, foot, "Foot", Mats.Props);
                if (side < 0) { _hipL = hip; _kneeL = knee; _footL = foot; } else { _hipR = hip; _kneeR = knee; _footR = foot; }
            }
            if (look.Gear == GearKind.Board)
            {
                _board = Node("Board", _footL, Vector3.zero);
                GearModels.Board(b, look.GearLength, look.GearColour, look.GearAccent, look.GearDesign);
                Skin(b, _board, "Board", Mats.Props);
            }
        }

        // ---- reaching ----------------------------------------------------------------------------

        /// <summary>Where a hand would be (in the world) with these arm angles, the body standing as it does now.</summary>
        public Vector3 HandFor(int side, float fwd, float outward, float twist, float elbow)
        {
            var sh = side < 0 ? _shL : _shR;
            Quaternion q = side < 0
                ? Quaternion.AngleAxis(-outward, Vector3.forward) * Quaternion.AngleAxis(-fwd, Vector3.right) * Quaternion.AngleAxis(twist, Vector3.up)
                : Quaternion.AngleAxis(outward, Vector3.forward) * Quaternion.AngleAxis(-fwd, Vector3.right) * Quaternion.AngleAxis(-twist, Vector3.up);
            Vector3 local = sh.localPosition + q * (Vector3.down * _upperArm + Quaternion.Euler(-elbow, 0f, 0f) * (Vector3.down * _forearm));
            return _torso.TransformPoint(local);
        }

        public Vector3 HandFor(int side, in Pose p) => side < 0
            ? HandFor(-1, p.ArmFwdL, p.ArmOutL, p.ArmTwistL, p.ElbowL)
            : HandFor(1, p.ArmFwdR, p.ArmOutR, p.ArmTwistR, p.ElbowR);

        /// <summary>
        /// Changes one arm of a pose so its hand comes to a point (as near as the arm allows),
        /// staying as close to the pose's own angles as it can: a little search over the shoulder
        /// and elbow angles, cheap enough to run every frame.
        /// </summary>
        public void Reach(ref Pose p, int side, Vector3 target)
        {
            if (_shL == null || _torso == null) return;
            float[] x = side < 0 ? new[] { p.ArmFwdL, p.ArmOutL, p.ArmTwistL, p.ElbowL } : new[] { p.ArmFwdR, p.ArmOutR, p.ArmTwistR, p.ElbowR };
            float[] x0 = (float[])x.Clone();
            float[] lo = { -70f, -25f, -70f, 2f }, hi = { 175f, 130f, 100f, 150f };
            float Cost(float[] v)
            {
                float c = (HandFor(side, v[0], v[1], v[2], v[3]) - target).sqrMagnitude;
                for (int i = 0; i < 4; i++) c += 1.5e-7f * (v[i] - x0[i]) * (v[i] - x0[i]);
                return c;
            }
            float best = Cost(x), step = 12f;
            for (int it = 0; it < 80 && step > 0.25f; it++)
            {
                bool better = false;
                for (int i = 0; i < 4 && !better; i++)
                    for (int dir = -1; dir <= 1; dir += 2)
                    {
                        float keep = x[i];
                        x[i] = Mathf.Clamp(keep + dir * step, lo[i], hi[i]);
                        float c = Cost(x);
                        if (c < best) { best = c; better = true; break; }
                        x[i] = keep;
                    }
                if (!better) step *= 0.5f;
            }
            if (side < 0) { p.ArmFwdL = x[0]; p.ArmOutL = x[1]; p.ArmTwistL = x[2]; p.ElbowL = x[3]; }
            else { p.ArmFwdR = x[0]; p.ArmOutR = x[1]; p.ArmTwistR = x[2]; p.ElbowR = x[3]; }
        }

        /// <summary>The size of a gloved fist (its radius), for keeping hands off things and apart.</summary>
        public float FistRadius => 0.045f * _s;

        // ---- animation -------------------------------------------------------------------------

        Renderer _probe;
        int _frame;

        void Update()
        {
            float dt = Time.deltaTime;
            if (Crowd) Cull();
            // Someone nobody can see is only brought up to date now and then.
            _probe ??= GetComponentInChildren<Renderer>();
            if (Look != null && !Look.Hero && _probe != null && !_probe.isVisible && ((++_frame + (int)(_seed * 10f)) & 7) != 0) { _time += dt; return; }
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
            if (_face != null || (Look.Hero && _model == null)) AnimateFace(dt);
        }

        void AnimateFace(float dt)
        {
            float hr = _hr;
            // Blink.
            if (_time > _nextBlink) { _blink = 0.13f; _nextBlink = _time + 1.8f + Mathf.PerlinNoise(_seed, _time) * 4f; }
            _blink -= dt;
            if (_face != null)
            {
                // The sculpted face: its own lids, jaw, lips and brows move.
                _blinkW = Mathf.MoveTowards(_blinkW, _blink > 0f || EyesClosed ? 1f : 0f, dt * 16f);
                float goal = 0f;
                if (Talking)
                {
                    float syll = Mathf.Abs(Mathf.Sin(_time * 11f + _seed)) * (0.35f + 0.65f * Mathf.PerlinNoise(_time * 5f, _seed));
                    goal = Mathf.PerlinNoise(_time * 1.3f, _seed + 9f) > 0.24f ? 0.15f + syll * 0.75f : 0.05f;
                }
                _talk = Mathf.Lerp(_talk, goal, 1f - Mathf.Exp(-26f * dt));
                float jaw = Mathf.Clamp01(Mathf.Max(MouthOpen, _talk));
                _face.SetBlendShapeWeight(_kBlink, Mathf.Clamp01(_blinkW + Mathf.Max(0f, -BrowRaise) * 0.18f) * 100f);
                _face.SetBlendShapeWeight(_kMouth, jaw * 46f + Smile * 5f);
                _face.SetBlendShapeWeight(_kSmile, Mathf.Clamp01(Smile) * 100f);
                _face.SetBlendShapeWeight(_kBrowUp, Mathf.Clamp01(BrowRaise) * 100f);
                _face.SetBlendShapeWeight(_kBrowDown, Mathf.Clamp01(-BrowRaise) * 100f);
                return;
            }
            float lidUp = _blink > 0f || EyesClosed ? -0.02f : 0.215f + (BrowRaise > 0f ? BrowRaise * 0.08f : BrowRaise * 0.06f) - Smile * 0.03f;
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
            float standY = _ankle + ext * Mathf.Cos(p.LegOut * Mathf.Deg2Rad) + _hipDrop;
            // Snowplough: the toes turn in at the ankle, never so far that the ski tips cross.
            float footIn = p.FootIn;
            if (Look.Gear == GearKind.Skis && footIn > 0f)
            {
                float footX = _hipX + ext * Mathf.Sin(p.LegOut * Mathf.Deg2Rad);
                float maxSin = (footX - SkiHalfWidth - 0.012f) / (0.56f * Look.GearLength);
                footIn = Mathf.Min(footIn, Mathf.Asin(Mathf.Clamp(maxSin, 0f, 0.6f)) * Mathf.Rad2Deg);
            }
            float footZ = _thigh * Mathf.Sin(tL) + _shin * Mathf.Sin(tL - kL);
            var hipRot = Quaternion.Euler(0f, side * 82f, 0f);
            _hips.localRotation = hipRot;
            _hips.localPosition = hipRot * new Vector3(0, 0, Mathf.Lerp(-footZ, 0f, p.Seated)) + new Vector3(0, Mathf.Lerp(standY, 0.1f * _s, p.Seated) - p.HipDrop, 0);

            _torso.localRotation = Quaternion.Euler(p.TorsoPitch + breath * 0.8f, p.TorsoYaw - side * 22f, -p.TorsoRoll);
            _head.localRotation = Quaternion.Euler(p.HeadPitch + _wander.y - LookOffset.y, p.HeadYaw + _wander.x + LookOffset.x - side * 56f, p.HeadRoll);

            _shL.localRotation = Quaternion.AngleAxis(-p.ArmOutL, Vector3.forward) * Quaternion.AngleAxis(-(p.ArmFwdL + ArmLiftL), Vector3.right) * Quaternion.AngleAxis(p.ArmTwistL, Vector3.up);
            _shR.localRotation = Quaternion.AngleAxis(p.ArmOutR, Vector3.forward) * Quaternion.AngleAxis(-(p.ArmFwdR + ArmLiftR), Vector3.right) * Quaternion.AngleAxis(-p.ArmTwistR, Vector3.up);
            _elL.localRotation = Quaternion.Euler(-p.ElbowL, 0, 0);
            _elR.localRotation = Quaternion.Euler(-p.ElbowR, 0, 0);

            _hipL.localRotation = Quaternion.AngleAxis(-p.LegOut, Vector3.forward) * Quaternion.AngleAxis(-p.ThighL, Vector3.right);
            _hipR.localRotation = Quaternion.AngleAxis(p.LegOut, Vector3.forward) * Quaternion.AngleAxis(-p.ThighR, Vector3.right);
            _kneeL.localRotation = Quaternion.Euler(p.KneeL, 0, 0);
            _kneeR.localRotation = Quaternion.Euler(p.KneeR, 0, 0);
            // Keep the feet flat on the ground (seated: let them hang a little toe-down).
            float hang = p.Seated * 12f;
            // Seated with a board strapped to one foot, that foot turns out so the board hangs pointing ahead.
            float twistL = _board != null && !_boardRight ? -80f * p.Seated : 0f, twistR = _board != null && _boardRight ? 80f * p.Seated : 0f;
            _footL.localRotation = Quaternion.Euler(p.ThighL - p.KneeL + hang, 0, 0) * Quaternion.AngleAxis(p.LegOut * (1f - p.Seated), Vector3.forward) * Quaternion.Euler(0f, footIn + twistL, -footIn * 0.35f);
            _footR.localRotation = Quaternion.Euler(p.ThighR - p.KneeR + hang, 0, 0) * Quaternion.AngleAxis(-p.LegOut * (1f - p.Seated), Vector3.forward) * Quaternion.Euler(0f, -footIn + twistR, footIn * 0.35f);

            if (_board != null)
            {
                // Riding: across the body, under both feet. On the lift: hanging from the front
                // foot, pointing ahead, most of it under the rider's own seat.
                // Always across the foot, the boot in its binding: riding, the board's middle is between
                // the feet; on the lift it hangs from the one foot (turned out), the rest of it ahead.
                var rot = Quaternion.Euler(0f, 90f, 0f);
                _board.localRotation = rot;
                _board.localPosition = new Vector3(0, -_ankle - 0.025f, 0) + rot * new Vector3(0, 0, (_boardRight ? -1f : 1f) * Mathf.Lerp(_hipX + (_thigh + _shin) * Mathf.Sin(p.LegOut * Mathf.Deg2Rad), 0.26f, p.Seated));
            }
            if (_poleMode == PoleMode.Lap && Poles != PoleMode.Lap)
                for (int i = 0; i < _poles.Count; i++) { _poles[i].localPosition = _poleHome[i]; _poles[i].localRotation = Quaternion.Euler(-62f, 0f, 0f); }
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
