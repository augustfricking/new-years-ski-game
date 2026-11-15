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
    /// beer-chugging game, the door leads back to the lift.
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

        enum Act { SitTalk, SitDrink, SitSway, Dance, BarDrink, Stand }
        sealed class Guest { public Person P; public Act Kind; public int Table; public float Phase; public Pose Base; }
        struct Flame { public Transform T; public float Phase, Speed, Lean; }

        readonly List<Rect> _blocks = new List<Rect>();
        readonly List<Guest> _guests = new List<Guest>();
        readonly List<Light> _lights = new List<Light>();
        readonly List<Flame> _flames = new List<Flame>();
        readonly List<Transform> _embers = new List<Transform>();
        Person _bartender;
        Light _fireLight;
        Vector3 _pos = new Vector3(-6.6f, 0f, 0f);
        float _yaw = 90f, _pitch = 4f, _time, _bob, _cheer;
        VisualElement _ui;
        Label _prompt;
        CharacterId _who;
        Material _glow, _glowHot, _flameMat, _beerMat;
        MeshBuilder _beer;
        public float Wobble; // grows with every beer

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
            _blocks.Add(new Rect(-7.8f, 7.6f, 3f, 3.2f));
            BuildDecor(b, bulbs, rng);

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

            if (AudioDirector.I != null)
            {
                AudioDirector.I.Silence();
                AudioDirector.I.Music("music_tavern", 1.2f);
                AudioDirector.I.Ambience("crowd", "fire");
                AudioDirector.I.Play("door", 0.8f);
            }
            var ui = Ui.Instance;
            if (ui != null)
            {
                _ui = Ui.Div(ui.ScreenLayer, "layer");
                var box = Ui.Div(_ui, "prompt");
                _prompt = Ui.Text(box, "", "h2 outlined");
                var keys = Ui.Div(_ui, "hud-corner hud-bl");
                Ui.Keys(keys, "W A S D", "walk");
                Ui.Keys(keys, "MOUSE", "look");
                Ui.Keys(keys, "E", "use");
            }
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
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
                foreach (float x in new[] { -7.7f, 3.2f }) b.Box(new Vector3(x, Ceiling * 0.5f, z), new Vector3(0.3f, Ceiling, 0.3f));

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
            _blocks.Add(new Rect(4.45f, -6.7f, 3.6f, 13.4f));
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
                _blocks.Add(new Rect(3.8f, z - 0.25f, 0.5f, 0.5f));
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
            _blocks.Add(new Rect(-4.2f, -HalfZ, 3.4f, 1.9f));

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
            _blocks.Add(new Rect(c.x - half - 0.05f, c.z - 0.92f, TableLen + 0.1f, 1.84f));
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

        static Light AddLight(Transform parent, Vector3 localPos, Color colour, float range, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = colour; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>A beer stein: glass body, beer inside, foam on top and a handle.</summary>
        public static void Stein(MeshBuilder glow, MeshBuilder solid, Vector3 at, float fill, float k = 0.72f)
        {
            glow.Col = Palette.Mix(Palette.Lamp, Palette.SafetyOrange, 0.35f);
            glow.Cylinder(at, 0.062f * k, 0.066f * k, 0.2f * k * Mathf.Clamp01(fill), 10);
            if (fill > 0.05f) { solid.Col = Palette.White; solid.Cylinder(at + Vector3.up * 0.2f * k * fill, 0.068f * k, 0.06f * k, 0.035f * k, 10); }
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
                look.Hero = true;   // close-up people: animated faces and inked shading
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
            // On the stage and on the floor in front of it.
            Add(new Vector3(-6.6f, 0.3f, 9.3f), 140f, Act.Dance);
            Add(new Vector3(-5.6f, 0.3f, 8.7f), 160f, Act.Dance);
            Add(new Vector3(-4.2f, 0f, 9.4f), 200f, Act.Dance);
            // Standing about with a glass.
            foreach (float z in new[] { -4.4f, -2.9f, 2.7f, 4.6f })
            {
                var g = Add(new Vector3(3.75f, 0, z + rng.Range(-0.2f, 0.2f)), 90f + rng.Range(-25f, 25f), Act.BarDrink);
                _blocks.Add(new Rect(g.P.transform.localPosition.x - 0.3f, g.P.transform.localPosition.z - 0.3f, 0.6f, 0.6f));
            }
            Add(new Vector3(-0.2f, 0f, -9.3f), 250f, Act.Stand);   // warming up by the fire
            _blocks.Add(new Rect(-0.5f, -9.6f, 0.6f, 0.6f));

            var barLook = Partygoer();
            barLook.Jacket = Palette.White; barLook.Trim = Palette.SignalRed; barLook.Beanie = false; barLook.HasGoggles = false; barLook.Hood = false;
            _bartender = Person.Create(barLook, transform, "Bartender");
            _bartender.transform.localPosition = new Vector3(6.2f, 0, 0f);
            _bartender.transform.localRotation = Quaternion.Euler(0, -90, 0);
            _bartender.Snap(Pose.Stand.WithArms(40, 10, 70, 40, 10, 70));
            _bartender.Smile = 0.6f;
        }

        void AnimateCrowd(float dt)
        {
            float beatPhase = _time * Mathf.PI * Bpm / 60f;
            bool cheering = _cheer > 0f;
            foreach (var g in _guests)
            {
                var p = g.P;
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
                var look = PersonLook.For(id);
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
            Cam.transform.localPosition = _pos + new Vector3(0f, Eye + Mathf.Sin(_bob) * 0.035f, 0f);
            Cam.transform.localRotation = Quaternion.Euler(-_pitch + Mathf.Cos(_time * 0.9f) * Wobble * 0.6f, _yaw + sway, sway * 1.4f);
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
            if (WorldLook.Instance != null) WorldLook.Instance.Indoors = true;

            if (Game != null) { Game.Tick(dt); PlaceCameraForGame(dt); return; }
            if (Dialogs.Open) return;

            // Walk.
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 d = mouse.delta.ReadValue() * SaveData.MouseSensitivity;
                _yaw += d.x; _pitch = Mathf.Clamp(_pitch + (SaveData.InvertLook ? -d.y : d.y), -70f, 70f);
            }
            Vector3 move = Vector3.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) move.z += 1f;
                if (kb.sKey.isPressed) move.z -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
            }
            if (move.sqrMagnitude > 0f)
            {
                move = Quaternion.Euler(0f, _yaw, 0f) * move.normalized * 2.6f * dt;
                TryMove(move);
                _bob += dt * 9f;
            }
            PlaceCamera(dt);

            // What can we do here?
            bool atBar = (_pos - BarSpot).magnitude < 1.6f;
            bool atDoor = _pos.x < -6.2f && Mathf.Abs(_pos.z) < 1.4f && _time > 1.5f;
            if (_prompt != null) _prompt.text = atBar ? "[E]  BEER CHALLENGE" : atDoor ? "[E]  BACK TO THE LIFT" : "";
            if (kb != null && kb.eKey.wasPressedThisFrame)
            {
                if (atBar) StartGame();
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
                    var b = _blocks[i];
                    hit = p.x > b.xMin - r && p.x < b.xMax + r && p.z > b.yMin - r && p.z < b.yMax + r;
                }
                if (!hit) _pos = p;
            }
        }

        // ---- the beer game ---------------------------------------------------------------

        public void StartGame()
        {
            if (Game != null) return;
            _prompt.text = "";
            Game = new BeerGame(this, _bartender, () => { Game = null; });
        }

        void PlaceCameraForGame(float dt)
        {
            // Stand at the bar, facing the bartender; tip the head back as the glass empties.
            _pos = Vector3.Lerp(_pos, BarSpot, 1f - Mathf.Exp(-5f * dt));
            _yaw = Mathf.LerpAngle(_yaw, 90f, 1f - Mathf.Exp(-5f * dt));
            _pitch = Mathf.Lerp(_pitch, Game.Tilt * 34f - 4f, 1f - Mathf.Exp(-7f * dt));
            PlaceCamera(dt);
        }

        public void Close()
        {
            Game?.Dispose();
            _ui?.RemoveFromHierarchy();
            if (WorldLook.Instance != null) WorldLook.Instance.Indoors = false;
            if (AudioDirector.I != null) { AudioDirector.I.Ambience(null); AudioDirector.I.Music(null); }
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            Destroy(gameObject);
        }
    }
}
