using System;
using System.Collections.Generic;
using Ski.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// The ski-map menu: you stand at the top station, in first person, holding a paper piste map.
    /// Hover a run to read about it, click to drop in. The five runs are drawn with the usual
    /// colours and symbols: green circle, blue square, red rectangle, black diamond and a dashed
    /// orange line with a warning triangle for off-piste.
    /// </summary>
    public sealed class MapScene : MonoBehaviour
    {
        public Action<SlopeId> OnPick;
        public Action OnChangeSkier;
        public Camera Cam { get; private set; }
        /// <summary>The run under the pointer, or -1.</summary>
        public int Hovered { get; private set; } = -1;

        CharacterId _who;
        Transform _map, _camPivot;
        readonly List<Vector2>[] _runs = new List<Vector2>[5];
        readonly GameObject[] _highlight = new GameObject[5];
        VisualElement _ui;
        Label _infoName, _infoGoal, _infoBest, _infoRival, _infoHint;
        VisualElement _infoChip, _infoCard;
        float _open, _time;
        bool _picked;
        Material _flat;

        const float MapW = 0.74f, MapH = 0.52f; // metres

        static readonly Vector2[][] RunPoints =
        {
            new[] { new Vector2(-0.02f, 0.22f), new Vector2(-0.27f, 0.12f), new Vector2(-0.37f, -0.06f), new Vector2(-0.22f, -0.2f), new Vector2(-0.04f, -0.27f) },
            new[] { new Vector2(-0.01f, 0.22f), new Vector2(-0.15f, 0.09f), new Vector2(-0.18f, -0.07f), new Vector2(-0.09f, -0.2f), new Vector2(-0.015f, -0.27f) },
            new[] { new Vector2(0.01f, 0.22f), new Vector2(0.09f, 0.1f), new Vector2(0.13f, -0.05f), new Vector2(0.07f, -0.2f), new Vector2(0.015f, -0.27f) },
            new[] { new Vector2(0.02f, 0.22f), new Vector2(0.21f, 0.13f), new Vector2(0.29f, -0.03f), new Vector2(0.18f, -0.19f), new Vector2(0.04f, -0.27f) },
            new[] { new Vector2(0.03f, 0.225f), new Vector2(0.33f, 0.17f), new Vector2(0.44f, 0.0f), new Vector2(0.33f, -0.2f), new Vector2(0.07f, -0.28f) },
        };
        static readonly string[] Grades = { "EASY", "INTERMEDIATE", "ADVANCED", "EXPERT", "OFF-PISTE - UNMARKED" };

        public static MapScene Create(Transform parent, CharacterId who)
        {
            var go = new GameObject("MapScene");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<MapScene>();
            m._who = who;
            m.Build();
            return m;
        }

        void Build()
        {
            _flat = Mats.Unlit(Mats.WhiteTex, new Color(0.84f, 0.84f, 0.84f)); // keeps the print from bleaching in the sun
            _flat.SetFloat("_Fog", 0f);
            TopStation.Build(transform, _who);

            // First-person camera.
            _camPivot = new GameObject("Head").transform;
            _camPivot.SetParent(transform, false);
            _camPivot.localPosition = new Vector3(0f, 1.66f, 0f);
            _camPivot.localRotation = Quaternion.Euler(14f, 0f, 0f);
            var camGo = new GameObject("Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(_camPivot, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.05f; Cam.farClipPlane = 2500f; Cam.fieldOfView = 62f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGo.AddComponent<AudioListener>();
            if (WorldLook.Instance != null) WorldLook.Instance.Follow = camGo.transform;

            // The map, held in both hands.
            _map = new GameObject("Map").transform;
            _map.SetParent(camGo.transform, false);
            BuildMap();
            BuildHands(camGo.transform);
            BuildUi();
            Pose(0f);
            AudioDirector.Sfx("paper", 0.8f);
            if (AudioDirector.I != null) AudioDirector.I.Music("music_menu", 0.7f);
        }

        // ---- the paper map -----------------------------------------------------------------

        static List<Vector2> Smooth(Vector2[] p, int perSegment = 9)
        {
            var o = new List<Vector2>();
            for (int i = 0; i < p.Length - 1; i++)
            {
                Vector2 p0 = p[Mathf.Max(i - 1, 0)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(i + 2, p.Length - 1)];
                for (int s = 0; s < perSegment; s++)
                {
                    float t = s / (float)perSegment, t2 = t * t, t3 = t2 * t;
                    o.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            o.Add(p[p.Length - 1]);
            return o;
        }

        /// <summary>Map coordinates (x in -0.5..0.5, y in -0.35..0.35) to metres on the sheet.</summary>
        static Vector3 P(Vector2 m, float lift) => new Vector3(m.x * MapW, m.y * MapW, -lift);
        static Vector3 P(float x, float y, float lift) => new Vector3(x * MapW, y * MapW, -lift);

        static void Ribbon(MeshBuilder b, List<Vector2> line, float width, Color c, float lift, bool dashed = false)
        {
            b.Col = c;
            for (int i = 0; i < line.Count - 1; i++)
            {
                if (dashed && i % 3 == 2) continue;
                Vector2 a = line[i], d = line[i + 1];
                Vector2 n = new Vector2(-(d - a).y, (d - a).x).normalized * width * 0.5f;
                b.Quad(P(a - n, lift), P(a + n, lift), P(d + n, lift), P(d - n, lift));
                // Round the joints.
                Dot(b, d, width * 0.5f, c, lift, 6);
            }
        }

        static void Dot(MeshBuilder b, Vector2 centre, float r, Color c, float lift, int sides = 14)
        {
            b.Col = c;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                b.Tri(P(centre, lift), P(centre + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, lift), P(centre + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, lift));
            }
        }

        static void Poly(MeshBuilder b, Color c, float lift, params Vector2[] pts)
        {
            b.Col = c;
            for (int i = 1; i < pts.Length - 1; i++) b.Tri(P(pts[0], lift), P(pts[i + 1], lift), P(pts[i], lift));
        }

        static void Box2(MeshBuilder b, Vector2 centre, Vector2 size, Color c, float lift, float angle = 0f)
        {
            Vector2 x = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * size.x * 0.5f;
            Vector2 y = new Vector2(-Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad)) * size.y * 0.5f;
            Poly(b, c, lift, centre - x - y, centre + x - y, centre + x + y, centre - x + y);
        }

        /// <summary>The standard symbol for a slope grade, with an ink edge.</summary>
        public static void Symbol(MeshBuilder b, SlopeId id, Vector2 at, float size, float lift)
        {
            Color c = Palette.Slope(id), ink = Palette.Ink;
            switch (id)
            {
                case SlopeId.Green: Dot(b, at, size * 0.62f, ink, lift); Dot(b, at, size * 0.5f, c, lift + 0.0004f); break;
                case SlopeId.Blue: Box2(b, at, Vector2.one * size * 1.16f, ink, lift); Box2(b, at, Vector2.one * size * 0.94f, c, lift + 0.0004f); break;
                case SlopeId.Red: Box2(b, at, new Vector2(size * 1.6f, size * 0.96f), ink, lift); Box2(b, at, new Vector2(size * 1.4f, size * 0.76f), c, lift + 0.0004f); break;
                case SlopeId.Black: Box2(b, at, Vector2.one * size * 1.1f, Palette.White, lift, 45f); Box2(b, at, Vector2.one * size * 0.9f, c, lift + 0.0004f, 45f); break;
                default:
                    // Warning triangle.
                    Poly(b, ink, lift, at + new Vector2(0, size * 0.8f), at + new Vector2(-size * 0.8f, -size * 0.55f), at + new Vector2(size * 0.8f, -size * 0.55f));
                    Poly(b, Palette.SignalYellow, lift + 0.0004f, at + new Vector2(0, size * 0.56f), at + new Vector2(-size * 0.58f, -size * 0.42f), at + new Vector2(size * 0.58f, -size * 0.42f));
                    Box2(b, at + new Vector2(0, size * 0.02f), new Vector2(size * 0.12f, size * 0.42f), ink, lift + 0.0008f);
                    Box2(b, at + new Vector2(0, -size * 0.3f), new Vector2(size * 0.13f, size * 0.12f), ink, lift + 0.0008f);
                    break;
            }
        }

        void BuildMap()
        {
            var b = new MeshBuilder();
            float hw = 0.5f, hh = MapH / MapW * 0.5f;
            Color paper = Palette.UiPaper, ink = Palette.Ink;

            // Sheet with an ink border and fold lines.
            Poly(b, Palette.Mul(paper, 0.8f), -0.0006f, new Vector2(-hw - 0.006f, -hh - 0.006f), new Vector2(hw + 0.006f, -hh - 0.006f), new Vector2(hw + 0.006f, hh + 0.006f), new Vector2(-hw - 0.006f, hh + 0.006f));
            Poly(b, paper, 0f, new Vector2(-hw, -hh), new Vector2(hw, -hh), new Vector2(hw, hh), new Vector2(-hw, hh));
            // Sky wash.
            Poly(b, Palette.Mix(paper, Palette.SkyMid, 0.35f), 0.0002f, new Vector2(-hw + 0.012f, 0.02f), new Vector2(hw - 0.012f, 0.02f), new Vector2(hw - 0.012f, hh - 0.012f), new Vector2(-hw + 0.012f, hh - 0.012f));
            // Far ridges.
            Poly(b, Palette.Mix(paper, Palette.RidgeFar, 0.6f), 0.0004f, new Vector2(-0.488f, 0.02f), new Vector2(-0.488f, 0.14f), new Vector2(-0.36f, 0.22f), new Vector2(-0.24f, 0.15f), new Vector2(-0.12f, 0.02f));
            Poly(b, Palette.Mix(paper, Palette.RidgeFar, 0.6f), 0.0004f, new Vector2(0.14f, 0.02f), new Vector2(0.3f, 0.2f), new Vector2(0.4f, 0.16f), new Vector2(0.488f, 0.21f), new Vector2(0.488f, 0.02f));
            // The mountain: a snowy body with a shaded flank.
            Vector2 peak = new Vector2(0.01f, 0.3f);
            Poly(b, Palette.Snow, 0.0006f, peak, new Vector2(-0.14f, 0.19f), new Vector2(-0.33f, 0.1f), new Vector2(-0.488f, -0.08f), new Vector2(-0.488f, -0.338f), new Vector2(0.488f, -0.338f), new Vector2(0.488f, 0.04f), new Vector2(0.36f, 0.18f), new Vector2(0.16f, 0.22f));
            Poly(b, Palette.Mix(Palette.Snow, Palette.SnowShadow, 0.6f), 0.0008f, peak, new Vector2(0.16f, 0.22f), new Vector2(0.36f, 0.18f), new Vector2(0.488f, 0.04f), new Vector2(0.488f, -0.1f), new Vector2(0.38f, 0.02f), new Vector2(0.2f, 0.14f));
            Poly(b, Palette.Mix(Palette.Snow, Palette.SnowShadow, 0.4f), 0.0008f, peak, new Vector2(0.04f, 0.2f), new Vector2(0.0f, 0.24f));

            // Forest patches between the runs.
            var rng = new Rng(77);
            for (int i = 0; i < 150; i++)
            {
                Vector2 p = new Vector2(rng.Range(-0.47f, 0.47f), rng.Range(-0.3f, 0.16f));
                float inMountain = 0.26f - Mathf.Abs(p.x) * 0.55f;
                if (p.y > inMountain) continue;
                bool nearRun = false;
                for (int r = 0; r < 5 && !nearRun; r++)
                    foreach (var q in Smooth(RunPoints[r], 4)) if ((q - p).sqrMagnitude < (r == 4 ? 0.0004f : 0.0011f)) { nearRun = true; break; }
                if (nearRun || p.magnitude < 0.0f) continue;
                float s = rng.Range(0.011f, 0.018f);
                Color tree = rng.Chance(0.5f) ? Palette.Pine : Palette.PineLight;
                Poly(b, tree, 0.001f, p + new Vector2(0, s * 1.5f), p + new Vector2(-s * 0.7f, -s * 0.4f), p + new Vector2(s * 0.7f, -s * 0.4f));
            }
            // Cliff marks along the off-piste line.
            foreach (var cp in new[] { new Vector2(0.4f, 0.08f), new Vector2(0.39f, -0.12f), new Vector2(0.26f, -0.24f) })
                for (int k = 0; k < 4; k++) Box2(b, cp + new Vector2(k * 0.012f, k % 2 * 0.006f), new Vector2(0.004f, 0.018f), Palette.Rock, 0.0012f, 20f);

            // Lift line with pylon ticks, top and bottom stations.
            Vector2 top = new Vector2(0.0f, 0.245f), bottom = new Vector2(-0.0f, -0.285f);
            var lift = new List<Vector2> { new Vector2(0.075f, -0.3f), new Vector2(0.105f, -0.1f), new Vector2(0.075f, 0.1f), new Vector2(0.02f, 0.25f) };
            var liftLine = Smooth(lift.ToArray(), 6);
            Ribbon(b, liftLine, 0.004f, ink, 0.0014f);
            for (int i = 2; i < liftLine.Count; i += 4) Box2(b, liftLine[i], new Vector2(0.016f, 0.004f), ink, 0.0016f);

            // The runs.
            for (int r = 0; r < 5; r++)
            {
                _runs[r] = Smooth(RunPoints[r]);
                Color c = Palette.Slope((SlopeId)r);
                bool off = r == 4;
                Ribbon(b, _runs[r], off ? 0.012f : 0.017f, ink, 0.0018f, off);
                Ribbon(b, _runs[r], off ? 0.007f : 0.011f, c, 0.0022f, off);
                var mid = _runs[r][_runs[r].Count / 2 + (r % 2 == 0 ? -3 : 2)];
                Symbol(b, (SlopeId)r, mid, 0.026f, 0.0026f);
            }

            // Stations and the village.
            Box2(b, top, new Vector2(0.05f, 0.026f), ink, 0.003f);
            Box2(b, top, new Vector2(0.04f, 0.017f), Palette.SignalYellow, 0.0034f);
            Vector2 v = new Vector2(0.0f, -0.305f);
            Poly(b, ink, 0.003f, v + new Vector2(-0.062f, -0.02f), v + new Vector2(0.062f, -0.02f), v + new Vector2(0.062f, 0.012f), v + new Vector2(0f, 0.04f), v + new Vector2(-0.062f, 0.012f));
            Poly(b, Palette.Timber, 0.0034f, v + new Vector2(-0.055f, -0.014f), v + new Vector2(0.055f, -0.014f), v + new Vector2(0.055f, 0.008f), v + new Vector2(0f, 0.031f), v + new Vector2(-0.055f, 0.008f));
            Box2(b, v + new Vector2(-0.025f, -0.004f), new Vector2(0.013f, 0.012f), Palette.Window, 0.0038f);
            Box2(b, v + new Vector2(0.025f, -0.004f), new Vector2(0.013f, 0.012f), Palette.Window, 0.0038f);

            // Legend panel.
            Vector2 lg = new Vector2(-0.385f, 0.245f);
            Box2(b, lg, new Vector2(0.2f, 0.17f), ink, 0.003f);
            Box2(b, lg, new Vector2(0.192f, 0.162f), paper, 0.0034f);
            for (int r = 0; r < 5; r++) Symbol(b, (SlopeId)r, lg + new Vector2(-0.075f, 0.056f - r * 0.028f), 0.014f, 0.0038f);

            b.ToObject("Sheet", _flat, _map, false, false);

            // Text.
            for (int r = 0; r < 5; r++)
            {
                Label3D(Courses.Names[r], lg + new Vector2(-0.055f, 0.056f - r * 0.028f), 0.016f, ink, TextAnchor.MiddleLeft);
                var q = _runs[r][_runs[r].Count / 2 + (r % 2 == 0 ? -3 : 2)];
                Label3D((r + 1).ToString(), q + new Vector2(r == 3 ? 0f : 0f, 0.0f), 0.022f, r == 3 ? Palette.White : r == 4 ? Palette.Ink : Palette.White, TextAnchor.MiddleCenter, r == 4 ? -0.006f : 0f);
            }
            Label3D("YOU ARE HERE", top + new Vector2(0.0f, 0.03f), 0.014f, ink, TextAnchor.MiddleCenter);
            Label3D("AFTER SKI", v + new Vector2(0.0f, -0.035f), 0.014f, ink, TextAnchor.MiddleCenter);
            Label3D("FJÄLLTOPPEN  -  PISTE MAP", new Vector2(0.17f, 0.315f), 0.02f, ink, TextAnchor.MiddleCenter);

            // Highlights, switched on when a run is hovered.
            for (int r = 0; r < 5; r++)
            {
                var hb = new MeshBuilder();
                Ribbon(hb, _runs[r], 0.032f, Palette.SignalYellow, 0.0016f, false);
                _highlight[r] = hb.ToObject("Highlight " + r, _flat, _map, false, false);
                _highlight[r].SetActive(false);
            }
        }

        void Label3D(string text, Vector2 at, float height, Color colour, TextAnchor anchor, float yShift = 0f)
        {
            var tm = VillageBuilder.Sign(_map, text, Vector3.zero, Quaternion.identity, height * MapW, colour);
            tm.anchor = anchor;
            tm.transform.localPosition = P(at.x, at.y + yShift, 0.0042f);
            tm.transform.localRotation = Quaternion.identity;
        }

        void BuildHands(Transform cam)
        {
            var st = CharacterStyle.Get(_who);
            var b = new MeshBuilder();
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 grip = new Vector3(s * (MapW * 0.5f - 0.02f), -MapH * 0.5f + 0.06f, -0.012f);
                b.Col = st.Gloves;
                b.Sphere(grip, new Vector3(0.05f, 0.062f, 0.04f), 10, 6);
                b.Sphere(grip + new Vector3(-s * 0.035f, 0.035f, -0.012f), 0.024f, 8, 5); // thumb over the paper
                b.Col = st.Jacket;
                b.Capsule(grip + new Vector3(s * 0.02f, -0.05f, 0f), grip + new Vector3(s * 0.12f, -0.34f, -0.16f), 0.05f, 0.062f, 10);
                b.Col = st.JacketTrim;
                b.Cylinder(grip + new Vector3(s * 0.016f, -0.07f, 0f), 0.054f, 0.054f, 0.035f, 10, true, false);
            }
            b.ToObject("Hands", Mats.Props, _map);
        }

        // ---- UI ----------------------------------------------------------------------------

        void BuildUi()
        {
            var ui = Ui.Instance;
            if (ui == null) return;
            _ui = Ui.Div(ui.ScreenLayer, "layer");
            var title = Ui.Div(_ui, "map-title");
            Ui.Text(title, "PICK YOUR LAST RUN", "h1 outlined");
            Ui.Text(title, "Hover a run on the map, click to drop in  -  or press 1 to 5", "hint");

            _infoCard = Ui.Div(_ui, "card shadowed map-info");
            var head = Ui.Div(_infoCard, "row");
            _infoChip = Ui.Div(head, "slope-chip");
            _infoName = Ui.Text(head, "", "h2");
            _infoGoal = Ui.Text(_infoCard, "", "small");
            _infoGoal.style.whiteSpace = WhiteSpace.Normal;
            _infoBest = Ui.Text(_infoCard, "", "small");
            _infoRival = Ui.Text(_infoCard, "", "small");
            _infoHint = Ui.Text(_infoCard, "", "tiny");
            _infoCard.style.display = DisplayStyle.None;

            var corner = Ui.Div(_ui, "menu-corner");
            corner.pickingMode = PickingMode.Ignore;
            var info = SkierInfo.Get(_who);
            Ui.Text(corner, info.Name.ToUpperInvariant() + "  -  " + info.TraitTitle, "hint");
            Ui.Btn(corner, "CHANGE SKIER", () => OnChangeSkier?.Invoke(), "btn-plain btn-small");
            Ui.Btn(corner, "SETTINGS", () => Dialogs.Settings(), "btn-plain btn-small");
        }

        void ShowInfo(int r)
        {
            if (_infoCard == null) return;
            _infoCard.style.display = r >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (r < 0) return;
            var id = (SlopeId)r;
            _infoChip.ClearClassList();
            _infoChip.AddToClassList("slope-chip");
            if (id == SlopeId.Green) _infoChip.AddToClassList("slope-chip-round");
            if (id == SlopeId.Black) _infoChip.AddToClassList("slope-chip-diamond");
            _infoChip.style.backgroundColor = Palette.Slope(id);
            _infoName.text = (r + 1) + "  " + Courses.Names[r].ToUpperInvariant();
            _infoGoal.text = Grades[r] + "\n" + Courses.Goals[r];
            float best = SaveData.Boards.PersonalBest(id);
            _infoBest.text = float.IsNaN(best) ? "Your best: -" : "Your best: " + Leaderboards.Format(id, best);
            var board = SaveData.Boards.Board(id);
            _infoRival.text = "Slope record: " + Leaderboards.Format(id, board[0].Value) + "  (" + board[0].Name + ")";
            _infoHint.text = "Click to drop in";
        }

        // ---- frame -------------------------------------------------------------------------

        /// <summary>Places the map: 0 = folded away below the view, 1 = held up.</summary>
        void Pose(float open)
        {
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(open), 3f);
            _map.localPosition = new Vector3(0f, Mathf.Lerp(-0.75f, -0.045f, e), Mathf.Lerp(0.5f, 0.6f, e));
            _map.localRotation = Quaternion.Euler(Mathf.Lerp(70f, 12f, e), 0f, Mathf.Sin(_time * 0.9f) * 0.6f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _time += dt;
            _open = Mathf.MoveTowards(_open, 1f, dt / 0.9f);
            Pose(_open);
            if (WorldLook.Instance != null) WorldLook.Instance.Storm = Mathf.MoveTowards(WorldLook.Instance.Storm, 0.1f, dt * 0.35f);
            if (AudioDirector.I != null) AudioDirector.I.Wind(0.25f + (WorldLook.Instance != null ? WorldLook.Instance.Storm * 0.6f : 0f));
            if (_picked || Dialogs.Open) return;

            int hover = -1;
            var mouse = Mouse.current;
            if (mouse != null && _open > 0.9f)
            {
                Vector2 sp = mouse.position.ReadValue();
                // Gentle parallax: the head follows the pointer a little.
                Vector2 n = new Vector2(sp.x / Screen.width - 0.5f, sp.y / Screen.height - 0.5f);
                _camPivot.localRotation = Quaternion.Slerp(_camPivot.localRotation, Quaternion.Euler(14f - n.y * 5f, n.x * 7f, 0f), 1f - Mathf.Exp(-5f * dt));
                var ray = Cam.ScreenPointToRay(sp);
                var plane = new Plane(_map.forward, _map.position);
                if (plane.Raycast(ray, out float dist))
                {
                    Vector3 local = _map.InverseTransformPoint(ray.GetPoint(dist));
                    Vector2 m = new Vector2(local.x / MapW, local.y / MapW);
                    float best = 0.03f;
                    for (int r = 0; r < 5; r++)
                        for (int i = 0; i < _runs[r].Count - 1; i++)
                        {
                            float d = M.DistToSegment(m.x, m.y, _runs[r][i].x, _runs[r][i].y, _runs[r][i + 1].x, _runs[r][i + 1].y, out _);
                            if (d < best) { best = d; hover = r; }
                        }
                }
            }
            var kb = Keyboard.current;
            int key = -1;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) key = 0;
                else if (kb.digit2Key.wasPressedThisFrame) key = 1;
                else if (kb.digit3Key.wasPressedThisFrame) key = 2;
                else if (kb.digit4Key.wasPressedThisFrame) key = 3;
                else if (kb.digit5Key.wasPressedThisFrame) key = 4;
            }
            if (hover != Hovered)
            {
                Hovered = hover;
                for (int r = 0; r < 5; r++) _highlight[r].SetActive(r == hover);
                ShowInfo(hover);
                if (hover >= 0) AudioDirector.Hover();
            }
            if (key >= 0) Pick(key);
            else if (hover >= 0 && mouse != null && mouse.leftButton.wasPressedThisFrame) Pick(hover);
        }

        public void Pick(int run)
        {
            if (_picked) return;
            _picked = true;
            AudioDirector.Click();
            AudioDirector.Sfx("paper", 0.7f, 1.2f);
            OnPick?.Invoke((SlopeId)run);
        }

        public void Close()
        {
            _ui?.RemoveFromHierarchy();
            Destroy(gameObject);
        }
    }
}
