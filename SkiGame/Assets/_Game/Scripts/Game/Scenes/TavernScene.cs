using System;
using System.Collections;
using System.Collections.Generic;
using Ski.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Ring = Ski.Game.MeshBuilder.Ring;

namespace Ski.Game
{
    /// <summary>
    /// The après-ski: a packed timber beer hall on New Year's Eve. Long tables with benches, people
    /// singing along shoulder to shoulder, a few dancing on the tables, a log fire, and behind the
    /// bar a painting of last year's midnight kiss. Walk around in first person; the bar offers the
    /// beer-chugging game, the door leads back out to the village. Jump onto the benches and tables;
    /// the toilet is in the corner past the bar.
    /// </summary>
    public sealed class TavernScene : MonoBehaviour
    {
        public Action OnLeave;
        public Camera Cam { get; private set; }
        public BeerGame Game { get; private set; }

        // Room: x from -8 (door wall) to 8 (bar wall), z from -11 to 11.
        const float HalfX = 8f, HalfZ = 11f, Ceiling = 4.4f, Eye = 1.66f;
        const float BenchTop = 0.465f, TableTop = 0.775f, Bpm = 132f;
        static readonly Vector3 BarSpot = new Vector3(3.9f, 0f, 0f);
        static readonly Vector3[] Tables = { new Vector3(-1.6f, 0, 3.5f), new Vector3(-1.6f, 0, 6.7f), new Vector3(-1.8f, 0, -3.6f), new Vector3(-1.8f, 0, -6.8f) };
        const float TableLen = 4.4f;

        enum Act { SitTalk, SitDrink, SitSway, Dance, BarDrink, Stand, Bartender }
        enum Knock { None, Flying, Down, GettingUp, WalkingBack }
        sealed class Guest
        {
            public Person P; public Act Kind; public int Table; public float Phase; public Pose Base;
            // Being punched: thrown, lying there, getting up, walking back to where they were.
            public Knock State; public Vector3 Centre, Vel, SpinAxis, Home; public Quaternion Rot, HomeRot;
            public float SpinRate, Timer, WalkPhase; public int Bounces;
            public Transform Tag;
            public Ragdoll Doll;
        }
        /// <summary>The ways a punch can land, each throwing a body differently.</summary>
        enum Blow { Jab, Hook, Uppercut, Gut, Haymaker }
        static readonly string[][] BlowWords = { new[] { "POW!", "BAM!" }, new[] { "WHAM!", "SMACK!" }, new[] { "UPPERCUT!", "KAPOW!" }, new[] { "OOF!", "BONK!" }, new[] { "HAYMAKER!", "KA-BOOM!" } };
        Blow _blow, _lastBlow = Blow.Haymaker;
        struct Flame { public Transform T; public float Phase, Speed, Lean; }

        // Solid things in the room, as seen from above, with how high they reach (and from how high:
        // a table top hangs above the floor). Low ones can be stepped or jumped onto.
        readonly List<Rect> _blocks = new List<Rect>();
        readonly List<float> _tops = new List<float>(), _bottoms = new List<float>();
        Transform _solids;
        const float StepUp = 0.35f, JumpSpeed = 4.8f, Gravity = 12f;
        float _vy;
        bool _grounded = true;
        static readonly Vector3 ToiletSpot = new Vector3(7.15f, 0f, -10.35f);
        readonly List<Guest> _guests = new List<Guest>();
        readonly List<Light> _lights = new List<Light>();
        readonly List<Flame> _flames = new List<Flame>();
        readonly List<Transform> _embers = new List<Transform>();
        Person _bartender;
        Light _fireLight;
        Vector3 _pos = new Vector3(-6.6f, 0f, 0f);
        float _yaw = 90f, _pitch = 4f, _time, _bob, _cheer, _punch = 1f, _powLeft;
        Transform _fist;
        Label _pow;
        public int Knockouts { get; private set; }
        VisualElement _ui;
        Label _prompt;
        CharacterId _who;
        Material _glow, _glowHot, _flameMat, _beerMat;
        MeshBuilder _beer;
        public float Wobble; // grows with every beer
        /// <summary>The frame the beer game was left (so the same Esc does not also open the pause menu).</summary>
        public int GameEndFrame = -1;

        // The DJ on the stage, the songs, and the walkman left on the bar (between the taps and the first stool,
        // at the front edge, so it is seen and reached from the floor).
        static readonly Vector3 DjDesk = new Vector3(-5.75f, 0.3f, 9.2f), WalkmanSpot = new Vector3(4.85f, 0f, -1.9f);
        const float BarTop = 1.17f;
        Person _dj;
        Transform _walkman;
        VisualElement _picker;
        float _djFlourish;
        /// <summary>True while something on screen has the keys (the beer game, the song list).</summary>
        public bool Busy => Game != null || _picker != null;
        /// <summary>Something is in reach to use (to sit at, a beer, a song, the door...).</summary>
        public bool CanUse { get; private set; }
        /// <summary>The frame something on screen let go of the keys.</summary>
        public int BusyEndFrame = -1;

        // Other people in here right now (see LiveRooms).
        sealed class LiveGuest { public GhostView V; public Guest Knock; public float Blend; public Vector3 Last; }
        struct PendingPunch { public LiveRooms.Remote By; public string Target; public float Yaw; public double At, Received; }
        readonly Dictionary<string, LiveGuest> _live = new Dictionary<string, LiveGuest>();
        readonly List<PendingPunch> _punches = new List<PendingPunch>();
        readonly List<string> _gone = new List<string>();
        readonly LiveRecorder _rec = new LiveRecorder { Mode = LiveMode.Tavern, Place = LivePlace.Tavern };
        Transform _liveRoot;
        // Being punched yourself: thrown back, down on the floor, up again.
        float _knock = -1f, _knockRoll, _knockDrop;
        Vector3 _knockVel;
        float _lastMove;
        // Sitting at a table (to talk): which table, where, facing which way.
        int _seatTable = -1;
        Vector3 _seatPos;
        float _seatSide;

        public static TavernScene Create(Transform parent, CharacterId who)
        {
            var go = new GameObject("TavernScene");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, -500f, 0f); // well away from any mountain
            var t = go.AddComponent<TavernScene>();
            t._who = who;
            t.Build();
            return t;
        }

        /// <summary>Puts the viewer somewhere in the room (for screenshots while developing).</summary>
        public void DebugView(Vector3 pos, float yaw, float pitch) { _pos = pos; _yaw = yaw; _pitch = pitch; PlaceCamera(0f); }

        /// <summary>The whole room raises its arms and roars (after a finished stein).</summary>
        public void Cheer(float seconds) { _cheer = Mathf.Max(_cheer, seconds); }

        void Build()
        {
            if (WorldLook.Instance != null) { WorldLook.Instance.Indoors = true; WorldLook.Instance.Storm = 0f; }
            _glow = Mats.Glow(Color.white, 1.5f);
            _glowHot = Mats.Glow(Color.white, 2.3f);
            _flameMat = Mats.Glow(Color.white, 1.12f);   // low enough that orange stays orange
            _beerMat = Mats.Glow(Color.white, 0.95f);
            _beer = new MeshBuilder();
            var b = new MeshBuilder();
            var glow = new MeshBuilder();
            var bulbs = new MeshBuilder();
            var rng = new Rng(1231);

            BuildShell(b, glow);
            BuildBar(b, glow, rng);
            BuildFireplace(b, glow, rng);
            foreach (var t in Tables) LongTable(b, glow, t, rng);
            b.Col = Palette.Roof;
            b.Box(new Vector3(-6.3f, 0.15f, 9.2f), new Vector3(3f, 0.3f, 3.2f)); // stage
            Block(new Rect(-7.8f, 7.6f, 3f, 3.2f), 0.3f);
            BuildDecor(b, bulbs, rng);
            BuildToilet(b, glow);

            b.ToObject("Tavern", Mats.Props, transform);
            glow.ToObject("Tavern glow", _glow, transform, false, false);
            _beer.ToObject("Tavern beer", _beerMat, transform, false, false);
            bulbs.ToObject("Tavern bulbs", _glowHot, transform, false, false);
            var sign = VillageBuilder.Sign(transform, "HAPPY NEW YEAR!", Vector3.zero, Quaternion.identity, 0.5f, Palette.White);
            sign.transform.localPosition = new Vector3(7.84f, 3.75f, 0f);
            sign.transform.localRotation = Quaternion.Euler(0, 90, 0);

            // Warm lamps down the room, pulsing gently with the music.
            foreach (var p in new[] { new Vector3(-3f, 3.4f, 6f), new Vector3(-3f, 3.4f, -4f), new Vector3(2.5f, 3.4f, 5f), new Vector3(2.5f, 3.4f, -5f), new Vector3(5.6f, 2.6f, 0f) })
                _lights.Add(AddLight(transform, p, Palette.Lamp, 12f, 14f));

            BuildCrowd(rng);
            BuildPainting();
            // The people at the tables are the house's; anyone else playing right now walks in for real.
            _liveRoot = new GameObject("LivePlayers").transform;
            _liveRoot.SetParent(transform, false);
            _rec.Character = (int)_who;
            LiveRooms.Punched += OnPunched;
            LiveRooms.Enter("tavern");

            // --- you ---------------------------------------------------------------------------
            var camGo = new GameObject("Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.06f; Cam.farClipPlane = 200f; Cam.fieldOfView = 70f;
            Cam.clearFlags = CameraClearFlags.SolidColor; Cam.backgroundColor = Palette.Ink;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGo.AddComponent<AudioListener>();
            if (WorldLook.Instance != null) WorldLook.Instance.Follow = null;
            // Your fist, out of sight until you swing.
            _fist = new GameObject("Fist").transform;
            _fist.SetParent(camGo.transform, false);
            var fistB = new MeshBuilder();
            var style = CharacterStyle.Get(_who);
            fistB.Col = style.Gloves;
            fistB.Sphere(Vector3.zero, new Vector3(0.062f, 0.058f, 0.075f), 10, 6);
            fistB.Sphere(new Vector3(-0.045f, 0.012f, 0.02f), new Vector3(0.026f, 0.03f, 0.04f), 6, 4);
            fistB.Col = style.JacketTrim;
            fistB.Cylinder(new Vector3(0f, 0f, -0.1f), 0.058f, 0.058f, 0.04f, 10);
            fistB.Col = style.Jacket;
            fistB.Capsule(new Vector3(0f, -0.01f, -0.1f), new Vector3(0.05f, -0.1f, -0.5f), 0.055f, 0.07f, 10);
            fistB.ToObject("Glove", Mats.Props, _fist, false);
            _fist.gameObject.SetActive(false);

            if (AudioDirector.I != null)
            {
                AudioDirector.I.Silence();
                PlayDj(Songs.Loaded ? null : "");   // the DJ's song (or, until the list is in, nothing yet)
                AudioDirector.I.Ambience("fire");
                AudioDirector.I.Play("door", 0.8f);
            }
            var ui = Ui.Instance;
            if (ui != null)
            {
                _ui = Ui.Div(ui.ScreenLayer, "layer");
                var box = Ui.Div(_ui, "prompt");
                _prompt = Ui.Text(box, "", "h2 outlined");
                _pow = Ui.Text(Ui.Div(_ui, "hud-centre"), "", "toast toast-hidden");
                var keys = Ui.Div(_ui, "hud-corner hud-bl");
                var more = new System.Collections.Generic.List<(string, string)> { ("E", "use") };
                if (SaveData.Voice && SaveData.Live) more.Add(("V", "talk"));
                Ui.Controls(keys, "walk", "", "", "", "CLICK", "punch", "jump", more.ToArray());
                if (Device.Touch) keys.style.display = DisplayStyle.None;     // the touch controls carry their own names
            }
            if (!Device.Touch) UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            PlaceCamera(0f);
        }

        // ---- the room ------------------------------------------------------------------------

        void BuildShell(MeshBuilder b, MeshBuilder glow)
        {
            // Floorboards, log walls, beams.
            for (int i = 0; i < 32; i++)
            {
                b.Col = i % 3 == 0 ? Palette.Timber : i % 3 == 1 ? Palette.Mul(Palette.Timber, 0.88f) : Palette.TimberLight;
                b.Box(new Vector3(-HalfX + (i + 0.5f) * 0.5f, -0.05f, 0f), new Vector3(0.5f, 0.1f, HalfZ * 2f));
            }
            for (int i = 0; i < 10; i++)
            {
                float y = (i + 0.5f) * Ceiling / 10f;
                b.Col = i % 2 == 0 ? Palette.Timber : Palette.Mul(Palette.Timber, 0.85f);
                b.Box(new Vector3(-HalfX - 0.2f, y, 0f), new Vector3(0.4f, Ceiling / 10f, HalfZ * 2f + 0.8f));
                b.Box(new Vector3(HalfX + 0.2f, y, 0f), new Vector3(0.4f, Ceiling / 10f, HalfZ * 2f + 0.8f));
                b.Box(new Vector3(0f, y, -HalfZ - 0.2f), new Vector3(HalfX * 2f, Ceiling / 10f, 0.4f));
                b.Box(new Vector3(0f, y, HalfZ + 0.2f), new Vector3(HalfX * 2f, Ceiling / 10f, 0.4f));
            }
            b.Col = Palette.TimberDark;
            b.Box(new Vector3(0f, Ceiling + 0.1f, 0f), new Vector3(HalfX * 2f + 0.8f, 0.2f, HalfZ * 2f + 0.8f));
            for (int i = -3; i <= 3; i++) b.Box(new Vector3(0f, Ceiling - 0.2f, i * 3.2f), new Vector3(HalfX * 2f, 0.3f, 0.3f));
            // Posts holding the beams.
            foreach (float z in new[] { -9.6f, 9.6f })
                foreach (float x in new[] { -7.7f, 3.2f })
                {
                    b.Box(new Vector3(x, Ceiling * 0.5f, z), new Vector3(0.3f, Ceiling, 0.3f));
                    Block(new Rect(x - 0.15f, z - 0.15f, 0.3f, 0.3f), Ceiling);
                }
            // Floor, walls and ceiling, for anyone sent flying.
            Solid(new Vector3(0f, -0.1f, 0f), new Vector3(HalfX * 2f + 1f, 0.2f, HalfZ * 2f + 1f));
            Solid(new Vector3(0f, Ceiling + 0.1f, 0f), new Vector3(HalfX * 2f + 1f, 0.2f, HalfZ * 2f + 1f));
            foreach (float sx in new[] { -1f, 1f }) Solid(new Vector3(sx * (HalfX + 0.2f), Ceiling * 0.5f, 0f), new Vector3(0.4f, Ceiling, HalfZ * 2f + 1f));
            foreach (float sz in new[] { -1f, 1f }) Solid(new Vector3(0f, Ceiling * 0.5f, sz * (HalfZ + 0.2f)), new Vector3(HalfX * 2f + 1f, Ceiling, 0.4f));

            // Door and frosty windows on the entrance wall.
            b.Col = Palette.TimberDark;
            b.Box(new Vector3(-HalfX + 0.03f, 1.1f, 0f), new Vector3(0.1f, 2.2f, 1.4f));
            b.Col = Palette.Lamp;
            b.Box(new Vector3(-HalfX + 0.1f, 1.1f, 0.5f), new Vector3(0.06f, 0.12f, 0.12f));
            foreach (float z in new[] { -6.5f, -3.4f, 3.4f, 6.5f })
            {
                glow.Col = Palette.Mix(Palette.SkyTop, Palette.SkyMid, 0.5f);
                glow.Box(new Vector3(-HalfX + 0.02f, 2.0f, z), new Vector3(0.05f, 1.2f, 1.5f));
                b.Col = Palette.TimberDark;
                b.Box(new Vector3(-HalfX + 0.05f, 2.0f, z), new Vector3(0.1f, 1.3f, 0.08f));
                b.Box(new Vector3(-HalfX + 0.05f, 2.0f, z), new Vector3(0.1f, 0.08f, 1.6f));
                b.Col = Palette.Snow;
                b.Box(new Vector3(-HalfX + 0.12f, 1.36f, z), new Vector3(0.2f, 0.08f, 1.7f));
            }
        }

        void BuildBar(MeshBuilder b, MeshBuilder glow, Rng rng)
        {
            b.Col = Palette.TimberDark;
            b.Box(new Vector3(5.2f, 0.55f, 0f), new Vector3(0.9f, 1.1f, 13f));
            for (float z = -6.2f; z < 6.3f; z += 0.62f)
            {
                b.Col = Palette.Mul(Palette.Timber, 0.9f);
                b.Box(new Vector3(4.73f, 0.55f, z), new Vector3(0.04f, 0.9f, 0.5f));   // panels on the bar front
            }
            b.Col = Palette.TimberLight;
            b.Box(new Vector3(5.1f, 1.13f, 0f), new Vector3(1.3f, 0.08f, 13.4f));
            b.Col = Palette.Steel;
            b.Tube(new Vector3(4.6f, 0.22f, -6.5f), new Vector3(4.6f, 0.22f, 6.5f), 0.03f, 6);   // foot rail
            Block(new Rect(4.45f, -6.7f, 3.6f, 13.4f), 1.17f);
            // Shelves with bottles, leaving the wall free where the painting hangs.
            for (int shelf = 0; shelf < 3; shelf++)
            {
                b.Col = Palette.Timber;
                b.Box(new Vector3(7.75f, 1.5f + shelf * 0.62f, -1.95f), new Vector3(0.4f, 0.06f, 8.1f));
                b.Box(new Vector3(7.75f, 1.5f + shelf * 0.62f, 5.3f), new Vector3(0.4f, 0.06f, 1.4f));
                for (float z = -5.8f; z < 5.9f; z += 0.34f)
                {
                    if (z > 1.9f && z < 4.7f || rng.Chance(0.2f)) continue;
                    Color c = Palette.Jackets[rng.Range(0, Palette.Jackets.Length)];
                    float h = rng.Range(0.26f, 0.4f);
                    glow.Col = Palette.Mix(c, Palette.Lamp, 0.25f);
                    glow.Cylinder(new Vector3(7.75f, 1.53f + shelf * 0.62f, z), 0.05f, 0.05f, h * 0.7f, 6);
                    glow.Cylinder(new Vector3(7.75f, 1.53f + shelf * 0.62f + h * 0.7f, z), 0.05f, 0.018f, h * 0.3f, 6);
                }
            }
            // Beer taps and stools.
            for (int i = -1; i <= 1; i++)
            {
                b.Col = Palette.Steel; b.Cylinder(new Vector3(5.3f, 1.17f, i * 0.35f), 0.03f, 0.03f, 0.42f, 6);
                b.Col = Palette.Lamp; b.Cylinder(new Vector3(5.3f, 1.58f, i * 0.35f), 0.045f, 0.03f, 0.16f, 6);
            }
            for (float z = -5f; z <= 5.1f; z += 2f)
            {
                if (Mathf.Abs(z) < 1.2f) continue; // room to stand at the taps
                b.Col = Palette.TimberDark; b.Cylinder(new Vector3(4.05f, 0f, z), 0.05f, 0.05f, 0.72f, 5); b.Cylinder(new Vector3(4.05f, 0f, z), 0.22f, 0.2f, 0.04f, 8);
                b.Col = Palette.SignalRed; b.Cylinder(new Vector3(4.05f, 0.72f, z), 0.2f, 0.2f, 0.08f, 10);
                Block(new Rect(3.8f, z - 0.25f, 0.5f, 0.5f), 0.8f);
            }
        }

        void BuildFireplace(MeshBuilder b, MeshBuilder glow, Rng rng)
        {
            // A stone chimney breast on the south wall, built stone by stone, with a deep firebox.
            const float cx = -2.5f, front = -HalfZ + 0.9f;
            b.Col = Palette.RockDark;
            b.Box(new Vector3(cx, 1.2f, -HalfZ + 0.45f), new Vector3(3.2f, 2.4f, 0.9f));
            b.TaperedBox(new Vector3(cx, 2.4f, -HalfZ + 0.45f), new Vector3(3.2f, 0, 0.9f), new Vector2(0.45f, 0.9f), Ceiling - 2.4f);
            for (int row = 0; row < 8; row++)
            {
                float y = 0.16f + row * 0.29f, x = cx - 1.6f + (row % 2) * 0.2f;
                while (x < cx + 1.55f)
                {
                    float w = rng.Range(0.3f, 0.56f);
                    bool opening = y < 1.42f && x + w > cx - 0.86f && x < cx + 0.86f;
                    if (!opening && x + w < cx + 1.62f)
                    {
                        b.Col = Palette.Mix(Palette.Rock, rng.Chance(0.5f) ? Palette.RockDark : Palette.DeadWood, rng.Range(0f, 0.55f));
                        b.Box(new Vector3(x + w * 0.5f, y, front + 0.02f), new Vector3(w - 0.035f, 0.255f, 0.1f + rng.Range(0f, 0.05f)));
                    }
                    x += w;
                }
            }
            b.Col = Palette.Ink;
            b.Box(new Vector3(cx, 0.72f, front - 0.05f), new Vector3(1.72f, 1.34f, 0.06f));           // the dark of the firebox
            b.Col = Palette.Rock;
            b.Box(new Vector3(cx, 0.06f, front + 0.4f), new Vector3(2.6f, 0.12f, 0.9f));               // hearth slab
            b.Col = Palette.TimberDark;
            b.Box(new Vector3(cx, 2.5f, front + 0.12f), new Vector3(3.5f, 0.14f, 0.42f));              // mantel beam
            // Candles on the mantel.
            foreach (float dx in new[] { -1.3f, -0.9f, 1.1f })
            {
                b.Col = Palette.White; b.Cylinder(new Vector3(cx + dx, 2.57f, front + 0.14f), 0.035f, 0.035f, 0.16f + dx * 0.02f, 6);
                glow.Col = Palette.Lamp; glow.Sphere(new Vector3(cx + dx, 2.78f + dx * 0.02f, front + 0.14f), new Vector3(0.022f, 0.05f, 0.022f), 5, 4);
            }
            Block(new Rect(-4.2f, -HalfZ, 3.4f, 1.9f), 1.2f);   // the fire is not for standing in
            Solid(new Vector3(cx, Ceiling * 0.5f, -HalfZ + 0.45f), new Vector3(3.2f, Ceiling, 0.9f));

            var fire = new GameObject("Fire").transform;
            fire.SetParent(transform, false);
            fire.localPosition = new Vector3(cx, 0.12f, front + 0.22f);
            // Logs: charred, glowing at the ends and along the cracks.
            var logs = new MeshBuilder(); var hot = new MeshBuilder();
            Vector3[,] logEnds = { { new Vector3(-0.62f, 0.07f, 0.1f), new Vector3(0.6f, 0.09f, -0.08f) }, { new Vector3(-0.5f, 0.07f, -0.16f), new Vector3(0.56f, 0.07f, 0.16f) }, { new Vector3(-0.36f, 0.2f, 0.03f), new Vector3(0.4f, 0.24f, -0.02f) } };
            for (int i = 0; i < 3; i++)
            {
                logs.Col = Palette.Mix(Palette.Ink, Palette.TimberDark, 0.45f);
                logs.Tube(logEnds[i, 0], logEnds[i, 1], 0.085f, 0.075f, 7, true);
                hot.Col = Palette.SafetyOrange;
                hot.Sphere(logEnds[i, 0], 0.06f, 6, 4); hot.Sphere(logEnds[i, 1], 0.055f, 6, 4);
                hot.Col = Palette.SignalRed;
                hot.Tube(Vector3.Lerp(logEnds[i, 0], logEnds[i, 1], 0.25f) + Vector3.up * 0.07f, Vector3.Lerp(logEnds[i, 0], logEnds[i, 1], 0.7f) + Vector3.up * 0.065f, 0.018f, 4);
            }
            hot.Col = Palette.SafetyOrange;
            hot.Sphere(new Vector3(0f, 0.01f, 0f), new Vector3(0.6f, 0.035f, 0.22f), 10, 4);   // bed of embers
            logs.ToObject("Logs", Mats.Props, fire);
            hot.ToObject("Embers", _flameMat, fire, false, false);

            // Flames: three layers of tongues, each licking and leaning on its own.
            Color[] bottom = { Palette.SafetyOrange, Palette.Lamp, Palette.Mix(Palette.Lamp, Palette.White, 0.5f) }, tip = { Palette.SignalRed, Palette.SafetyOrange, Palette.Lamp };
            float[] height = { 0.9f, 0.62f, 0.34f }, radius = { 0.25f, 0.17f, 0.1f };
            int[] count = { 6, 5, 4 };
            for (int layer = 0; layer < 3; layer++)
                for (int i = 0; i < count[layer]; i++)
                {
                    float h = height[layer] * rng.Range(0.75f, 1.15f), r = radius[layer] * rng.Range(0.8f, 1.15f);
                    var fb = new MeshBuilder();
                    fb.Loft(new[]
                    {
                        new Ring(0f, r * 0.7f, r * 0.5f).Tint(bottom[layer]),
                        new Ring(h * 0.16f, r, r * 0.7f).Tint(bottom[layer]),
                        new Ring(h * 0.45f, r * 0.72f, r * 0.5f, 0f, r * 0.15f).Tint(Color.Lerp(bottom[layer], tip[layer], 0.5f)),
                        new Ring(h * 0.75f, r * 0.36f, r * 0.26f, 0f, -r * 0.1f).Tint(tip[layer]),
                        new Ring(h, 0f, 0f, 0f, r * 0.12f).Tint(tip[layer]),
                    }, 7, true, false);
                    var t = fb.ToObject("Flame", layer == 2 ? _glow : _flameMat, fire, false, false).transform;
                    float spread = layer == 0 ? 0.52f : layer == 1 ? 0.4f : 0.26f;
                    t.localPosition = new Vector3(Mathf.Lerp(-spread, spread, count[layer] == 1 ? 0.5f : i / (float)(count[layer] - 1)) + rng.Range(-0.05f, 0.05f), 0.1f + layer * 0.03f, rng.Range(-0.06f, 0.06f) + layer * 0.03f);
                    _flames.Add(new Flame { T = t, Phase = rng.Range(0f, 30f), Speed = rng.Range(3.2f, 6.5f) * (1f + layer * 0.25f), Lean = rng.Range(14f, 30f) });
                }
            // Sparks drifting up the chimney.
            for (int i = 0; i < 10; i++)
            {
                var sb = new MeshBuilder();
                sb.Col = i % 3 == 0 ? Palette.White : Palette.Lamp;
                sb.Sphere(Vector3.zero, 0.014f, 4, 3);
                var t = sb.ToObject("Spark", _glowHot, fire, false, false).transform;
                _embers.Add(t);
            }
            _fireLight = AddLight(transform, new Vector3(cx, 0.9f, front + 1.1f), Palette.SafetyOrange, 13f, 9f);
        }

        void LongTable(MeshBuilder b, MeshBuilder glow, Vector3 c, Rng rng)
        {
            float half = TableLen * 0.5f;
            // Top of three planks, trestle ends, a stretcher, and a bench on each side.
            for (int i = -1; i <= 1; i++)
            {
                b.Col = i == 0 ? Palette.TimberLight : Palette.Mul(Palette.TimberLight, 0.9f);
                b.Box(c + new Vector3(0f, TableTop - 0.035f, i * 0.27f), new Vector3(TableLen, 0.07f, 0.265f));
            }
            b.Col = Palette.TimberDark;
            foreach (float sx in new[] { -1f, 1f })
            {
                b.Box(c + new Vector3(sx * (half - 0.45f), 0.37f, 0f), new Vector3(0.1f, 0.74f, 0.62f));
                b.Box(c + new Vector3(sx * (half - 0.45f), 0.04f, 0f), new Vector3(0.14f, 0.08f, 0.78f));
            }
            b.Box(c + new Vector3(0f, 0.3f, 0f), new Vector3(TableLen - 0.9f, 0.08f, 0.08f));
            foreach (float sz in new[] { -0.72f, 0.72f })
            {
                b.Col = Palette.Timber;
                b.Box(c + new Vector3(0f, BenchTop - 0.03f, sz), new Vector3(TableLen, 0.06f, 0.32f));
                b.Col = Palette.TimberDark;
                foreach (float sx in new[] { -1f, 1f }) b.Box(c + new Vector3(sx * (half - 0.35f), (BenchTop - 0.06f) * 0.5f, sz), new Vector3(0.08f, BenchTop - 0.06f, 0.28f));
            }
            // Table top and benches: things to stand on, and to land on.
            Block(new Rect(c.x - half, c.z - 0.4f, TableLen, 0.8f), TableTop, TableTop - 0.07f);
            foreach (float sz in new[] { -0.72f, 0.72f }) Block(new Rect(c.x - half, c.z + sz - 0.16f, TableLen, 0.32f), BenchTop, BenchTop - 0.06f);
            foreach (float sx in new[] { -1f, 1f }) Solid(c + new Vector3(sx * (half - 0.45f), 0.37f, 0f), new Vector3(0.1f, 0.74f, 0.62f));
            // Steins, a candle lantern and a basket of pretzels.
            for (int g = 0; g < 7; g++)
                Stein(_beer, b, c + new Vector3(rng.Range(-half + 0.3f, half - 0.3f), TableTop + 0.012f, rng.Range(-0.2f, 0.2f)), rng.Range(0.2f, 1f));
            b.Col = Palette.SteelDark;
            b.Cylinder(c + new Vector3(0f, TableTop, 0f), 0.06f, 0.05f, 0.03f, 6);
            glow.Col = Palette.Lamp;
            glow.Sphere(c + new Vector3(0f, TableTop + 0.1f, 0f), new Vector3(0.03f, 0.07f, 0.03f), 5, 4);
            b.Col = Palette.Bush;
            b.Cylinder(c + new Vector3(1.1f, TableTop, 0.05f), 0.11f, 0.15f, 0.07f, 8);
            b.Col = Palette.TimberLight;
            b.Sphere(c + new Vector3(1.1f, TableTop + 0.09f, 0.05f), new Vector3(0.1f, 0.04f, 0.1f), 6, 3);
        }

        void BuildDecor(MeshBuilder b, MeshBuilder bulbs, Rng rng)
        {
            // New Year: banner, balloons, strings of lights.
            b.Col = Palette.SignalRed;
            b.Box(new Vector3(7.9f, 3.75f, 0f), new Vector3(0.06f, 0.75f, 7.6f));
            Color[] party = { Palette.Lamp, Palette.SignalRed, Palette.Jackets[0], Palette.Jackets[3], Palette.PisteGreen, Palette.Jackets[1] };
            for (int i = 0; i < 9; i++)
            {
                float z = -9f + i * 2.25f;
                Vector3 a0 = new Vector3(-HalfX + 0.3f, Ceiling - 0.5f, z), a1 = new Vector3(HalfX - 0.4f, Ceiling - 0.5f, z + (i % 2 == 0 ? 1.1f : -1.1f));
                Vector3 prev = a0;
                for (int k = 1; k <= 16; k++)
                {
                    float t = k / 16f;
                    Vector3 p = Vector3.Lerp(a0, a1, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.55f;
                    b.Col = Palette.Ink; b.Tube(prev, p, 0.01f, 0.01f, 3, false);
                    if (k < 16) { bulbs.Col = party[(k + i) % party.Length]; bulbs.Sphere(p + Vector3.down * 0.07f, 0.06f, 6, 4); }
                    prev = p;
                }
            }
            for (int i = 0; i < 14; i++)
            {
                Vector3 p = new Vector3(rng.Range(-7f, 7f), Ceiling - rng.Range(0.5f, 1.0f), rng.Range(-10f, 10f));
                b.Col = party[i % party.Length];
                b.Sphere(p, new Vector3(0.2f, 0.25f, 0.2f), 8, 6);
                b.Col = Palette.Ink; b.Tube(p + Vector3.down * 0.25f, p + Vector3.down * 0.9f, 0.006f, 0.006f, 3, false);
            }
            // Old skis crossed on the north wall, and a rack of jackets by the door.
            for (int s = -1; s <= 1; s += 2)
            {
                b.Push(new Vector3(1.5f, 2.5f, HalfZ - 0.06f), Quaternion.Euler(-90f, 0f, s * 24f));
                GearModels.Ski(b, 1.9f, false, s < 0 ? Palette.SignalRed : Palette.SignalBlue, Palette.White, 0.1f);
                b.Pop();
            }
            b.Col = Palette.TimberDark;
            b.Box(new Vector3(-HalfX + 0.08f, 1.75f, -1.9f), new Vector3(0.08f, 0.1f, 1.6f));
            for (int i = 0; i < 4; i++)
            {
                b.Col = Palette.Jackets[(i * 2 + 1) % Palette.Jackets.Length];
                b.Sphere(new Vector3(-HalfX + 0.2f, 1.35f, -2.5f + i * 0.4f), new Vector3(0.14f, 0.45f, 0.2f), 7, 5);
            }
        }

        /// <summary>
        /// The toilet, in the corner past the end of the bar: an accessible one, with a wide door
        /// standing open, the wheelchair sign, a raised seat between a wall rail and a fold-down
        /// rail, a sink low enough to roll under, a tilted mirror and the red emergency cord.
        /// </summary>
        void BuildToilet(MeshBuilder b, MeshBuilder glow)
        {
            const float x0 = 4.6f, z1 = -7.4f, doorZ0 = -9.35f, doorZ1 = -8.35f, doorH = 2.2f, t = 0.12f;
            Color tile = Palette.Mix(Palette.White, Palette.SignalBlue, 0.12f), wall = Palette.TimberLight;
            Color porcelain = Palette.White, steel = Palette.Steel;
            // Walls: one along the bar's end, one facing the room with the doorway in it.
            b.Col = wall;
            b.Box(new Vector3((x0 + HalfX) * 0.5f, Ceiling * 0.5f, z1), new Vector3(HalfX - x0, Ceiling, t));
            Block(new Rect(x0, z1 - t * 0.5f, HalfX - x0, t), Ceiling);
            b.Box(new Vector3(x0, Ceiling * 0.5f, (-HalfZ + doorZ0) * 0.5f), new Vector3(t, Ceiling, doorZ0 + HalfZ));
            Block(new Rect(x0 - t * 0.5f, -HalfZ, t, doorZ0 + HalfZ), Ceiling);
            b.Box(new Vector3(x0, Ceiling * 0.5f, (doorZ1 + z1) * 0.5f), new Vector3(t, Ceiling, z1 - doorZ1));
            Block(new Rect(x0 - t * 0.5f, doorZ1, t, z1 - doorZ1), Ceiling);
            b.Box(new Vector3(x0, (doorH + Ceiling) * 0.5f, (doorZ0 + doorZ1) * 0.5f), new Vector3(t, Ceiling - doorH, doorZ1 - doorZ0));
            Block(new Rect(x0 - t * 0.5f, doorZ0, t, doorZ1 - doorZ0), Ceiling, doorH);
            b.Col = Palette.TimberDark;   // door frame
            foreach (float z in new[] { doorZ0, doorZ1 }) b.Box(new Vector3(x0, doorH * 0.5f, z), new Vector3(t + 0.04f, doorH, 0.06f));
            b.Box(new Vector3(x0, doorH, (doorZ0 + doorZ1) * 0.5f), new Vector3(t + 0.04f, 0.06f, doorZ1 - doorZ0 + 0.06f));
            // Tiles inside, on all four walls, with grout lines.
            b.Col = tile;
            b.Box(new Vector3((x0 + HalfX) * 0.5f, 1.2f, z1 - t * 0.5f - 0.011f), new Vector3(HalfX - x0, 2.4f, 0.02f));
            b.Box(new Vector3((x0 + HalfX) * 0.5f, 1.2f, -HalfZ + 0.011f), new Vector3(HalfX - x0, 2.4f, 0.02f));
            b.Box(new Vector3(HalfX - 0.011f, 1.2f, (z1 - HalfZ) * 0.5f), new Vector3(0.02f, 2.4f, z1 + HalfZ));
            b.Box(new Vector3(x0 + t * 0.5f + 0.011f, 1.2f, (-HalfZ + doorZ0) * 0.5f), new Vector3(0.02f, 2.4f, doorZ0 + HalfZ));
            b.Col = Palette.Mul(tile, 0.8f);
            for (float y = 0.3f; y < 2.4f; y += 0.3f)
            {
                b.Box(new Vector3((x0 + HalfX) * 0.5f, y, -HalfZ + 0.023f), new Vector3(HalfX - x0, 0.012f, 0.004f));
                b.Box(new Vector3(HalfX - 0.023f, y, (z1 - HalfZ) * 0.5f), new Vector3(0.004f, 0.012f, z1 + HalfZ));
            }
            // The door, wide open into the room, with a pull rail across it.
            b.Col = Palette.Hex("#3E6FA8");
            b.Box(new Vector3(x0 + 0.48f, 1.05f, doorZ0 + 0.05f), new Vector3(0.95f, 2.1f, 0.05f));
            Block(new Rect(x0, doorZ0 + 0.01f, 0.95f, 0.08f), 2.1f);
            b.Col = steel;
            b.Tube(new Vector3(x0 + 0.15f, 0.95f, doorZ0 + 0.11f), new Vector3(x0 + 0.8f, 0.95f, doorZ0 + 0.11f), 0.015f, 6);
            // The sign beside the doorway: the wheelchair symbol, white on blue, and WC above the door.
            Vector3 plaque = new Vector3(x0 - t * 0.5f - 0.012f, 1.55f, -7.88f);
            b.Col = Palette.Hex("#1F5FA8");
            b.Box(plaque, new Vector3(0.02f, 0.3f, 0.3f));
            b.Col = Palette.White;
            Vector3 o = plaque + new Vector3(-0.012f, 0f, 0f);
            b.Sphere(o + new Vector3(0f, 0.095f, 0.03f), new Vector3(0.006f, 0.022f, 0.022f), 8, 4);   // head
            b.Box(o + new Vector3(0f, 0.035f, 0.035f), new Vector3(0.006f, 0.07f, 0.024f));            // back
            b.Box(o + new Vector3(0f, -0.005f, 0.0f), new Vector3(0.006f, 0.022f, 0.07f));             // seat
            b.Box(o + new Vector3(0f, -0.045f, -0.04f), new Vector3(0.006f, 0.07f, 0.02f));            // shins
            b.Box(o + new Vector3(0f, 0.03f, -0.01f), new Vector3(0.006f, 0.016f, 0.06f));             // arm
            for (int i = 0; i < 16; i++)                                                                  // the wheel
            {
                float a0 = i / 16f * Mathf.PI * 2f, a1 = (i + 1) / 16f * Mathf.PI * 2f;
                Vector3 c = o + new Vector3(0f, -0.045f, 0.035f);
                b.Tube(c + new Vector3(0f, Mathf.Sin(a0), Mathf.Cos(a0)) * 0.065f, c + new Vector3(0f, Mathf.Sin(a1), Mathf.Cos(a1)) * 0.065f, 0.007f, 4);
            }
            var wc = VillageBuilder.Sign(transform, "WC", Vector3.zero, Quaternion.identity, 0.3f, Palette.White);
            wc.transform.localPosition = new Vector3(x0 - t * 0.5f - 0.02f, doorH + 0.32f, (doorZ0 + doorZ1) * 0.5f);
            wc.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            // The toilet: raised seat, against the back wall, a rail on the wall beside it and a fold-down one on the open side.
            Vector3 bowl = new Vector3(7.5f, 0f, -10.35f);
            b.Col = porcelain;
            b.Push(bowl, Quaternion.identity, new Vector3(1.25f, 1f, 1f));
            b.Cylinder(Vector3.zero, 0.15f, 0.19f, 0.36f, 14);
            b.Cylinder(new Vector3(0f, 0.36f, 0f), 0.2f, 0.21f, 0.06f, 16);
            b.Col = Palette.Mul(porcelain, 0.92f);
            b.Cylinder(new Vector3(0f, 0.42f, 0f), 0.205f, 0.2f, 0.03f, 16);
            b.Pop();
            b.Col = porcelain;
            b.Box(new Vector3(7.88f, 0.72f, bowl.z), new Vector3(0.22f, 0.5f, 0.44f));    // tank
            b.Col = steel;
            b.Cylinder(new Vector3(7.88f, 0.97f, bowl.z), 0.035f, 0.035f, 0.02f, 10);     // flush button
            Block(new Rect(7.2f, bowl.z - 0.24f, 0.8f, 0.48f), 0.45f);
            b.Col = steel;
            b.Tube(new Vector3(7.95f, 0.78f, -HalfZ + 0.06f), new Vector3(6.85f, 0.78f, -HalfZ + 0.06f), 0.018f, 8);   // wall rail
            foreach (float x in new[] { 7.9f, 6.9f }) b.Tube(new Vector3(x, 0.78f, -HalfZ + 0.06f), new Vector3(x, 0.78f, -HalfZ + 0.005f), 0.012f, 6);
            b.Tube(new Vector3(7.95f, 0.78f, bowl.z + 0.38f), new Vector3(7.05f, 0.78f, bowl.z + 0.38f), 0.018f, 8);     // fold-down rail, lowered
            b.Tube(new Vector3(7.05f, 0.78f, bowl.z + 0.38f), new Vector3(7.05f, 0.72f, bowl.z + 0.38f), 0.018f, 6);
            b.Col = Palette.SteelDark;
            b.Box(new Vector3(7.95f, 0.82f, bowl.z + 0.38f), new Vector3(0.06f, 0.16f, 0.1f));                          // its hinge
            b.Col = Palette.White;
            b.Cylinder(new Vector3(7.4f, 0.68f, bowl.z + 0.38f), 0.055f, 0.055f, 0.1f, 10);                             // the roll on it
            // The red emergency cord, down to the floor.
            b.Col = Palette.SignalRed;
            b.Tube(new Vector3(6.95f, Ceiling - 0.02f, -10.85f), new Vector3(6.95f, 0.12f, -10.85f), 0.006f, 4);
            b.Cylinder(new Vector3(6.95f, 0.1f, -10.85f), 0.035f, 0.035f, 0.03f, 8);
            // A sink low enough to roll under, a lever tap, a mirror tilted down.
            Vector3 sink = new Vector3(6.1f, 0.8f, z1 - t * 0.5f - 0.22f);
            b.Col = porcelain;
            b.Box(sink, new Vector3(0.55f, 0.13f, 0.42f));
            b.Col = Palette.Mul(porcelain, 0.8f);
            b.Box(sink + new Vector3(0f, 0.055f, -0.02f), new Vector3(0.4f, 0.03f, 0.28f));
            b.Col = steel;
            b.Tube(sink + new Vector3(0f, 0.06f, 0.17f), sink + new Vector3(0f, 0.2f, 0.17f), 0.016f, 6);
            b.Tube(sink + new Vector3(0f, 0.2f, 0.17f), sink + new Vector3(0f, 0.18f, 0.06f), 0.014f, 6);
            b.Box(sink + new Vector3(0.07f, 0.2f, 0.17f), new Vector3(0.14f, 0.02f, 0.025f));
            Block(new Rect(sink.x - 0.28f, sink.z - 0.21f, 0.56f, 0.42f), 0.87f, 0.7f);
            b.Push(new Vector3(sink.x, 1.5f, z1 - t * 0.5f - 0.04f), Quaternion.Euler(-8f, 0f, 0f));
            b.Col = Palette.TimberDark;
            b.Box(Vector3.zero, new Vector3(0.56f, 0.76f, 0.03f));
            b.Pop();
            glow.Col = Palette.Mix(Palette.SkyTop, Palette.White, 0.55f);
            glow.Push(new Vector3(sink.x, 1.5f, z1 - t * 0.5f - 0.06f), Quaternion.Euler(-8f, 0f, 0f));
            glow.Box(Vector3.zero, new Vector3(0.5f, 0.7f, 0.01f));
            glow.Pop();
            // A lamp of its own.
            glow.Col = Palette.Lamp;
            glow.Sphere(new Vector3(6.3f, Ceiling - 0.3f, -9.2f), new Vector3(0.18f, 0.08f, 0.18f), 8, 4);
            AddLight(transform, new Vector3(6.3f, Ceiling - 0.6f, -9.2f), Palette.Lamp, 5f, 4f);
        }

        static Light AddLight(Transform parent, Vector3 localPos, Color colour, float range, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = colour; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>The colour of glass: pale, a little blue, so an empty part of a stein still reads as glass.</summary>
        public static readonly Color GlassColour = Palette.Mix(Palette.White, Palette.SignalBlue, 0.16f);

        /// <summary>A beer stein: glass body, beer inside, foam on top and a handle.</summary>
        public static void Stein(MeshBuilder glow, MeshBuilder solid, Vector3 at, float fill, float k = 0.72f)
        {
            float f = Mathf.Clamp01(fill), h = 0.2f * k;
            glow.Col = Palette.Mix(Palette.Lamp, Palette.SafetyOrange, 0.35f);
            glow.Cylinder(at, 0.062f * k, 0.066f * k, h * f, 10);
            if (fill > 0.05f) { solid.Col = Palette.White; solid.Cylinder(at + Vector3.up * h * fill, 0.068f * k, 0.06f * k, 0.035f * k, 10); }
            // The empty part of the glass: a thin wall from the beer up to the rim, open at the top.
            if (f < 0.97f)
            {
                solid.Col = GlassColour;
                float y0 = h * f + (fill > 0.05f ? 0.03f * k : 0f);
                solid.Lathe(at, new[] { new Vector2(0.066f * k, y0), new Vector2(0.07f * k, h + 0.01f * k), new Vector2(0.062f * k, h + 0.01f * k), new Vector2(0.058f * k, y0 + 0.004f) }, 12, false, false);
            }
            solid.Col = Palette.Mix(Palette.Snow, Palette.SnowShadow, 0.35f);
            solid.Cylinder(at + Vector3.down * 0.01f * k, 0.072f * k, 0.072f * k, 0.012f, 10);
            solid.Tube(at + new Vector3(0.07f, 0.04f, 0) * k, at + new Vector3(0.12f, 0.1f, 0) * k, 0.012f * k, 0.012f * k, 4, false);
            solid.Tube(at + new Vector3(0.12f, 0.1f, 0) * k, at + new Vector3(0.07f, 0.16f, 0) * k, 0.012f * k, 0.012f * k, 4, false);
        }

        // ---- people --------------------------------------------------------------------------

        /// <summary>Sitting at a table: forearms resting on it.</summary>
        static Pose AtTable()
        {
            var p = Pose.Sit;
            p.TorsoPitch = 7f; p.ThighL = p.ThighR = 86f; p.KneeL = p.KneeR = 84f; p.LegOut = 5f;
            p.ArmFwdL = p.ArmFwdR = 30f; p.ArmOutL = p.ArmOutR = 12f; p.ElbowL = p.ElbowR = 62f; p.ArmTwistL = p.ArmTwistR = 18f;
            return p;
        }

        void GiveStein(Person p)
        {
            var solid = new MeshBuilder(); var beer = new MeshBuilder();
            Stein(beer, solid, Vector3.zero, 0.7f);
            var root = new GameObject("Stein").transform;
            root.SetParent(p.HandR, false);
            // The stein stands along the back of the fist, so raising the forearm tips it to the mouth.
            root.localPosition = new Vector3(-0.02f, -0.13f * p.Scale, -0.075f);
            root.localRotation = Quaternion.Euler(90f, 0f, 180f);
            solid.ToObject("Glass", Mats.Props, root, false);
            beer.ToObject("Beer", _beerMat, root, false, false);
        }

        void BuildCrowd(Rng rng)
        {
            PersonLook Partygoer()
            {
                var look = PersonLook.Random(rng, false);
                look.Hero = !GraphicsQuality.Light;   // close-up people: animated faces and inked shading (not on low)
                look.Gear = GearKind.None; look.Poles = false; look.HasHelmet = false; look.GogglesDown = false;
                look.HasGoggles = rng.Chance(0.3f);
                look.Beanie = rng.Chance(0.4f); look.Helmet = Palette.Jackets[rng.Range(0, Palette.Jackets.Length)];
                look.Boots = Palette.Ink;
                return look;
            }
            Guest Add(Vector3 at, float yaw, Act kind, int table = -1)
            {
                var p = Person.Create(Partygoer(), transform, "Guest");
                p.transform.localPosition = at;
                p.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                var g = new Guest { P = p, Kind = kind, Table = table, Phase = rng.Range(0f, 20f) };
                g.Base = kind == Act.SitTalk || kind == Act.SitDrink || kind == Act.SitSway ? AtTable() : Pose.Stand;
                p.Snap(g.Base);
                p.SetGogglesUp(true, true);
                p.Smile = rng.Range(0.4f, 0.9f);
                p.PoseSpeed = 7f;
                if (kind == Act.SitDrink || kind == Act.BarDrink) GiveStein(p);
                _guests.Add(g);
                return g;
            }

            // Along the benches, both sides of every table.
            for (int t = 0; t < Tables.Length; t++)
            {
                bool swaying = t % 2 == 0;   // these tables link arms and sway to the song
                for (int side = -1; side <= 1; side += 2)
                    for (int seat = 0; seat < 4; seat++)
                    {
                        if (rng.Chance(0.36f)) continue;
                        Vector3 at = Tables[t] + new Vector3((seat - 1.5f) * 1.02f + rng.Range(-0.08f, 0.08f), BenchTop, side * 0.74f);
                        Act kind = swaying ? (rng.Chance(0.3f) ? Act.SitDrink : Act.SitSway) : rng.Chance(0.4f) ? Act.SitDrink : Act.SitTalk;
                        Add(at, side > 0 ? 180f : 0f, kind, t);
                    }
            }
            // Dancing on the tables, in ski boots, obviously.
            Add(Tables[0] + new Vector3(0.9f, TableTop + 0.01f, 0f), 200f, Act.Dance, 0);
            Add(Tables[1] + new Vector3(-0.8f, TableTop + 0.01f, 0.02f), 150f, Act.Dance, 1);
            Add(Tables[3] + new Vector3(0.3f, TableTop + 0.01f, 0f), 20f, Act.Dance, 3);
            // A little crowd dancing in front of the DJ.
            foreach (var at in new[] { new Vector3(-4.2f, 0f, 8.3f), new Vector3(-3.5f, 0f, 9.1f), new Vector3(-4.1f, 0f, 10.2f), new Vector3(-3.0f, 0f, 10.1f), new Vector3(-2.9f, 0f, 8.5f), new Vector3(-4.4f, 0f, 9.3f) })
                Add(at, -90f + rng.Range(-30f, 30f), Act.Dance);
            // Standing about with a glass.
            foreach (float z in new[] { -4.4f, -2.9f, 2.7f, 4.6f })
                Add(new Vector3(3.75f, 0, z + rng.Range(-0.2f, 0.2f)), 90f + rng.Range(-25f, 25f), Act.BarDrink);
            Add(new Vector3(-0.2f, 0f, -9.3f), 250f, Act.Stand);   // warming up by the fire

            var barLook = Partygoer();
            barLook.Jacket = Palette.White; barLook.Trim = Palette.SignalRed; barLook.Beanie = false; barLook.HasGoggles = false; barLook.Hood = false;
            _bartender = Person.Create(barLook, transform, "Bartender");
            _bartender.transform.localPosition = new Vector3(6.2f, 0, 0f);
            _bartender.transform.localRotation = Quaternion.Euler(0, -90, 0);
            _bartender.Snap(Pose.Stand.WithArms(40, 10, 70, 40, 10, 70));
            _bartender.Smile = 0.6f;
            _guests.Add(new Guest { P = _bartender, Kind = Act.Bartender, Table = -1, Base = Pose.Stand.WithArms(40, 10, 70, 40, 10, 70) });
            foreach (var g in _guests) { g.Home = g.P.transform.localPosition; g.HomeRot = g.P.transform.localRotation; }
            BuildDj(rng);
        }

        void BuildDj(Rng rng)
        {
            var b = new MeshBuilder();
            var glow = new MeshBuilder();
            b.Push(DjDesk); glow.Push(DjDesk);   // the lights belong on the booth, not in the middle of the room
            // The booth: a desk with two decks and a mixer, speakers either side, lights along the front.
            b.Col = Palette.Ink;
            b.Box(new Vector3(0f, 0.5f, 0f), new Vector3(0.7f, 1.0f, 2.1f));
            b.Col = Palette.Hex("#3B3550");
            b.Box(new Vector3(0f, 1.02f, 0f), new Vector3(0.75f, 0.05f, 2.2f));
            for (int s = -1; s <= 1; s += 2)
            {
                b.Col = Palette.Hex("#2A2638");
                b.Cylinder(new Vector3(0f, 1.045f, s * 0.6f), 0.21f, 0.21f, 0.03f, 18);
                b.Col = Palette.Ink;
                b.Cylinder(new Vector3(0f, 1.075f, s * 0.6f), 0.15f, 0.15f, 0.01f, 18);
                b.Col = Palette.SignalRed;
                b.Cylinder(new Vector3(0f, 1.085f, s * 0.6f), 0.04f, 0.04f, 0.01f, 10);
                b.Col = Palette.SteelDark;
                b.Box(new Vector3(0.12f, 1.07f, s * 0.6f + s * 0.17f), new Vector3(0.03f, 0.02f, 0.16f));   // tone arm
                // Speakers, tall, on the stage floor.
                b.Col = Palette.Ink;
                b.Box(new Vector3(-0.2f, 0.75f, s * 1.5f), new Vector3(0.6f, 1.5f, 0.55f));
                b.Col = Palette.Hex("#3B3550");
                b.Cylinder(new Vector3(0.11f, 1.05f, s * 1.5f), 0.2f, 0.2f, 0.01f, 16);
                b.Cylinder(new Vector3(0.11f, 0.45f, s * 1.5f), 0.12f, 0.12f, 0.01f, 16);
            }
            b.Col = Palette.SteelDark;
            b.Box(new Vector3(0f, 1.06f, 0f), new Vector3(0.4f, 0.04f, 0.5f));                         // mixer
            for (int i = 0; i < 6; i++)
            {
                glow.Col = i % 2 == 0 ? Palette.Hex("#FF4FA3") : Palette.Hex("#4FD8FF");
                glow.Box(new Vector3(0.36f, 0.12f + i * 0.075f, 0f), new Vector3(0.02f, 0.035f, 1.9f));
            }
            b.Pop(); glow.Pop();
            Block(new Rect(DjDesk.x - 0.35f, DjDesk.z - 1.05f, 0.7f, 2.1f), DjDesk.y + 1.05f);
            foreach (float sz in new[] { -1.5f, 1.5f }) Block(new Rect(DjDesk.x - 0.5f, DjDesk.z + sz - 0.275f, 0.6f, 0.55f), DjDesk.y + 1.5f);
            b.ToObject("DJ booth", Mats.Props, transform);
            glow.ToObject("DJ lights", _glow, transform, false, false);
            var sign = VillageBuilder.Sign(transform, "DJ", Vector3.zero, Quaternion.identity, 0.5f, Palette.White);
            sign.transform.localPosition = DjDesk + new Vector3(0.37f, 0.76f, 0f);
            sign.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

            // The DJ, behind the decks, headphones on.
            var look = PersonLook.Random(rng, false);
            look.Hero = true; look.Gear = GearKind.None; look.Poles = false; look.HasHelmet = false; look.Beanie = true; look.HasGoggles = false;
            look.Jacket = Palette.Hex("#22202E"); look.Trim = Palette.Hex("#FF4FA3"); look.Helmet = Palette.Hex("#FF4FA3");
            _dj = Person.Create(look, transform, "DJ");
            _dj.transform.localPosition = DjDesk + new Vector3(-0.75f, 0f, 0f);
            _dj.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            _dj.Snap(Pose.Stand);
            _dj.Smile = 0.7f;
            var hp = new MeshBuilder();
            float hr = 0.13f;
            hp.Col = Palette.Ink;
            for (int i = 0; i < 8; i++)
            {
                float a0 = Mathf.Lerp(-80f, 80f, i / 8f) * Mathf.Deg2Rad, a1 = Mathf.Lerp(-80f, 80f, (i + 1) / 8f) * Mathf.Deg2Rad;
                hp.Tube(new Vector3(Mathf.Sin(a0), Mathf.Cos(a0) + 0.75f, -0.1f) * hr, new Vector3(Mathf.Sin(a1), Mathf.Cos(a1) + 0.75f, -0.1f) * hr, 0.012f, 5);
            }
            hp.Col = Palette.Hex("#FF4FA3");
            hp.Cylinder(new Vector3(-0.93f, 0.62f, -0.1f) * hr, 0.055f, 0.055f, 0.06f, 12);
            hp.Cylinder(new Vector3(0.93f, 0.62f, -0.1f) * hr, 0.055f, 0.055f, 0.06f, 12);
            var hpGo = hp.ToObject("Headphones", Mats.People, _dj.Head, false);
            hpGo.transform.localPosition = Vector3.zero;
            _guests.Add(new Guest { P = _dj, Kind = Act.Stand, Table = -1, Base = Pose.Stand, Home = _dj.transform.localPosition, HomeRot = _dj.transform.localRotation });
            _djGuest = _guests[_guests.Count - 1];

            // The walkman, left on the bar, its headphones beside it; it turns slowly so it catches the eye.
            if (!SaveData.HasWalkman)
            {
                _walkman = new GameObject("Walkman").transform;
                _walkman.SetParent(transform, false);
                _walkman.localPosition = WalkmanSpot + new Vector3(0f, BarTop + 0.005f, 0f);
                _walkman.localRotation = Quaternion.Euler(0f, 20f, 0f);
                _walkman.localScale = Vector3.one * 1.4f;                       // a little larger than life, to be noticed
                var w = new MeshBuilder();
                w.Col = Palette.Hex("#E8455A");
                w.Box(new Vector3(0f, 0.018f, 0f), new Vector3(0.12f, 0.036f, 0.085f));
                w.Col = Palette.Hex("#22202E");
                w.Box(new Vector3(0f, 0.037f, 0.005f), new Vector3(0.08f, 0.004f, 0.05f));
                w.Col = Palette.Hex("#FFC64A");
                w.Box(new Vector3(-0.045f, 0.038f, -0.03f), new Vector3(0.02f, 0.006f, 0.012f));
                w.Col = Palette.Ink;
                for (int i = 0; i < 8; i++)
                {
                    float a0 = Mathf.Lerp(0f, 180f, i / 8f) * Mathf.Deg2Rad, a1 = Mathf.Lerp(0f, 180f, (i + 1) / 8f) * Mathf.Deg2Rad;
                    w.Tube(new Vector3(0.2f + Mathf.Cos(a0) * 0.08f, 0.012f, Mathf.Sin(a0) * 0.08f), new Vector3(0.2f + Mathf.Cos(a1) * 0.08f, 0.012f, Mathf.Sin(a1) * 0.08f), 0.008f, 4);
                }
                w.Col = Palette.Hex("#FFC64A");
                w.Cylinder(new Vector3(0.12f, 0f, 0f), 0.035f, 0.035f, 0.025f, 10);
                w.Cylinder(new Vector3(0.28f, 0f, 0f), 0.035f, 0.035f, 0.025f, 10);
                w.ToObject("Walkman model", Mats.Props, _walkman);
            }

            // The DJ plays the songs, one after another, and takes requests.
            if (AudioDirector.I != null)
            {
                AudioDirector.I.DjHasFloor = true;
                AudioDirector.I.SongEnded += OnSongEnded;
            }
            Songs.LoadList(this, () => { if (this != null) PlayDj(null); });
        }

        Guest _djGuest;

        /// <summary>The DJ puts on a song: the one asked for, or another at random.</summary>
        void PlayDj(string id)
        {
            if (AudioDirector.I == null) return;
            if (id == "") return;
            if (Songs.All.Count == 0) { AudioDirector.I.Music("music_tavern", 1.2f); return; }
            if (id == null)
            {
                string now = AudioDirector.I.CurrentSong;
                int i = UnityEngine.Random.Range(0, Songs.All.Count);
                if (Songs.All.Count > 1 && Songs.All[i].id == now) i = (i + 1) % Songs.All.Count;
                id = Songs.All[i].id;
            }
            bool same = AudioDirector.I.CurrentSong == id;
            AudioDirector.I.Music("song:" + id, 1.1f);
            if (same) AudioDirector.I.RestartMusic();
            _djFlourish = 2.5f;
        }

        void OnSongEnded(string id) { if (this != null) PlayDj(null); }

        void OpenPicker()
        {
            var ui = Ui.Instance;
            if (ui == null || Songs.All.Count == 0) return;
            _picker = Ui.Div(ui.ScreenLayer, "layer dim");
            var card = Ui.Div(_picker, "card shadowed dialog tilt-left");
            Ui.Text(card, "REQUESTS", "h1");
            string now = AudioDirector.I != null ? AudioDirector.I.CurrentSong : null;
            foreach (var s in Songs.All)
            {
                var song = s;
                var row = Ui.Btn(card, s.title + "   -   " + s.style, () =>
                {
                    ClosePicker();
                    PlayDj(song.id);
                    SaveData.Record(_who, p => p.songsRequested++);
                }, "btn-plain song-row" + (s.id == now ? " btn-selected" : ""));
            }
            if (Device.Touch) Ui.Btn(card, "CLOSE", ClosePicker, "btn-plain");
            else Ui.Keys(card, "ESC", "close");
            UnityEngine.Cursor.lockState = CursorLockMode.None;
        }

        void ClosePicker()
        {
            _picker?.RemoveFromHierarchy();
            _picker = null;
            BusyEndFrame = Time.frameCount;
            if (!Device.Touch) UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        }

        /// <summary>Behind the decks: nodding to the beat, a hand on the headphones, now and then a fist in the air.</summary>
        void AnimateDj(float dt)
        {
            if (_dj == null || _djGuest.State != Knock.None) return;
            float beat = _time * Mathf.PI * Bpm / 60f;
            _djFlourish = Mathf.Max(0f, _djFlourish - dt);
            var p = Pose.Stand;
            p.HeadPitch = 8f + Mathf.Abs(Mathf.Sin(beat)) * 14f;
            p.TorsoPitch = 10f + Mathf.Abs(Mathf.Sin(beat)) * 4f;
            p.KneeL = p.KneeR = 10f + Mathf.Abs(Mathf.Sin(beat)) * 10f; p.ThighL = p.ThighR = 5f + Mathf.Abs(Mathf.Sin(beat)) * 5f;
            p.ArmFwdR = 55f; p.ElbowR = 55f + Mathf.Sin(beat * 0.5f) * 10f;                                   // a hand on the deck
            bool pump = _djFlourish > 0f || Mathf.Repeat(_time * Bpm / 60f / 16f, 1f) > 0.82f;
            if (pump) { p.ArmOutL = 150f; p.ElbowL = 20f + Mathf.Abs(Mathf.Sin(beat)) * 40f; p.HeadPitch = -10f; }
            else { p.ArmFwdL = 30f; p.ArmOutL = 40f; p.ElbowL = 150f; p.HeadRoll = -10f; }                   // holding the headphones to an ear
            _dj.Target = p;
        }

        void AnimateCrowd(float dt)
        {
            float beatPhase = _time * Mathf.PI * Bpm / 60f;
            bool cheering = _cheer > 0f;
            foreach (var g in _guests)
            {
                var p = g.P;
                if (g.Tag != null && Cam != null) g.Tag.rotation = Quaternion.LookRotation(g.Tag.position - Cam.transform.position, Vector3.up);
                if (g.State != Knock.None) { AnimateKnocked(g, dt); continue; }
                if (g.Kind == Act.Bartender) continue;   // the beer game directs the bartender
                float t = _time + g.Phase;
                Pose pose = g.Base;
                bool seated = g.Kind == Act.SitTalk || g.Kind == Act.SitDrink || g.Kind == Act.SitSway;
                if (cheering)
                {
                    // Arms in the air, bouncing, everybody.
                    float k = Mathf.Sin(beatPhase * 2f + g.Phase);
                    pose.ArmOutL = 138f + k * 16f; pose.ArmOutR = 138f - k * 16f; pose.ArmFwdL = pose.ArmFwdR = 24f; pose.ElbowL = pose.ElbowR = 22f + k * 10f;
                    pose.ArmTwistL = pose.ArmTwistR = 0f;
                    pose.TorsoPitch = seated ? -6f : -4f; pose.HeadPitch = -14f;
                    p.MouthOpen = 0.8f; p.BrowRaise = 0.7f; p.Talking = false;
                    p.Target = pose;
                    continue;
                }
                p.MouthOpen = 0f;
                switch (g.Kind)
                {
                    case Act.SitSway:
                    {
                        // Arms over the neighbours' shoulders, the whole bench rocking left and right, singing.
                        float sway = Mathf.Sin(beatPhase * 0.5f + g.Table * 1.3f);
                        pose.TorsoRoll = sway * 11f; pose.HeadRoll = sway * 5f; pose.TorsoPitch = 2f;
                        pose.ArmOutL = pose.ArmOutR = 74f; pose.ArmFwdL = pose.ArmFwdR = 26f; pose.ElbowL = pose.ElbowR = 34f; pose.ArmTwistL = pose.ArmTwistR = 0f;
                        p.Talking = true; p.BrowRaise = 0.35f;
                        break;
                    }
                    case Act.SitTalk:
                    {
                        // Leaning in, turning to a neighbour, one hand making the point.
                        float turn = Mathf.Sin(t * 0.35f);
                        bool speaking = Mathf.Sin(t * 0.42f) > -0.1f;
                        float gesture = speaking ? Mathf.Max(0f, Mathf.Sin(t * 2.3f)) : 0f;
                        pose.HeadYaw = turn * 42f; pose.TorsoYaw = turn * 14f; pose.TorsoPitch = 9f + gesture * 5f;
                        pose.ArmFwdR += gesture * 26f; pose.ElbowR += gesture * 24f; pose.ArmOutR += gesture * 12f;
                        pose.HeadRoll = Mathf.Sin(t * 0.9f) * 5f;
                        p.Talking = speaking; p.BrowRaise = speaking ? 0.3f : 0f;
                        break;
                    }
                    case Act.SitDrink:
                    case Act.BarDrink:
                    {
                        // The glass comes up for a pull every so often; in between it rests.
                        float cyc = Mathf.Repeat(t * 0.16f, 1f);
                        float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(cyc * 12f)) * Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((cyc - 0.2f) * 12f));
                        if (g.Kind == Act.BarDrink) { pose.ArmFwdR = 34f; pose.ElbowR = 58f; pose.ArmOutR = 10f; pose.ArmTwistR = 10f; pose.ArmFwdL = 6f; pose.ElbowL = 16f; }
                        pose.ArmFwdR = Mathf.Lerp(pose.ArmFwdR, 66f, lift); pose.ElbowR = Mathf.Lerp(pose.ElbowR, 126f, lift);
                        pose.ArmTwistR = Mathf.Lerp(pose.ArmTwistR, 30f, lift); pose.HeadPitch = Mathf.Lerp(0f, -16f, lift);
                        pose.TorsoRoll = Mathf.Sin(beatPhase * 0.5f + g.Phase) * 3f;
                        pose.HeadYaw = (1f - lift) * Mathf.Sin(t * 0.3f) * 30f;
                        p.Talking = lift < 0.05f && Mathf.Sin(t * 0.5f) > 0.3f;
                        break;
                    }
                    case Act.Dance:
                    {
                        // Bounce on the beat, knees pumping, arms punching the air.
                        var d = Pose.Cheer;
                        float k = Mathf.Sin(beatPhase + g.Phase), k2 = Mathf.Sin(beatPhase * 0.5f + g.Phase);
                        d.ThighL = 12f + k * 12f; d.KneeL = 24f + k * 24f; d.ThighR = 12f - k * 12f; d.KneeR = 24f - k * 24f;
                        d.ArmOutL = 120f + k * 30f; d.ArmOutR = 120f - k * 30f; d.ElbowL = 30f + k2 * 25f; d.ElbowR = 30f - k2 * 25f;
                        d.TorsoRoll = k * 8f; d.TorsoYaw = k2 * 22f; d.HeadRoll = -k * 6f;
                        pose = d;
                        p.Talking = true; p.BrowRaise = 0.4f;
                        break;
                    }
                    default:
                        pose.TorsoRoll = Mathf.Sin(beatPhase * 0.5f + g.Phase) * 3f;
                        pose.ArmFwdL = pose.ArmFwdR = 50f; pose.ElbowL = pose.ElbowR = 30f;   // hands out to the fire
                        break;
                }
                p.Target = pose;
            }
        }

        // ---- punching ----------------------------------------------------------------------------

        /// <summary>A different blow each time: mostly jabs, hooks, uppercuts and body blows, now and then a haymaker.</summary>
        Blow PickBlow()
        {
            Blow b;
            do
            {
                float r = UnityEngine.Random.value;
                b = r < 0.24f ? Blow.Jab : r < 0.48f ? Blow.Hook : r < 0.7f ? Blow.Uppercut : r < 0.9f ? Blow.Gut : Blow.Haymaker;
            } while (b == _lastBlow);
            return _lastBlow = b;
        }

        static Blow BlowFromYaw(float yaw) => (Blow)(Mathf.Abs(Mathf.RoundToInt(yaw * 7.3f)) % 5);

        void Punch()
        {
            _punch = 0f;
            _blow = PickBlow();
            AudioDirector.Sfx("jump", 0.5f, 1.5f);
            // Whoever is nearest in front of the fist takes it.
            Vector3 eye = _pos + Vector3.up * 1.3f;
            Vector3 fwd = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            Guest hit = null; float best = float.MaxValue;
            foreach (var g in _guests)
            {
                if (g.State == Knock.Flying) continue;
                Vector3 to = g.P.Hips.position - transform.position - eye;
                to.y = 0f;
                float d = to.magnitude, reach = g.Kind == Act.Bartender ? 2.7f : 2.0f;
                if (d > reach || d > 0.2f && Vector3.Dot(to / d, fwd) < 0.72f) continue;
                if (d < best) { best = d; hit = g; }
            }
            // Other players count too: the punch goes to them, and they are sent flying on every screen.
            LiveGuest liveHit = null; string liveId = null;
            foreach (var kv in _live)
            {
                var lg = kv.Value;
                if (lg.Knock != null || !lg.V.gameObject.activeSelf) continue;
                Vector3 to = lg.V.transform.localPosition - eye; to.y = 0f;
                float d = to.magnitude;
                if (d > 2.0f || d > 0.2f && Vector3.Dot(to / d, fwd) < 0.72f) continue;
                if (d < best) { best = d; liveHit = lg; liveId = kv.Key; hit = null; }
            }
            if (liveHit != null)
            {
                _blow = BlowFromYaw(_yaw);   // every screen works out the same blow from the punch's direction
                LiveRooms.SendPunch(liveId, _yaw);
                LaunchLive(liveHit, fwd);
            }
            else if (hit == null) return;
            else Launch(hit, fwd, 1f, _blow);
            Knockouts++;
            SaveData.Record(_who, p => p.knockouts++);
            AudioDirector.Sfx("land_heavy", 1f, 1.25f);
            AudioDirector.Sfx("crash", 0.6f, 1.2f);
            if (_pow != null)
            {
                var words = BlowWords[(int)_blow];
                _pow.text = words[Knockouts % words.Length];
                _pow.RemoveFromClassList("toast-hidden");
                _powLeft = 0.55f;
            }
        }

        /// <summary>Sends a guest flying, limp: how depends on the blow (straight back, spun round, up and over, folded in half, or across the room).</summary>
        void Launch(Guest g, Vector3 dir, float power, Blow blow)
        {
            var p = g.P;
            p.Talking = false; p.MouthOpen = 1f; p.BrowRaise = 1f; p.Smile = 0f; p.EyesClosed = false;
            if (g.Doll == null)
            {
                foreach (var smr in p.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
                g.Doll = Ragdoll.Make(p);
            }
            g.Doll.Age = 0f;
            g.State = Knock.Flying; g.Timer = 0f; g.Bounces = 0;
            dir.y = 0f; dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized, up = Vector3.up;
            float k = power;
            var d = g.Doll;
            switch (blow)
            {
                case Blow.Jab:      d.Throw((dir * 5f + up * 1.6f) * k, side * 3f * k, d.HeadBody, dir * 5f * k); break;
                case Blow.Hook:     d.Throw((dir * 3.8f + side * 2.6f + up * 2.2f) * k, (up * 10f - dir * 3f) * k, d.HeadBody, side * 7f * k); break;
                case Blow.Uppercut: d.Throw((dir * 3f + up * 6.2f) * k, -side * 7.5f * k, d.HeadBody, (up * 5f + dir * 2f) * k); break;
                case Blow.Gut:      d.Throw((dir * 6.5f + up * 2.4f) * k, side * 5f * k, d.Pelvis, dir * 4.5f * k); break;
                default:            d.Throw((dir * 11f + up * 4.6f) * k, (up * 6f - side * 8f) * k, d.HeadBody, dir * 6f * k); break;
            }
            g.Vel = d.Pelvis.linearVelocity;
        }

        // ---- live players ------------------------------------------------------------------

        void OnPunched(LiveRooms.Remote by, string target, float yaw, double at)
        {
            if (this == null) return;
            // Somebody hit you: that lands the moment it arrives.
            if (target == LiveRooms.MyId) { KnockMe(yaw); return; }
            // Somebody hit somebody: shown when the thrower's replay reaches the punch.
            _punches.Add(new PendingPunch { By = by, Target = target, Yaw = yaw, At = at, Received = LiveRooms.Now });
        }

        void LaunchLive(LiveGuest lg, Vector3 dir)
        {
            var v = lg.V;
            // The body leaves its view and flies through the room on its own; it comes back when it is up again.
            Vector3 at = v.transform.localPosition; Quaternion rot = v.transform.localRotation;
            v.transform.localPosition = Vector3.zero; v.transform.localRotation = Quaternion.identity;
            v.Body.transform.localPosition = at; v.Body.transform.localRotation = rot;
            v.ShowName(false);
            lg.Knock = new Guest { P = v.Body, Kind = Act.Stand, Home = at, HomeRot = rot, Base = Pose.Stand };
            Launch(lg.Knock, dir, 1f, BlowFromYaw(Quaternion.LookRotation(dir).eulerAngles.y));
            AudioDirector.Sfx("land_heavy", 0.8f, 1.2f);
        }

        void KnockMe(float yaw)
        {
            if (_knock >= 0f || Game != null) return;
            _knock = 0f;
            _knockVel = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 7f;
            AudioDirector.Sfx("land_heavy", 1f, 0.9f);
            AudioDirector.Sfx("crash", 0.7f, 1.1f);
            if (_pow != null) { _pow.text = "OOF!"; _pow.RemoveFromClassList("toast-hidden"); _powLeft = 0.8f; }
        }

        /// <summary>Your own knockdown: flying back for half a second, a moment on the floor, then up.</summary>
        bool AnimateMyKnock(float dt)
        {
            if (_knock < 0f) return false;
            _knock += dt;
            if (_knock < 0.55f)
            {
                TryMove(_knockVel * dt);
                _knockVel *= Mathf.Exp(-3f * dt);
                _knockRoll = Mathf.Lerp(_knockRoll, 75f, 1f - Mathf.Exp(-10f * dt));
                _knockDrop = Mathf.Lerp(_knockDrop, 1.3f, 1f - Mathf.Exp(-9f * dt));
                _pitch = Mathf.Lerp(_pitch, 40f, 1f - Mathf.Exp(-8f * dt));
            }
            else if (_knock > 2.3f)
            {
                _knockRoll = Mathf.Lerp(_knockRoll, 0f, 1f - Mathf.Exp(-7f * dt));
                _knockDrop = Mathf.Lerp(_knockDrop, 0f, 1f - Mathf.Exp(-6f * dt));
                if (_knock > 3.1f) { _knock = -1f; _knockRoll = _knockDrop = 0f; }
            }
            PlaceCamera(dt);
            return true;
        }

        void SyncLive(float dt)
        {
            double now = LiveRooms.Now;
            foreach (var r in LiveRooms.Others.Values)
            {
                bool there = r.Timeline.Visible(now) && r.Timeline.At(now, out var s, out float speed, out var mode) && mode == LiveMode.Tavern;
                if (_live.TryGetValue(r.Id, out var lg) && lg.Knock == null && lg.V.OutfitCode != r.Outfit) { Destroy(lg.V.gameObject); _live.Remove(r.Id); lg = null; }
                if (lg == null)
                {
                    if (!there) continue;
                    var v = GhostView.Create(_liveRoot, r.Name, r.Who, true, true, r.Outfit);
                    _live[r.Id] = lg = new LiveGuest { V = v };
                }
                lg.V.Rename(r.Name);
                if (lg.Knock != null)
                {
                    AnimateKnocked(lg.Knock, dt);
                    if (lg.Knock.State == Knock.WalkingBack || lg.Knock.State == Knock.None)
                    {
                        // Up again: back into their own movement, blending from where they got up.
                        var p = lg.Knock.P;
                        lg.Last = p.transform.localPosition; lg.Last.y = 0f;
                        p.transform.localPosition = Vector3.zero; p.transform.localRotation = Quaternion.identity;
                        p.Snap(Pose.Stand); p.EyesClosed = false; p.MouthOpen = 0f; p.BrowRaise = 0f;
                        lg.Knock = null; lg.Blend = 1f;
                        lg.V.ShowName(true);
                    }
                    continue;
                }
                if (!there || !r.Timeline.At(now, out s, out speed, out mode)) { lg.V.Hide(); continue; }
                Vector3 pos = new Vector3(s.Pos.x, 0f, s.Pos.z);
                lg.V.SetTalking(VoiceChat.IsTalking(r.Id));
                if ((s.Flags & LiveFlags.Sit) != 0) { lg.V.ShowSeated(pos + Vector3.up * BenchTop, s.Yaw, Cam); continue; }
                if (lg.Blend > 0f) { lg.Blend = Mathf.Max(0f, lg.Blend - dt / 1.2f); pos = Vector3.Lerp(pos, lg.Last, lg.Blend); speed = Mathf.Max(speed, 1f); }
                lg.V.ShowOnFoot(pos, s.Yaw, speed, Cam, dt, (s.Flags & LiveFlags.Punch) != 0);
            }
            // Punches between other people, when the thrower's replay gets there (or soon, whatever happens).
            for (int i = _punches.Count - 1; i >= 0; i--)
            {
                var pp = _punches[i];
                double t = pp.By.Timeline.PlaybackTime(now);
                if (!(t >= pp.At) && now - pp.Received < 6.0) continue;
                _punches.RemoveAt(i);
                if (_live.TryGetValue(pp.Target, out var target) && target.Knock == null && target.V.gameObject.activeSelf)
                    LaunchLive(target, Quaternion.Euler(0f, pp.Yaw, 0f) * Vector3.forward);
            }
            _gone.Clear();
            foreach (var id in _live.Keys) if (!LiveRooms.Others.ContainsKey(id)) _gone.Add(id);
            foreach (var id in _gone) { Destroy(_live[id].V.gameObject); _live.Remove(id); }

            // Where you are, for everyone else. Standing still, a packet every five seconds is plenty.
            var flags = LiveFlags.None;
            if (_punch < 1f) flags |= LiveFlags.Punch;
            if (_knock >= 0f) flags |= LiveFlags.Down;
            if (_seatTable >= 0) flags |= LiveFlags.Sit;
            VoiceChat.MyPos = _pos; VoiceChat.Place = "tavern"; VoiceChat.MyTable = _seatTable;
            _rec.PacketSeconds = Time.time - _lastMove < 2f || flags != LiveFlags.None ? 2f : 4.9f;
            LiveRooms.Send(_rec, now, new LiveSample { Pos = new Vector3(_pos.x, _pos.y, _pos.z), Yaw = _yaw, Flags = flags });
        }

        float FloorAt(Vector3 c)
        {
            foreach (var t in Tables)
                if (Mathf.Abs(c.x - t.x) < TableLen * 0.5f && Mathf.Abs(c.z - t.z) < 0.42f) return TableTop;   // you can land on a table
            return 0f;
        }

        void AnimateKnocked(Guest g, float dt)
        {
            var p = g.P;
            g.Timer += dt;
            const float body = 0.32f, hipHeight = 0.9f;
            switch (g.State)
            {
                case Knock.Flying:
                {
                    var doll = g.Doll;
                    if (doll == null) { g.State = Knock.GettingUp; g.Timer = 0.9f; break; }
                    doll.Age += dt;
                    Vector3 vel = doll.Pelvis.linearVelocity;
                    // A thud for every hard knock against the floor, a table, a wall.
                    float jolt = (vel - g.Vel).magnitude;
                    if (jolt > 2.6f && g.Bounces < 6) { g.Bounces++; Thud(Mathf.Clamp01(jolt / 9f)); }
                    g.Vel = vel;
                    // Anybody in the way goes down too.
                    if (vel.sqrMagnitude > 12f)
                    {
                        Vector3 at = transform.InverseTransformPoint(doll.Pelvis.position);
                        foreach (var o in _guests)
                        {
                            if (o == g || o.State != Knock.None) continue;
                            if ((transform.InverseTransformPoint(o.P.Hips.position) - at).sqrMagnitude > 0.7f * 0.7f) continue;
                            Launch(o, vel, 0.6f, Blow.Gut);
                            Thud(0.8f);
                        }
                    }
                    // Come to rest (or long enough): lie there a moment.
                    if (doll.Age > 0.5f && doll.Speed < 0.5f || doll.Age > 4.5f)
                    {
                        g.State = Knock.Down; g.Timer = 0f;
                        p.MouthOpen = 0.3f; p.EyesClosed = true;
                    }
                    break;
                }
                case Knock.Down:
                    if (g.Doll != null)
                    {
                        if (g.Timer > 1.7f) GetUp(g);
                        break;
                    }
                    p.transform.localRotation = g.Rot;
                    p.transform.localPosition = g.Centre - g.Rot * new Vector3(0f, hipHeight * p.Scale, 0f);
                    if (g.Timer > 2.4f) { g.State = Knock.GettingUp; g.Timer = 0f; p.EyesClosed = false; p.MouthOpen = 0f; p.BrowRaise = -0.6f; p.Target = Pose.Stand; }
                    break;
                case Knock.GettingUp:
                {
                    // Back on their feet where they landed (down off the table if that is where they ended up).
                    float k = Mathf.SmoothStep(0f, 1f, g.Timer / 0.9f);
                    Vector3 feet = new Vector3(g.Centre.x, GroundAt(g.Centre), g.Centre.z);   // on a table or the bar, if that is where they landed
                    Vector3 home = g.Home - feet; home.y = 0f;
                    Quaternion upright = Quaternion.LookRotation(home.sqrMagnitude > 0.01f ? home.normalized : Vector3.forward, Vector3.up);
                    p.transform.localRotation = Quaternion.Slerp(g.Rot, upright, k);
                    p.transform.localPosition = Vector3.Lerp(g.Centre - g.Rot * new Vector3(0f, hipHeight * p.Scale, 0f), feet, k);
                    if (g.Timer >= 0.9f) { g.State = Knock.WalkingBack; g.Timer = 0f; g.Centre = feet; p.PoseSpeed = 9f; }
                    break;
                }
                case Knock.WalkingBack:
                {
                    Vector3 to = g.Home - g.Centre; to.y = 0f;
                    float d = to.magnitude;
                    if (d < 0.25f || g.Timer > 14f)
                    {
                        // Home: sit back down, pick the evening up where it left off.
                        p.transform.localPosition = g.Home; p.transform.localRotation = g.HomeRot;
                        p.Snap(g.Base); p.PoseSpeed = 7f; p.BrowRaise = 0f; p.Smile = 0.5f;
                        g.State = Knock.None;
                        break;
                    }
                    g.Centre += to / d * Mathf.Min(1.9f * dt, d);
                    float below = GroundAt(g.Centre);
                    if (below < g.Centre.y) g.Centre.y = Mathf.MoveTowards(g.Centre.y, below, 5f * dt);   // off the edge and down
                    g.WalkPhase += dt * 8f;
                    p.Target = VillageLife.Walk(g.WalkPhase);
                    p.transform.localPosition = g.Centre;
                    p.transform.localRotation = Quaternion.Slerp(p.transform.localRotation, Quaternion.LookRotation(to / d, Vector3.up), 1f - Mathf.Exp(-8f * dt));
                    break;
                }
            }
        }

        /// <summary>The physics lets go: the body lies as it fell, then gets up from there.</summary>
        void GetUp(Guest g)
        {
            const float hipHeight = 0.9f;
            var p = g.P;
            Vector3 hips = p.Hips.position, head = p.Head.position;
            Vector3 up = head - hips;
            up = up.sqrMagnitude > 1e-4f ? up.normalized : Vector3.up;
            Vector3 fwd = Vector3.ProjectOnPlane(p.Torso.forward, up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(Vector3.forward, up);
            g.Rot = Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(fwd.normalized, up);
            g.Centre = transform.InverseTransformPoint(hips);
            g.Doll.End();
            g.Doll = null;
            foreach (var smr in p.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = false;
            p.transform.localRotation = g.Rot;
            p.transform.localPosition = g.Centre - g.Rot * new Vector3(0f, hipHeight * p.Scale, 0f);
            var flat = Pose.Stand;
            flat.ArmOutL = 60f; flat.ArmOutR = 50f; flat.LegOut = 12f; flat.KneeL = 20f;
            p.Snap(flat);
            g.State = Knock.GettingUp; g.Timer = 0f;
            p.EyesClosed = false; p.MouthOpen = 0f; p.BrowRaise = -0.6f; p.Target = Pose.Stand;
        }

        void Thud(float strength)
        {
            if (strength > 0.15f) AudioDirector.Sfx("land", Mathf.Clamp01(strength) * 0.8f, 0.9f + strength * 0.3f);
        }

        // ---- the painting ----------------------------------------------------------------------

        /// <summary>
        /// Behind the bar hangs last New Year's midnight: Erik and Linn, mid-kiss, under the
        /// fireworks. It is painted from the real characters, posed in a little set and rendered once.
        /// </summary>
        void BuildPainting()
        {
            var rt = new RenderTexture(384, 480, 16) { name = "Painting", antiAliasing = 1 };
            rt.Create();
            const float w = 1.5f, h = 1.875f, y0 = 1.52f, zc = 3.3f, x = 7.9f;
            var fb = new MeshBuilder();
            fb.Col = Palette.Lamp;                                                   // gilt frame
            fb.Box(new Vector3(x, y0 + h * 0.5f, zc), new Vector3(0.08f, h + 0.22f, w + 0.22f));
            fb.Col = Palette.Mul(Palette.Lamp, 0.6f);
            fb.Box(new Vector3(x - 0.02f, y0 + h * 0.5f, zc), new Vector3(0.07f, h + 0.08f, w + 0.08f));
            fb.Col = Palette.TimberLight;
            fb.Box(new Vector3(x - 0.02f, y0 - 0.24f, zc), new Vector3(0.05f, 0.14f, 1.0f));   // brass plate
            fb.ToObject("Frame", Mats.Props, transform);
            var cb = new MeshBuilder();
            cb.Col = Color.white;
            float xf = x - 0.06f;
            cb.Quad(new Vector3(xf, y0, zc + w * 0.5f), new Vector3(xf, y0 + h, zc + w * 0.5f), new Vector3(xf, y0 + h, zc - w * 0.5f), new Vector3(xf, y0, zc - w * 0.5f));
            var canvasMat = Mats.Unlit(rt, new Color(0.95f, 0.92f, 0.86f));
            canvasMat.SetFloat("_Fog", 0f);
            cb.ToObject("Canvas", canvasMat, transform, false, false);
            var plate = VillageBuilder.Sign(transform, "MIDNIGHT, LAST NEW YEAR", Vector3.zero, Quaternion.identity, 0.07f, Palette.Ink);
            plate.transform.localPosition = new Vector3(x - 0.05f, y0 - 0.24f, zc);
            plate.transform.localRotation = Quaternion.Euler(0, 90, 0);
            _lights.Add(AddLight(transform, new Vector3(x - 1.1f, y0 + h + 0.3f, zc), Palette.Lamp, 4f, 5f));   // picture light

            // The set, out of sight below the floor.
            var set = new GameObject("Painting set").transform;
            set.SetParent(transform, false);
            set.localPosition = new Vector3(0f, -30f, 0f);
            var sky = new MeshBuilder();
            sky.Col = Palette.SkyTop;
            sky.Quad(new Vector3(-3f, -1f, 2.2f), new Vector3(-3f, 4.5f, 2.2f), new Vector3(3f, 4.5f, 2.2f), new Vector3(3f, -1f, 2.2f));
            sky.Col = Palette.Mix(Palette.SkyTop, Palette.SkyMid, 0.6f);
            sky.Quad(new Vector3(-3f, -1f, 2.15f), new Vector3(-3f, 1.2f, 2.15f), new Vector3(3f, 1.2f, 2.15f), new Vector3(3f, -1f, 2.15f));
            sky.Col = Palette.Snow;
            sky.Quad(new Vector3(-3f, -1f, 2.1f), new Vector3(-3f, 0.55f, 2.1f), new Vector3(3f, 0.75f, 2.1f), new Vector3(3f, -1f, 2.1f));
            sky.ToObject("Backdrop", Mats.Glow(Color.white, 0.85f), set, false, false);
            var fw = new MeshBuilder();
            var frng = new Rng(2025);
            Color[] cols = { Palette.Lamp, Palette.SignalRed, Palette.Jackets[3], Palette.PisteGreen };
            for (int burst = 0; burst < 4; burst++)
            {
                Vector3 c = new Vector3(frng.Range(-0.75f, 0.75f), frng.Range(2.05f, 2.5f), 2.0f);
                if (Mathf.Abs(c.x) < 0.28f) c.x += 0.45f;
                fw.Col = cols[burst];
                for (int i = 0; i < 14; i++)
                {
                    float a = i / 14f * Mathf.PI * 2f, r = frng.Range(0.16f, 0.3f);
                    Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    fw.Tube(c + d * 0.05f, c + d * r + Vector3.down * r * 0.25f, 0.008f, 0.003f, 3, false);
                    fw.Sphere(c + d * r + Vector3.down * r * 0.25f, 0.012f, 4, 3);
                }
            }
            fw.ToObject("Fireworks", _glowHot, set, false, false);

            PersonLook Dressed(CharacterId id)
            {
                var look = PersonLook.For(id, new Outfit());
                look.HasHelmet = false; look.HasGoggles = false; look.Gear = GearKind.None; look.Poles = false;
                return look;
            }
            var erik = Person.Create(Dressed(CharacterId.Erik), set, "Erik");
            var linn = Person.Create(Dressed(CharacterId.Linn), set, "Linn");
            erik.Alive = linn.Alive = false; erik.EyesClosed = linn.EyesClosed = true;
            erik.Smile = linn.Smile = 0.2f;
            erik.transform.localPosition = new Vector3(-0.225f, 0f, 0f); erik.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            linn.transform.localPosition = new Vector3(0.225f, 0f, 0f); linn.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            var pe = Pose.Stand; pe.TorsoPitch = 11f; pe.HeadPitch = 14f; pe.HeadRoll = 18f;
            pe.ArmFwdL = 46f; pe.ElbowL = 44f; pe.ArmTwistL = 30f; pe.ArmFwdR = 42f; pe.ElbowR = 50f; pe.ArmTwistR = 34f;      // hands at her waist
            var pl = Pose.Stand; pl.TorsoPitch = 8f; pl.HeadPitch = -16f; pl.HeadRoll = 18f;
            pl.ArmFwdL = 96f; pl.ElbowL = 46f; pl.ArmTwistL = 24f; pl.ArmFwdR = 100f; pl.ElbowR = 52f; pl.ArmTwistR = 28f;     // arms round his neck
            pl.ThighR = -14f; pl.KneeR = 62f;                                                                                // one foot kicked up
            erik.Snap(pe); linn.Snap(pl);
            AddLight(set, new Vector3(-1.2f, 2.4f, -1.6f), Palette.Lamp, 7f, 7f);
            AddLight(set, new Vector3(1.4f, 2.2f, 1.2f), Palette.Jackets[3], 6f, 5f);

            var camGo = new GameObject("Painter");
            camGo.transform.SetParent(set, false);
            camGo.transform.localPosition = new Vector3(0f, 1.42f, -2.5f);
            camGo.transform.localRotation = Quaternion.Euler(1.5f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 31f; cam.aspect = 384f / 480f; cam.nearClipPlane = 0.2f; cam.farClipPlane = 12f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Palette.SkyTop;
            cam.targetTexture = rt; cam.depth = -10;
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            StartCoroutine(FinishPainting(set.gameObject, cam));
        }

        IEnumerator FinishPainting(GameObject set, Camera cam)
        {
            // A few frames for the picture to be taken, then the set is struck.
            for (int i = 0; i < 5; i++) yield return null;
            cam.targetTexture = null;
            Destroy(set);
        }

        // ---- frame -----------------------------------------------------------------------

        void PlaceCamera(float dt)
        {
            float sway = Mathf.Sin(_time * 1.3f) * Wobble;
            Cam.transform.localPosition = _pos + new Vector3(0f, Eye - _knockDrop - (_seatTable >= 0 ? 0.45f : 0f) + Mathf.Sin(_bob) * 0.035f, 0f);
            Cam.transform.localRotation = Quaternion.Euler(-_pitch + Mathf.Cos(_time * 0.9f) * Wobble * 0.6f, _yaw + sway, sway * 1.4f + _knockRoll);
        }

        void AnimateFire(float dt)
        {
            for (int i = 0; i < _flames.Count; i++)
            {
                var f = _flames[i];
                float t = _time * f.Speed + f.Phase;
                float h = 0.7f + Mathf.PerlinNoise(t * 0.55f, f.Phase) * 0.75f;
                float wdt = 1.12f - h * 0.18f + Mathf.Sin(t * 1.7f) * 0.06f;
                f.T.localScale = new Vector3(wdt, h, wdt);
                f.T.localRotation = Quaternion.Euler((Mathf.PerlinNoise(f.Phase, t * 0.4f) - 0.5f) * f.Lean * 0.6f, t * 20f, (Mathf.PerlinNoise(t * 0.45f, 7f + f.Phase) - 0.5f) * f.Lean * 2f);
            }
            for (int i = 0; i < _embers.Count; i++)
            {
                float life = Mathf.Repeat(_time * (0.35f + i * 0.03f) + i * 0.37f, 1f);
                float x = Mathf.Sin(i * 12.9f) * 0.45f + Mathf.Sin(_time * 2.1f + i) * 0.08f * life;
                _embers[i].localPosition = new Vector3(x, 0.3f + life * 1.25f, Mathf.Cos(i * 7.3f) * 0.1f);
                _embers[i].localScale = Vector3.one * (1f - life) * 1.4f;
            }
            float flick = Mathf.PerlinNoise(_time * 7f, 0f) * 0.6f + Mathf.PerlinNoise(_time * 19f, 3f) * 0.4f;
            _fireLight.intensity = 7.5f + flick * 6.5f;
            _fireLight.range = 12f + flick * 2.5f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _time += dt;
            _cheer = Mathf.Max(0f, _cheer - dt);
            // The room bounces to the beat.
            float beat = Mathf.Abs(Mathf.Sin(_time * Mathf.PI * Bpm / 60f));
            for (int i = 0; i < _lights.Count; i++) _lights[i].intensity = (i < 5 ? 11f : 4.5f) + beat * (i < 5 ? 5f : 0.6f) * (i % 2 == 0 ? 1f : 0.5f);
            AnimateFire(dt);
            AnimateCrowd(dt);
            AnimateDj(dt);
            SyncLive(dt);
            if (WorldLook.Instance != null) WorldLook.Instance.Indoors = true;

            // The fist: out, and back.
            if (_punch < 1f)
            {
                _punch = Mathf.Min(1f, _punch + dt / (_blow == Blow.Haymaker ? 0.42f : 0.3f));
                float jab = Mathf.Sin(Mathf.PI * Mathf.Pow(_punch, 0.6f));
                _fist.gameObject.SetActive(true);
                Vector3 from = new Vector3(0.24f, -0.34f, 0.3f), to = new Vector3(0.06f, -0.1f, 0.78f);
                float roll = 0f;
                switch (_blow)
                {
                    case Blow.Hook: from = new Vector3(0.55f, -0.22f, 0.22f); to = new Vector3(-0.02f, -0.06f, 0.7f); roll = -70f; break;
                    case Blow.Uppercut: from = new Vector3(0.16f, -0.62f, 0.32f); to = new Vector3(0.04f, 0.06f, 0.62f); break;
                    case Blow.Gut: from = new Vector3(0.26f, -0.42f, 0.28f); to = new Vector3(0.05f, -0.46f, 0.82f); break;
                    case Blow.Haymaker: from = new Vector3(0.75f, -0.05f, -0.05f); to = new Vector3(-0.04f, -0.08f, 0.86f); roll = -55f; break;
                }
                // A hook and a haymaker come round in an arc, not straight.
                Vector3 arc = (_blow == Blow.Hook || _blow == Blow.Haymaker) ? new Vector3(0.12f, 0f, 0.25f) * Mathf.Sin(Mathf.PI * jab) : Vector3.zero;
                _fist.localPosition = Vector3.Lerp(from, to, jab) + arc;
                _fist.localRotation = Quaternion.Euler(Mathf.Lerp(_blow == Blow.Uppercut ? -60f : -18f, 0f, jab), Mathf.Lerp(-22f, -4f, jab), roll * jab);
            }
            else if (_fist.gameObject.activeSelf) _fist.gameObject.SetActive(false);
            if (_powLeft > 0f) { _powLeft -= dt; if (_powLeft <= 0f) _pow.AddToClassList("toast-hidden"); }

            if (Game != null) { Game.Tick(dt); PlaceCameraForGame(dt); return; }
            if (_picker != null)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ClosePicker();
                PlaceCamera(dt);
                return;
            }
            if (AnimateMyKnock(dt)) return;
            if (Dialogs.Open) return;
            // On a phone or tablet the fingers (TouchControls) do what the keys and the mouse do.
            var fingers = TouchControls.OnFoot ? TouchControls.I : null;
            bool punch = (!Device.Touch && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) || (fingers != null && fingers.TakePunch());
            if (punch && _punch >= 1f && _time > 0.6f) Punch();

            // Walk.
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (mouse != null && !Device.Touch)
            {
                Vector2 d = mouse.delta.ReadValue() * SaveData.MouseSensitivity;
                _yaw += d.x; _pitch = Mathf.Clamp(_pitch + (SaveData.InvertLook ? -d.y : d.y), -70f, 70f);
            }
            if (fingers != null)
            {
                Vector2 d = fingers.TakeLook() * 0.16f * (SaveData.MouseSensitivity / 0.12f);
                _yaw += d.x; _pitch = Mathf.Clamp(_pitch + (SaveData.InvertLook ? d.y : -d.y), -70f, 70f);
            }
            Vector3 move = Vector3.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) move.z += 1f;
                if (kb.sKey.isPressed) move.z -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
            }
            // A thumb walks as far as it is pushed (a small push: a slow step); keys walk at full pace.
            if (fingers != null && fingers.Move.sqrMagnitude > 0.04f) { move.x += fingers.Move.x; move.z += fingers.Move.y; }
            if (move.sqrMagnitude > 0f && _seatTable >= 0) { StandUp(); move = Vector3.zero; }
            if (move.sqrMagnitude > 0f)
            {
                Vector3 dir = fingers != null ? Vector3.ClampMagnitude(move, 1f) : move.normalized;
                move = Quaternion.Euler(0f, _yaw, 0f) * dir * 2.6f * dt;
                TryMove(move);
                if (_grounded) _bob += dt * 9f;
                _lastMove = Time.time;
            }
            if (_seatTable < 0) Climb((kb != null && kb.spaceKey.wasPressedThisFrame) || (fingers != null && fingers.TakeJump()), dt);
            PlaceCamera(dt);

            // What can we do here?
            bool atBar = (_pos - BarSpot).magnitude < 1.6f;
            bool atDoor = _pos.x < -6.2f && Mathf.Abs(_pos.z) < 1.4f && _time > 1.5f;
            bool atDj = new Vector2(_pos.x - DjDesk.x, _pos.z - DjDesk.z).magnitude < 2.1f && Songs.All.Count > 0;
            bool atWalkman = _walkman != null && new Vector2(_pos.x - WalkmanSpot.x, _pos.z - WalkmanSpot.z).magnitude < 1.4f;
            if (_walkman != null) _walkman.localRotation = Quaternion.Euler(0f, Time.time * 35f, 0f);
            bool atToilet = new Vector2(_pos.x - ToiletSpot.x, _pos.z - ToiletSpot.z).magnitude < 1.1f;
            Vector3 seatPos = default; float seatSide = 0f; int seat = -1;
            if (_seatTable < 0 && !atBar && !atDj && !atWalkman && !atDoor && !atToilet && _grounded && _pos.y < 0.05f) seat = FreeSeat(out seatPos, out seatSide);
            string action = _seatTable >= 0 ? "STAND UP" : atBar ? "BEER CHALLENGE" : atDj ? "REQUEST A SONG" : atWalkman ? "TAKE THE WALKMAN" : atDoor ? "OUTSIDE" : atToilet ? "FLUSH" : seat >= 0 ? "SIT DOWN" : "";
            CanUse = action.Length > 0;
            // The key to press goes in front, on a keyboard; on a touch screen the USE button shows up instead.
            if (_prompt != null) _prompt.text = !CanUse ? "" : Device.Touch ? action : "[E]  " + action;
            if ((kb != null && kb.eKey.wasPressedThisFrame) || (fingers != null && fingers.TakeUse()))
            {
                if (_seatTable >= 0) StandUp();
                else if (seat >= 0) { _seatTable = seat; _seatPos = seatPos; _seatSide = seatSide; _pos = new Vector3(seatPos.x, 0f, seatPos.z); _vy = 0f; _grounded = true; _yaw = seatSide > 0f ? 180f : 0f; AudioDirector.Sfx("land", 0.3f, 1.3f); }
                else if (atDj) OpenPicker();
                else if (atWalkman) TakeWalkman();
                else if (atToilet) { AudioDirector.Sfx("flush", 0.8f); SaveData.Record(_who, pr => pr.flushes++); }
                else if (atBar) StartGame();
                else if (atDoor) { AudioDirector.Sfx("door", 0.8f); OnLeave?.Invoke(); }
            }
        }

        void TryMove(Vector3 delta)
        {
            const float r = 0.32f;
            for (int axis = 0; axis < 2; axis++)
            {
                Vector3 p = _pos;
                if (axis == 0) p.x += delta.x; else p.z += delta.z;
                p.x = Mathf.Clamp(p.x, -HalfX + r, HalfX - r);
                p.z = Mathf.Clamp(p.z, -HalfZ + r, HalfZ - r);
                bool hit = false;
                for (int i = 0; i < _blocks.Count && !hit; i++)
                {
                    // Low enough to step onto (or already above it), or high enough to pass under.
                    if (_tops[i] <= _pos.y + StepUp || _bottoms[i] >= _pos.y + 1.75f) continue;
                    var b = _blocks[i];
                    hit = p.x > b.xMin - r && p.x < b.xMax + r && p.z > b.yMin - r && p.z < b.yMax + r;
                }
                if (!hit) _pos = p;
            }
        }

        /// <summary>The highest thing under your feet that you can stand on from where you are.</summary>
        float GroundAt(Vector3 p)
        {
            const float foot = 0.16f;
            float g = 0f;
            for (int i = 0; i < _blocks.Count; i++)
            {
                if (_tops[i] > p.y + StepUp || _tops[i] >= Ceiling - 0.5f) continue;
                var b = _blocks[i];
                if (p.x > b.xMin - foot && p.x < b.xMax + foot && p.z > b.yMin - foot && p.z < b.yMax + foot) g = Mathf.Max(g, _tops[i]);
            }
            return g;
        }

        /// <summary>Jumping, falling, stepping up: onto the benches, the tables, the bar if you dare.</summary>
        void Climb(bool jump, float dt)
        {
            float ground = GroundAt(_pos);
            if (jump && _grounded) { _vy = JumpSpeed; _grounded = false; AudioDirector.Sfx("jump", 0.3f, 1.3f); }
            if (!_grounded || _pos.y > ground + 0.02f)
            {
                _grounded = false;
                _vy -= Gravity * dt;
                _pos.y += _vy * dt;
                ground = GroundAt(_pos);
                if (_pos.y <= ground && _vy <= 0f)
                {
                    if (_vy < -2.5f) AudioDirector.Sfx("land", Mathf.Clamp01(-_vy / 7f) * 0.6f, 1.2f);
                    _pos.y = ground; _vy = 0f; _grounded = true;
                }
                else if (_pos.y < ground) _pos.y = ground;   // stepped up onto it on the way up
            }
            else _pos.y = ground;
        }

        /// <summary>Something solid in the room: walked around (or climbed onto, if low enough), and hit by anyone flying.</summary>
        void Block(Rect r, float top, float bottom = 0f)
        {
            _blocks.Add(r); _tops.Add(top); _bottoms.Add(bottom);
            Solid(new Vector3(r.center.x, (top + bottom) * 0.5f, r.center.y), new Vector3(r.width, top - bottom, r.height));
        }

        /// <summary>A collider only (for bodies sent flying).</summary>
        void Solid(Vector3 centre, Vector3 size)
        {
            if (_solids == null) { _solids = new GameObject("Solids").transform; _solids.SetParent(transform, false); }
            var go = new GameObject("Solid");
            go.transform.SetParent(_solids, false);
            go.transform.localPosition = centre;
            go.AddComponent<BoxCollider>().size = size;
        }

        // ---- the beer game ---------------------------------------------------------------

        public void StartGame()
        {
            if (Game != null) return;
            _prompt.text = "";
            Game = new BeerGame(this, _bartender, () => { Game = null; GameEndFrame = Time.frameCount; });
        }

        void PlaceCameraForGame(float dt)
        {
            // Stand at the bar, facing the bartender; tip the head back as the glass empties.
            _pos = Vector3.Lerp(_pos, BarSpot, 1f - Mathf.Exp(-5f * dt));
            _yaw = Mathf.LerpAngle(_yaw, 90f, 1f - Mathf.Exp(-5f * dt));
            _pitch = Mathf.Lerp(_pitch, Game.Tilt * 34f - 4f, 1f - Mathf.Exp(-7f * dt));
            PlaceCamera(dt);
        }

        /// <summary>The nearest empty place on a bench within reach, or -1.</summary>
        int FreeSeat(out Vector3 at, out float side)
        {
            at = default; side = 0f;
            int best = -1; float bestD = 1.1f;
            for (int t = 0; t < Tables.Length; t++)
                for (int sd = -1; sd <= 1; sd += 2)
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 p = Tables[t] + new Vector3((i - 1.5f) * 1.02f, 0f, sd * 0.74f);
                        float d = new Vector2(p.x - _pos.x, p.z - _pos.z).magnitude;
                        if (d > bestD) continue;
                        bool taken = false;
                        foreach (var g in _guests) if (g.Table >= 0 && (new Vector2(g.Home.x - p.x, g.Home.z - p.z)).sqrMagnitude < 0.45f * 0.45f) { taken = true; break; }
                        if (taken) continue;
                        best = t; bestD = d; at = p; side = sd;
                    }
            return best;
        }

        void StandUp()
        {
            if (_seatTable < 0) return;
            _pos = new Vector3(_seatPos.x, 0f, _seatPos.z + _seatSide * 0.75f);
            _seatTable = -1;
        }

        void TakeWalkman()
        {
            SaveData.HasWalkman = true;
            SaveData.WalkmanOn = true;
            if (AudioDirector.I != null && AudioDirector.I.CurrentSong != null) SaveData.WalkmanSong = AudioDirector.I.CurrentSong;
            SaveData.Save();
            Destroy(_walkman.gameObject);
            _walkman = null;
            AudioDirector.Sfx("click", 0.8f);
            Ui.Instance?.Notice("WALKMAN!", "M  next song     N  on / off", 4f);
        }

        public void Close()
        {
            LiveRooms.Punched -= OnPunched;
            ClosePicker();
            if (AudioDirector.I != null) { AudioDirector.I.SongEnded -= OnSongEnded; AudioDirector.I.DjHasFloor = false; }
            Game?.Dispose();
            _ui?.RemoveFromHierarchy();
            if (WorldLook.Instance != null) WorldLook.Instance.Indoors = false;
            if (AudioDirector.I != null) { AudioDirector.I.Ambience(null); AudioDirector.I.Music(null); }
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            Destroy(gameObject);
        }
    }
}
