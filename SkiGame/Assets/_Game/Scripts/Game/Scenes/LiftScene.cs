using System;
using Ski.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// The four friends on the chair lift. The chair stays put and the mountain scrolls past in
    /// identical tiles, so the scene loops forever with no seam. Three uses:
    /// the main menu (static front view, pick a character), the storm cutscene (cuts between
    /// angles, voiced lines, subtitles, skippable) and the short ride back up after a run.
    /// </summary>
    public sealed class LiftScene : MonoBehaviour
    {
        public enum Mode { Menu, Cutscene, Ride }

        public Mode State { get; private set; } = Mode.Menu;
        public Person[] Friends { get; private set; }
        public Camera Cam { get; private set; }
        public Action<CharacterId> OnSelect;
        public float CutsceneTime { get; private set; }
        public int BeatIndex => _beat;

        // Slope the lift climbs.
        const float SlopeDeg = 20f, Period = 60f, CableHeight = 12f, LiftSpeed = 2.6f, ChairGap = 15f;
        static readonly Vector3 U = new Vector3(0f, Mathf.Sin(SlopeDeg * Mathf.Deg2Rad), Mathf.Cos(SlopeDeg * Mathf.Deg2Rad));
        static readonly Vector3 N = new Vector3(0f, Mathf.Cos(SlopeDeg * Mathf.Deg2Rad), -Mathf.Sin(SlopeDeg * Mathf.Deg2Rad));
        static readonly Quaternion SlopeRot = Quaternion.LookRotation(U, N);
        static readonly Quaternion Upright = Quaternion.Inverse(SlopeRot); // makes a slope-space object stand vertical

        Transform _slope, _env, _returnLine, _chair, _station;
        float _travel, _time, _sway, _swayVel;
        int _lastPylon;

        // Skiers on the piste below.
        Person[] _skiers; float[] _skierS, _skierSpeed, _skierPhase, _skierX;

        // Idle acting.
        readonly float[] _gestureTimer = new float[4];
        readonly int[] _gesture = new int[4];
        readonly float[] _gestureLen = new float[4];
        readonly Pose[] _basePose = new Pose[4];
        int _hovered = -1, _waving = -1;

        // Camera.
        struct Shot { public Vector3 From, To, Look; public float Fov; public Shot(Vector3 f, Vector3 t, Vector3 l, float fov) { From = f; To = t; Look = l; Fov = fov; } }
        static readonly Shot[] Shots =
        {
            new Shot(new Vector3(0.55f, 0.42f, 5.0f), new Vector3(0.1f, 0.5f, 4.2f), new Vector3(0.38f, -0.2f, 0f), 34f),     // 0 front wide
            new Shot(new Vector3(1.75f, 0.78f, 2.7f), new Vector3(1.55f, 0.76f, 2.5f), new Vector3(0.56f, 0.62f, 0f), 27f), // 1 Jonte + Maja
            new Shot(new Vector3(-2.9f, 0.74f, 1.5f), new Vector3(-2.6f, 0.72f, 1.2f), new Vector3(0.2f, 0.56f, 0.05f), 30f), // 2 profile along the row
            new Shot(new Vector3(-1.65f, 0.76f, 2.6f), new Vector3(-1.45f, 0.74f, 2.4f), new Vector3(-0.57f, 0.62f, 0f), 26f), // 3 Erik + Linn
            new Shot(new Vector3(-0.7f, 0.22f, 3.7f), new Vector3(-0.35f, 0.3f, 3.2f), new Vector3(0f, 0.58f, 0f), 33f),      // 4 low front, all four faces
            new Shot(new Vector3(1.5f, 0.98f, 3.9f), new Vector3(1.1f, 0.9f, 3.4f), new Vector3(-0.05f, 0.6f, 0f), 32f),     // 5 front, a little high and from Jonte's side
            new Shot(new Vector3(-0.15f, 0.64f, 3.1f), new Vector3(-0.05f, 0.62f, 2.7f), new Vector3(0f, 0.6f, 0f), 25f),     // 6 tight front
            new Shot(new Vector3(0.35f, 0.78f, 4.6f), new Vector3(0.2f, 0.7f, 3.3f), new Vector3(0.1f, 0.58f, 0f), 31f),       // 7 front, pushing in as they arrive
        };
        int _shot;
        float _shotT, _shotLen = 6f;

        // Cutscene.
        int _beat = -1;
        float _beatT, _beatLen, _stormFrom;
        Action _done;
        bool _finishing;
        float _finishT;
        VisualElement _ui, _subBox, _skip;
        Label _subName, _subText;
        CharacterId _player;

        public static float SeatX(int i) => 0.86f - i * 0.573f;

        // The safety bar, its side arms and the foot-rest stem, in the chair's own space (see Props.Chair).
        static readonly Vector3[] BarParts =
        {
            new Vector3(-1.15f, -2.55f, 0.42f), new Vector3(1.15f, -2.55f, 0.42f),
            new Vector3(-1.15f, -1.9f, -0.4f), new Vector3(-1.15f, -2.55f, 0.42f),
            new Vector3(1.15f, -1.9f, -0.4f), new Vector3(1.15f, -2.55f, 0.42f),
            new Vector3(0f, -2.55f, 0.42f), new Vector3(0f, -3.65f, 0.62f),
        };

        /// <summary>What each friend's hands are doing: 0 as their pose has them (kept off the bar and
        /// apart), 1 holding the bar, 2 cupped at the mouth against the cold.</summary>
        readonly int[] _hands = new int[4];

        /// <summary>
        /// Hands never go through the bar or through each other. A hand meant to hold the bar is put
        /// on top of it; one that would cut through it comes to rest on it instead; two hands that
        /// meet stay a fist apart. Done by bending the arm to the spot (Person.Reach), every frame,
        /// so it works whatever the body's size.
        /// </summary>
        void PlaceHands()
        {
            if (Friends == null || _chair == null) return;
            for (int i = 0; i < Friends.Length; i++)
            {
                var f = Friends[i];
                if (f == null || f.ShoulderL == null) continue;
                f.ArmLiftL = f.ArmLiftR = 0f;
                var p = f.Target;
                float fist = f.FistRadius;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 hand = f.HandFor(side, p);
                    Transform sh = side < 0 ? f.ShoulderL : f.ShoulderR, el = side < 0 ? f.ElbowL : f.ElbowR;
                    int mode = _hands[i];
                    if (mode == 0)
                    {
                        // Would the hand or forearm go through the bar? Then it rests on it.
                        Vector3 h = _chair.InverseTransformPoint(hand), e = _chair.InverseTransformPoint(el.position);
                        float d = float.MaxValue;
                        for (int k = 0; k < BarParts.Length; k += 2) d = Mathf.Min(d, SegmentDistance(e, h, BarParts[k], BarParts[k + 1]));
                        if (d < fist + 0.035f) mode = 1;
                    }
                    if (mode == 1) f.Reach(ref p, side, BarGrip(sh, fist));
                    else if (mode == 2) f.Reach(ref p, side, f.Head.position + f.transform.forward * (0.15f * f.Scale) + Vector3.down * (0.03f * f.Scale) + f.transform.right * side * fist * 1.05f);
                }
                // Two hands never in the same place.
                Vector3 hl = f.HandFor(-1, p), hr = f.HandFor(1, p);
                if ((hl - hr).magnitude < fist * 2f)
                {
                    Vector3 mid = (hl + hr) * 0.5f, across = f.transform.right;
                    f.Reach(ref p, -1, mid - across * fist * 1.05f);
                    f.Reach(ref p, 1, mid + across * fist * 1.05f);
                }
                f.Target = p;
            }
        }

        /// <summary>A hand's place on top of the safety bar, in front of its shoulder.</summary>
        Vector3 BarGrip(Transform shoulder, float fist)
        {
            Vector3 s = _chair.InverseTransformPoint(shoulder.position);
            Vector3 a = BarParts[0], b = BarParts[1];
            float x = Mathf.Clamp(s.x, Mathf.Min(a.x, b.x) + 0.08f, Mathf.Max(a.x, b.x) - 0.08f);
            return _chair.TransformPoint(new Vector3(x, a.y + fist * 0.8f, a.z - 0.01f));
        }

        /// <summary>Shortest distance between two line segments.</summary>
        static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a < 1e-8f && e < 1e-8f) return r.magnitude;
            if (a < 1e-8f) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e < 1e-8f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
                    s = denom > 1e-8f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            return ((p1 + d1 * s) - (p2 + d2 * t)).magnitude;
        }

        public static LiftScene Create(Transform parent, Mode mode, CharacterId player = CharacterId.Erik)
        {
            var go = new GameObject("LiftScene");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<LiftScene>();
            s.State = mode; s._player = player;
            s.Build();
            return s;
        }

        // ---- building --------------------------------------------------------------------

        static float TileHeight(float x, float z)
        {
            float k = 2f * Mathf.PI / Period;
            return 0.55f * Mathf.Sin(z * k + x * 0.06f) + 0.3f * Mathf.Sin(2f * z * k - x * 0.11f + 1f) + 0.012f * x * x * 0.08f;
        }

        void Build()
        {
            // Slope frame: Z runs up the lift line, Y is the slope normal; the cable is CableHeight above the snow.
            _slope = new GameObject("Slope").transform;
            _slope.SetParent(transform, false);
            _slope.localPosition = new Vector3(0f, 3.1f, 0f) - N * CableHeight;
            _slope.localRotation = SlopeRot;
            _env = new GameObject("Scrolling").transform;
            _env.SetParent(_slope, false);

            BuildTiles();
            BuildCablesAndChairs();
            BuildChair();
            BuildSkiers();
            BuildStation();

            var camGo = new GameObject("Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.1f; Cam.farClipPlane = 2500f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGo.AddComponent<AudioListener>();
            if (WorldLook.Instance != null) WorldLook.Instance.Follow = camGo.transform;

            SetShot(State == Mode.Ride ? 2 : 0, 30f);
            PlaceCamera(0f);

            if (AudioDirector.I != null)
            {
                AudioDirector.I.Ambience("lift");
                AudioDirector.I.Music("music_menu", State == Mode.Menu ? 1f : 0.7f);
            }
            if (State == Mode.Menu) BuildMenuUi();
            if (State == Mode.Ride) BuildRideUi();
        }

        void BuildTiles()
        {
            // One tile of snow, forest and pylon, repeated up and down the mountain.
            const float half = 110f, step = 4f;
            int nx = Mathf.RoundToInt(half * 2f / step), nz = Mathf.RoundToInt(Period / step);
            var v = new Vector3[(nx + 1) * (nz + 1)]; var c = new Color32[v.Length];
            for (int iz = 0; iz <= nz; iz++)
                for (int ix = 0; ix <= nx; ix++)
                {
                    float x = -half + ix * step, z = iz * step;
                    v[iz * (nx + 1) + ix] = new Vector3(x, TileHeight(x, z), z);
                    bool piste = x > 9f && x < 36f;
                    Color32 col = piste ? Palette.Snow : Palette.SnowOffPiste; col.a = (byte)(piste ? 255 : 0);
                    c[iz * (nx + 1) + ix] = col;
                }
            var tris = new int[nx * nz * 6];
            int t = 0;
            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    int a = iz * (nx + 1) + ix, b = a + nx + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = b + 1; tris[t++] = a; tris[t++] = b + 1; tris[t++] = a + 1;
                }
            var snow = new Mesh { name = "LiftSnow", vertices = v, colors32 = c, triangles = tris };
            snow.RecalculateNormals(); snow.RecalculateBounds();

            var rng = new Rng(2027);
            var trees = new MeshBuilder();
            for (int i = 0; i < 150; i++)
            {
                float x = rng.Range(-half + 4f, half - 4f), z = rng.Range(0f, Period);
                float scale = rng.Range(0.9f, 1.9f); int variant = rng.Range(0, 3); float yaw = rng.Range(0f, 360f);
                if (x > 6.5f && x < 39f) continue;                    // the piste
                if (Mathf.Abs(x - 2.1f) < 8.5f) continue;             // the lift cut
                trees.Push(new Vector3(x, TileHeight(x, z) - 0.2f, z), Upright * Quaternion.Euler(0, yaw, 0), Vector3.one * scale);
                Props.Pine(trees, variant, rng);
                trees.Pop();
            }
            var treeMesh = trees.Build("LiftTrees", false);

            var props = new MeshBuilder();
            float pz = Period * 0.5f, py = TileHeight(2.1f, pz);
            props.Push(new Vector3(2.1f, py - 0.4f, pz), Upright);
            Props.Pylon(props, (CableHeight - py) / Mathf.Cos(SlopeDeg * Mathf.Deg2Rad) + 0.85f);
            props.Pop();
            // Piste markers on the run beside the lift.
            for (int i = 0; i < 2; i++)
            {
                props.Push(new Vector3(8.5f, TileHeight(8.5f, i * 30f + 6f), i * 30f + 6f), Upright);
                Props.PisteMarker(props, Palette.SignalBlue);
                props.Pop();
            }
            var propMesh = props.Build("LiftProps");

            for (int k = -12; k <= 2; k++)
            {
                var tile = new GameObject("Tile " + k).transform;
                tile.SetParent(_env, false);
                tile.localPosition = new Vector3(0f, 0f, k * Period);
                AddMesh(tile, snow, Mats.Snow, false);
                AddMesh(tile, treeMesh, Mats.Trees, true);
                AddMesh(tile, propMesh, Mats.Props, true);
            }
        }

        static void AddMesh(Transform parent, Mesh mesh, Material mat, bool shadows)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        void BuildCablesAndChairs()
        {
            var b = new MeshBuilder();
            b.Col = Palette.Ink;
            b.Tube(new Vector3(0f, CableHeight, -420f), new Vector3(0f, CableHeight, 90f), 0.03f, 4);
            b.Tube(new Vector3(4.2f, CableHeight, -420f), new Vector3(4.2f, CableHeight, 90f), 0.03f, 4);
            b.ToObject("Cables", Mats.Props, _slope, false);

            var cb = new MeshBuilder();
            cb.Push(Vector3.zero, Upright);
            Props.Chair(cb);
            cb.Pop();
            var chairMesh = cb.Build("Chair");

            // Chairs on our cable keep their distance to us; some carry other skiers.
            var rng = new Rng(88);
            for (int k = -14; k <= 2; k++)
            {
                if (k == 0) continue;
                var ch = new GameObject("Chair " + k).transform;
                ch.SetParent(_slope, false);
                ch.localPosition = new Vector3(0f, CableHeight, k * ChairGap);
                AddMesh(ch, chairMesh, Mats.Props, true);
                if (k < 0 && k >= -5)
                {
                    int riders = rng.Range(1, 4);
                    for (int i = 0; i < riders; i++)
                    {
                        var p = Person.Create(PersonLook.Random(rng, false), ch, "Rider");
                        p.transform.localRotation = Upright;
                        p.transform.localPosition = Upright * new Vector3(SeatX(rng.Range(0, 4)) + rng.Range(-0.05f, 0.05f), -3.07f, -0.02f);
                        p.Snap(Pose.Sit);
                    }
                }
            }
            // The other cable brings empty chairs back down.
            _returnLine = new GameObject("Return line").transform;
            _returnLine.SetParent(_slope, false);
            for (int k = -16; k <= 4; k++)
            {
                var ch = new GameObject("Return chair").transform;
                ch.SetParent(_returnLine, false);
                ch.localPosition = new Vector3(4.2f, CableHeight, k * ChairGap);
                ch.localRotation = Upright * Quaternion.Euler(0, 180, 0) * SlopeRot; // faces back down the hill
                AddMesh(ch, chairMesh, Mats.Props, true);
            }
        }

        void BuildChair()
        {
            _chair = new GameObject("Our chair").transform;
            _chair.SetParent(transform, false);
            _chair.localPosition = new Vector3(0f, 3.1f, 0f);
            var b = new MeshBuilder();
            Props.Chair(b);
            b.ToObject("Chair model", Mats.Props, _chair);

            Friends = new Person[4];
            for (int i = 0; i < 4; i++)
            {
                var p = Person.Create(PersonLook.For((CharacterId)i), _chair, SkierInfo.Get((CharacterId)i).Name);
                p.transform.localPosition = new Vector3(SeatX(i), -3.07f, -0.03f);
                var pose = Pose.Sit;
                // A little personality in how each one sits.
                if (i == 0) { pose.TorsoPitch = -12f; pose.LegOut = 12f; pose.KneeL = 70f; pose.KneeR = 96f; pose.ArmFwdR = 40f; pose.ElbowR = 30f; }
                if (i == 1) { pose.TorsoPitch = 2f; pose.KneeL = 92f; pose.KneeR = 76f; pose.ArmTwistL = 40f; pose.ArmTwistR = 40f; }
                if (i == 2) { pose.TorsoPitch = -2f; pose.ArmFwdL = 36f; pose.ArmFwdR = 36f; pose.ElbowL = 80f; pose.ElbowR = 80f; }
                if (i == 3) { pose.TorsoPitch = 6f; pose.KneeL = 70f; pose.KneeR = 70f; pose.LegOut = 9f; }
                _basePose[i] = pose;
                p.Snap(pose);
                p.SetGogglesUp(true, true);
                p.Poles = PoleMode.Lap;
                p.BoardHangYaw = SeatX(i) < 0f ? -14f : 14f;
                p.BoardOnRightFoot = SeatX(i) > 0f;             // hangs from the outer foot, clear of the neighbour
                p.Smile = 0.35f;
                p.PoseSpeed = 5f;
                Friends[i] = p;
                _gestureTimer[i] = 1.5f + i * 1.7f;
            }
        }

        void BuildSkiers()
        {
            var rng = new Rng(555);
            int n = 7;
            _skiers = new Person[n]; _skierS = new float[n]; _skierSpeed = new float[n]; _skierPhase = new float[n]; _skierX = new float[n];
            for (int i = 0; i < n; i++)
            {
                _skiers[i] = Person.Create(PersonLook.Random(rng, false), _slope, "Skier");
                _skiers[i].Snap(Pose.Ski);
                _skiers[i].Alive = false;
                _skierS[i] = rng.Range(-300f, 20f);
                _skierSpeed[i] = rng.Range(8f, 14f);
                _skierPhase[i] = rng.Range(0f, 6f);
                _skierX[i] = rng.Range(14f, 30f);
            }
        }

        void BuildStation()
        {
            // The top station, only seen at the end of the cutscene as it looms out of the snow.
            _station = new GameObject("Top station").transform;
            _station.SetParent(_slope, false);
            var b = new MeshBuilder();
            b.Push(new Vector3(2.1f, 0f, 0f), Upright);
            Props.Pylon(b, CableHeight / Mathf.Cos(SlopeDeg * Mathf.Deg2Rad) + 0.9f);
            b.Push(new Vector3(-9f, 1.5f, 10f));
            Props.Cabin(b, new Vector3(8f, 4.2f, 10f), 1, false, Palette.TimberLight);
            b.Pop();
            b.Col = Palette.Snow;
            b.Box(new Vector3(-2.1f, 9.2f, 6f), new Vector3(13f, 0.5f, 9f));   // unloading platform
            b.Col = Palette.SteelDark;
            b.Box(new Vector3(-2.1f, 15.5f, 9f), new Vector3(9f, 0.5f, 5f));   // bullwheel housing
            b.Pop();
            b.ToObject("Station model", Mats.Props, _station);
            _station.localPosition = new Vector3(0f, 0f, 400f);
        }

        // ---- menu ------------------------------------------------------------------------

        void BuildMenuUi()
        {
            var ui = Ui.Instance;
            if (ui == null) return;
            _ui = Ui.Div(ui.ScreenLayer, "layer");
            var title = Ui.Div(_ui, "menu-title-box");
            Ui.Text(title, "LAST RUN", "title");
            Ui.Text(title, "NEW YEAR'S EVE ON THE MOUNTAIN", "subtitle");

            var corner = Ui.Div(_ui, "menu-corner");
            Ui.Btn(corner, "SETTINGS", () => Dialogs.Settings(), "btn-plain btn-small");

            var prompt = Ui.Div(_ui, "menu-prompt");
            Ui.Text(prompt, "WHO ARE YOU SKIING AS?", "h2 outlined");

            var row = Ui.Div(_ui, "menu-chars");
            for (int i = 0; i < 4; i++)
            {
                int id = i;
                var info = SkierInfo.Get((CharacterId)i);
                var st = CharacterStyle.Get((CharacterId)i);
                var card = Ui.Div(row, "char-card");
                card.pickingMode = PickingMode.Position;
                Ui.Text(card, info.Name.ToUpperInvariant(), "char-name");
                var gear = Ui.Text(card, GearName(info.Gear), "char-gear");
                gear.style.backgroundColor = st.Jacket;
                gear.style.color = Palette.White;
                Ui.Text(card, info.TraitTitle.ToUpperInvariant(), "char-trait");
                Ui.Text(card, info.TraitText, "char-text");
                card.RegisterCallback<PointerEnterEvent>(_ => { _hovered = id; AudioDirector.Hover(); });
                card.RegisterCallback<PointerLeaveEvent>(_ => { if (_hovered == id) _hovered = -1; });
                card.RegisterCallback<ClickEvent>(_ => Select(id));
            }
        }

        static string GearName(Gear g) => g == Gear.Snowboard ? "SNOWBOARD" : g == Gear.Blades ? "SKIBLADES" : g == Gear.Carvers ? "CARVING SKIS" : "TWIN-TIPS";

        public void Select(int id)
        {
            if (State != Mode.Menu || Dialogs.Open) return;
            AudioDirector.Click();
            _hovered = -1;
            OnSelect?.Invoke((CharacterId)id);
        }

        void BuildRideUi()
        {
            var ui = Ui.Instance;
            if (ui == null) return;
            _ui = Ui.Div(ui.ScreenLayer, "layer");
            var box = Ui.Div(_ui, "banner");
            Ui.Text(box, "BACK UP FOR ANOTHER ONE", "h1 outlined");
        }

        // ---- cutscene --------------------------------------------------------------------

        /// <summary>Starts the storm cutscene. The menu UI disappears and the camera moves in.</summary>
        public void PlayCutscene(CharacterId player, Action done)
        {
            _player = player;
            _done = done;
            State = Mode.Cutscene;
            AudioDirector.I?.Music(null);       // the theme rests during the cutscene (a walkman song plays on)
            _ui?.RemoveFromHierarchy();
            var ui = Ui.Instance;
            if (ui != null)
            {
                _ui = Ui.Div(ui.ScreenLayer, "layer");
                var subs = Ui.Div(_ui, "subs");
                _subBox = Ui.Div(subs, "sub-box");
                _subName = Ui.Text(_subBox, "", "sub-name");
                _subText = Ui.Text(_subBox, "", "sub-text");
                _subBox.style.opacity = 0f;
                // On the top layer, so it shows on the letterbox bar instead of under it.
                _skip = Ui.Div(ui.TopLayer, "skip");
                Ui.Keys(_skip, "SPACE", "skip");
                ui.Letterbox(true);
            }
            CutsceneTime = 0f;
            _beat = -1;
            // Glide from the menu view into the first shot instead of cutting.
            _shot = 0; _shotT = 0f; _shotLen = 7f;
            NextBeat();
        }

        void NextBeat()
        {
            if (_beat >= 0) Friends[(int)CutsceneScript.Beats[_beat].Who].Talking = false;
            _beat++;
            if (_beat >= CutsceneScript.Beats.Length) { BeginFinish(); return; }
            var b = CutsceneScript.Beats[_beat];
            var clip = Resources.Load<AudioClip>("Voices/" + b.Voice);
            float len = clip != null ? clip.length : CutsceneScript.EstimateSeconds(b.Text);
            if (clip != null && AudioDirector.I != null) AudioDirector.I.Voice(clip);
            _beatT = 0f; _beatLen = len + b.Hold;
            _stormFrom = WorldLook.Instance != null ? WorldLook.Instance.Storm : 0f;
            if (b.Shot != _shot || _beat == 0) SetShot(b.Shot, Mathf.Max(_beatLen * 1.6f, 5f));

            int who = (int)b.Who;
            for (int i = 0; i < 4; i++)
            {
                var f = Friends[i];
                f.Talking = i == who;
                f.BrowRaise = i == who ? b.Mood : b.Panic ? 0.8f : b.Mood * 0.3f;
                // Everyone else looks at whoever is talking.
                f.LookOffset = i == who ? Vector2.zero : new Vector2(Mathf.Clamp((SeatX(who) - SeatX(i)) * 40f, -36f, 36f), 0f);   // glance at the speaker, face still to the camera
                Pose p = b.Panic ? (i % 2 == 0 ? Pose.SitPanic : Pose.SitCold) : _basePose[i];
                _hands[i] = b.Panic ? (i % 2 == 0 ? 1 : 2) : 0;
                if (i == who && b.Gesture.HasValue) { p = b.Gesture.Value; _hands[i] = 0; }
                f.Target = p;
                _gestureTimer[i] = 3f + i;
                _gesture[i] = 0;
                if (b.Panic) f.SetGogglesUp(false);
            }
            if (_subBox != null)
            {
                bool show = SaveData.Subtitles;
                _subBox.style.opacity = show ? 1f : 0f;
                _subName.text = SkierInfo.Get(b.Who).Name.ToUpperInvariant();
                _subName.style.backgroundColor = CharacterStyle.Get(b.Who).Jacket;
                _subName.style.color = Palette.White;
                _subText.text = b.Text;
            }
            if (_beat == 11 && AudioDirector.I != null) AudioDirector.I.Music(null);
        }

        void BeginFinish()
        {
            if (_finishing) return;
            _finishing = true; _finishT = 0f;
            if (_subBox != null) _subBox.style.opacity = 0f;
            if (Ui.Instance != null) Ui.Instance.Fade(1f, 0.5f);
            if (AudioDirector.I != null) AudioDirector.I.StopVoice();
        }

        public void Skip()
        {
            if (State == Mode.Cutscene && !_finishing) { AudioDirector.Click(); BeginFinish(); }
        }

        // ---- camera ----------------------------------------------------------------------

        void SetShot(int shot, float seconds)
        {
            _shot = Mathf.Clamp(shot, 0, Shots.Length - 1);
            _shotT = 0f; _shotLen = seconds;
        }

        void PlaceCamera(float storm)
        {
            var s = Shots[_shot];
            float t = Mathf.Clamp01(_shotT / _shotLen);
            t = t * t * (3f - 2f * t);
            Vector3 pos = Vector3.Lerp(s.From, s.To, State == Mode.Menu ? 0f : t);
            // Hand-held drift; rougher as the storm grows.
            float amp = 0.012f + storm * 0.05f;
            Vector3 drift = new Vector3(Mathf.PerlinNoise(_time * 0.35f, 1f) - 0.5f, Mathf.PerlinNoise(2f, _time * 0.3f) - 0.5f, 0f) * amp * 2f;
            drift += new Vector3(Mathf.PerlinNoise(_time * 7f, 4f) - 0.5f, Mathf.PerlinNoise(6f, _time * 7f) - 0.5f, 0f) * storm * 0.03f;
            Cam.transform.position = transform.TransformPoint(pos + drift);
            Cam.transform.rotation = Quaternion.LookRotation(transform.TransformPoint(s.Look + new Vector3(0f, _sway * 0.3f, 0f)) - Cam.transform.position, Vector3.up);
            Cam.fieldOfView = s.Fov;
        }

        // ---- frame -----------------------------------------------------------------------

        void Update()
        {
            float dt = Time.deltaTime;
            _time += dt;
            var look = WorldLook.Instance;
            float storm = look != null ? look.Storm : 0f;

            PlaceHands();
            // The mountain goes by.
            _travel += LiftSpeed * dt;
            float off = _travel % Period;
            _env.localPosition = new Vector3(0f, 0f, -off);
            _returnLine.localPosition = new Vector3(0f, 0f, -((_travel * 2f) % ChairGap));

            // Passing a pylon: clatter and a nudge to the chair.
            int pylon = Mathf.FloorToInt((_travel + Period * 0.5f) / Period);
            if (pylon != _lastPylon)
            {
                _lastPylon = pylon;
                _swayVel += 0.9f;
            }
            _swayVel += (-_sway * 9f - _swayVel * 1.2f) * dt + (Mathf.PerlinNoise(_time * 0.8f, 9f) - 0.5f) * (0.5f + storm * 6f) * dt;
            _sway += _swayVel * dt;
            _chair.localRotation = Quaternion.Euler(_sway * 2.2f, 0f, Mathf.Sin(_time * 0.7f) * (0.5f + storm * 2.5f));

            UpdateSkiers(dt, off);
            UpdateActing(dt, storm);

            if (State == Mode.Cutscene) UpdateCutscene(dt, look);
            else if (State == Mode.Ride)
            {
                CutsceneTime += dt;
                if (look != null) look.Storm = Mathf.MoveTowards(look.Storm, 0.12f, dt);
            }
            else if (look != null) look.Storm = Mathf.MoveTowards(look.Storm, 0f, dt * 0.5f);

            _shotT += dt;
            PlaceCamera(storm);
            if (AudioDirector.I != null) AudioDirector.I.Wind(0.16f + storm * 0.85f);
        }

        void UpdateSkiers(float dt, float off)
        {
            for (int i = 0; i < _skiers.Length; i++)
            {
                _skierS[i] -= (_skierSpeed[i] + LiftSpeed) * dt;
                if (_skierS[i] < -330f) { _skierS[i] = 40f + i * 9f; }
                float s = _skierS[i];
                float turn = Mathf.Sin(s * 0.09f + _skierPhase[i]);
                float x = _skierX[i] + turn * 6f;
                float h = TileHeight(x, s + off);
                var p = _skiers[i].transform;
                p.localPosition = new Vector3(x, h, s);
                float heading = 180f + Mathf.Cos(s * 0.09f + _skierPhase[i]) * -32f;
                // The whole body banks into the turn, the upper body stays quieter, the knees sink
                // through the middle of the turn and rise at the change of edges.
                p.localRotation = Upright * Quaternion.Euler(0f, heading, -turn * 21f);
                var pose = Pose.Ski;
                float load = Mathf.Abs(turn);
                pose.ThighL += load * 12f; pose.KneeL += load * 24f; pose.ThighR += load * 12f; pose.KneeR += load * 24f;
                pose.TorsoRoll = -turn * 9f; pose.TorsoYaw = turn * 12f; pose.TorsoPitch += load * 6f;
                pose.ArmOutL += turn * 14f; pose.ArmOutR -= turn * 14f;
                pose.ArmFwdL += (1f - load) * 16f * (turn > 0f ? 1f : 0f); pose.ArmFwdR += (1f - load) * 16f * (turn < 0f ? 1f : 0f);   // reach for the pole plant
                _skiers[i].Target = pose;
                _skiers[i].PoseSpeed = 9f;
            }
        }

        void UpdateActing(float dt, float storm)
        {
            for (int i = 0; i < 4; i++)
            {
                var f = Friends[i];
                if (State == Mode.Menu)
                {
                    // Hovering a card: that friend waves at the camera.
                    if (_hovered == i)
                    {
                        var wave = _basePose[i];
                        wave.ArmFwdR = 40f; wave.ArmOutR = 105f + Mathf.Sin(_time * 9f) * 16f; wave.ElbowR = 40f; wave.HeadRoll = 6f;
                        f.Target = wave; f.BrowRaise = 0.7f; f.LookOffset = new Vector2(-SeatX(i) * 8f, -4f);
                        _waving = i;
                        continue;
                    }
                    if (_waving == i) { _waving = -1; f.Target = _basePose[i]; f.BrowRaise = 0f; f.LookOffset = Vector2.zero; }
                }
                else if (State == Mode.Cutscene)
                {
                    // Shiver in the storm; idle gestures stop once things get tense.
                    if (storm > 0.55f) { f.Target.TorsoRoll = Mathf.Sin(_time * 31f + i * 2f) * storm * 2.2f; continue; }
                    if (f.Talking) continue;
                }

                _gestureTimer[i] -= dt;
                if (_gesture[i] != 0)
                {
                    // Animated gestures.
                    if (_gesture[i] == 3)
                    {
                        float k = Mathf.Sin(_time * 5.2f) * 22f;
                        f.Target.KneeL = _basePose[i].KneeL + k; f.Target.KneeR = _basePose[i].KneeR - k;
                    }
                    if (_gestureTimer[i] <= 0f)
                    {
                        if (_gesture[i] == 1) f.SetGogglesUp(true);
                        _gesture[i] = 0;
                        f.Target = _basePose[i];
                        f.LookOffset = Vector2.zero;
                        f.Talking = false;
                        _gestureTimer[i] = 3.5f + Mathf.PerlinNoise(_time, i * 3.1f) * 6f;
                    }
                }
                else if (_gestureTimer[i] <= 0f)
                {
                    int pick = 1 + (int)(Mathf.PerlinNoise(_time * 3.3f, i * 7.7f) * 4.99f) % 5;
                    _gesture[i] = pick;
                    switch (pick)
                    {
                        case 1: f.Target = Pose.SitAdjustGoggles; f.SetGogglesUp(false); _gestureTimer[i] = 2.2f; break;                 // goggles down, then up again
                        case 2: f.Target = Pose.SitZipJacket; _gestureTimer[i] = 2.0f; break;                                             // zip the jacket
                        case 3: _gestureTimer[i] = 2.6f; break;                                                                           // swing the skis
                        case 4:                                                                                                           // chat with a neighbour
                            int other = i == 0 ? 1 : i == 3 ? 2 : (Mathf.PerlinNoise(_time, i) > 0.5f ? i - 1 : i + 1);
                            f.LookOffset = new Vector2((SeatX(other) - SeatX(i)) * 70f, 0f);
                            f.Talking = State == Mode.Menu;
                            Friends[other].LookOffset = new Vector2((SeatX(i) - SeatX(other)) * 60f, 0f);
                            _gestureTimer[i] = 2.8f;
                            break;
                        default: f.Target = Pose.SitPoint; _gestureTimer[i] = 2.4f; break;                                               // point at the view
                    }
                }
            }
        }

        void UpdateCutscene(float dt, WorldLook look)
        {
            CutsceneTime += dt;
            var kb = Keyboard.current;
            if (kb != null && !_finishing && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) Skip();

            if (_finishing)
            {
                _finishT += dt;
                if (_finishT > 0.6f)
                {
                    var done = _done; _done = null;
                    done?.Invoke();
                }
                return;
            }
            var b = CutsceneScript.Beats[_beat];
            _beatT += dt;
            if (look != null) look.Storm = Mathf.Lerp(_stormFrom, b.Storm, Mathf.Clamp01(_beatT / _beatLen));
            // The top station slides out of the white at the very end.
            if (b.Shot == 7) _station.localPosition = new Vector3(0f, 0f, Mathf.MoveTowards(_station.localPosition.z, 13f, dt * 9f));
            else _station.localPosition = new Vector3(0f, 0f, 60f);
            // Normally the line ends with its voice clip; never wait more than a moment longer than
            // its length, so a browser that has not started audio yet cannot stall the scene.
            bool voiceDone = AudioDirector.I == null || !AudioDirector.I.VoicePlaying;
            if (_beatT >= _beatLen && (voiceDone || _beatT >= _beatLen + 1.2f)) NextBeat();
        }

        public void Close()
        {
            _ui?.RemoveFromHierarchy();
            _skip?.RemoveFromHierarchy();
            if (Ui.Instance != null) Ui.Instance.Letterbox(false);
            if (AudioDirector.I != null) { AudioDirector.I.StopVoice(); AudioDirector.I.Ambience(null); }
            Destroy(gameObject);
        }
    }
}
