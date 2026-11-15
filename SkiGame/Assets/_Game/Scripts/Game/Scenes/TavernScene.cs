using System;
using System.Collections.Generic;
using Ski.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// The après-ski: a packed timber tavern on New Year's Eve. Walk around in first person; the
    /// bar offers the beer-chugging minigame, the door leads back to the lift.
    /// </summary>
    public sealed class TavernScene : MonoBehaviour
    {
        public Action OnLeave;
        public Camera Cam { get; private set; }
        public BeerGame Game { get; private set; }

        // Room: x from -8 (door wall) to 8 (bar wall), z from -11 to 11.
        const float HalfX = 8f, HalfZ = 11f, Ceiling = 4.4f, Eye = 1.66f;
        static readonly Vector3 BarSpot = new Vector3(3.9f, 0f, 0f);

        readonly List<Rect> _blocks = new List<Rect>();
        readonly List<Person> _crowd = new List<Person>();
        readonly List<int> _crowdKind = new List<int>();
        readonly List<Light> _lights = new List<Light>();
        Person _bartender;
        Transform _fire;
        Light _fireLight;
        Vector3 _pos = new Vector3(-6.6f, 0f, 0f);
        float _yaw = 90f, _pitch = 4f, _time, _bob;
        VisualElement _ui;
        Label _prompt;
        CharacterId _who;
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

        void Build()
        {
            if (WorldLook.Instance != null) { WorldLook.Instance.Indoors = true; WorldLook.Instance.Storm = 0f; }
            var b = new MeshBuilder();
            var glow = new MeshBuilder();
            var bulbs = new MeshBuilder();

            // --- shell: floorboards, log walls, beams -------------------------------------------
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

            // --- the bar along the far wall ---------------------------------------------------
            b.Col = Palette.TimberDark;
            b.Box(new Vector3(5.2f, 0.55f, 0f), new Vector3(0.9f, 1.1f, 13f));
            b.Col = Palette.TimberLight;
            b.Box(new Vector3(5.1f, 1.13f, 0f), new Vector3(1.3f, 0.08f, 13.4f));
            _blocks.Add(new Rect(4.45f, -6.7f, 3.6f, 13.4f));
            // Shelves with bottles.
            var rng = new Rng(1231);
            for (int shelf = 0; shelf < 3; shelf++)
            {
                b.Col = Palette.Timber;
                b.Box(new Vector3(7.75f, 1.5f + shelf * 0.62f, 0f), new Vector3(0.4f, 0.06f, 12f));
                for (float z = -5.6f; z < 5.7f; z += 0.34f)
                {
                    if (rng.Chance(0.2f)) continue;
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
                Stool(b, new Vector3(4.05f, 0f, z));
                _blocks.Add(new Rect(3.8f, z - 0.25f, 0.5f, 0.5f));
            }

            // --- fireplace on the south wall ---------------------------------------------------
            b.Col = Palette.Rock;
            b.Box(new Vector3(-2.5f, 1.2f, -HalfZ + 0.45f), new Vector3(3.2f, 2.4f, 0.9f));
            b.TaperedBox(new Vector3(-2.5f, 2.4f, -HalfZ + 0.45f), new Vector3(3.2f, 0, 0.9f), new Vector2(0.45f, 0.9f), Ceiling - 2.4f);
            b.Col = Palette.Ink;
            b.Box(new Vector3(-2.5f, 0.7f, -HalfZ + 0.88f), new Vector3(1.7f, 1.2f, 0.1f));
            b.Col = Palette.TimberDark;
            b.Box(new Vector3(-2.5f, 2.45f, -HalfZ + 0.95f), new Vector3(3.5f, 0.12f, 0.35f));
            _blocks.Add(new Rect(-4.2f, -HalfZ, 3.4f, 1.1f));
            _fire = new GameObject("Fire").transform;
            _fire.SetParent(transform, false);
            _fire.localPosition = new Vector3(-2.5f, 0.15f, -HalfZ + 0.95f);
            var fb = new MeshBuilder();
            fb.Col = Palette.SafetyOrange; fb.Cylinder(new Vector3(-0.3f, 0, 0), 0.22f, 0f, 0.7f, 5);
            fb.Col = Palette.Lamp; fb.Cylinder(new Vector3(0.05f, 0, 0.02f), 0.26f, 0f, 0.95f, 5);
            fb.Col = Palette.SignalRed; fb.Cylinder(new Vector3(0.38f, 0, 0), 0.2f, 0f, 0.6f, 5);
            fb.ToObject("Flames", Mats.Glow(Color.white, 2.2f), _fire, false, false);
            _fireLight = AddLight(new Vector3(-2.5f, 1.0f, -HalfZ + 2f), Palette.SafetyOrange, 13f, 9f);

            // --- tables, benches, a little stage -------------------------------------------------
            Vector3[] tables = { new Vector3(-4.5f, 0, 6.2f), new Vector3(-1.2f, 0, 7.6f), new Vector3(-4.6f, 0, -5.2f), new Vector3(0.6f, 0, -6.6f), new Vector3(-0.8f, 0, 2.6f) };
            foreach (var t in tables)
            {
                b.Col = Palette.TimberLight; b.Cylinder(t + Vector3.up * 0.98f, 0.85f, 0.85f, 0.08f, 12);
                b.Col = Palette.TimberDark; b.Cylinder(t, 0.12f, 0.1f, 0.98f, 6); b.Cylinder(t, 0.45f, 0.4f, 0.06f, 8);
                _blocks.Add(new Rect(t.x - 0.75f, t.z - 0.75f, 1.5f, 1.5f));
                // Glasses on the table.
                for (int g = 0; g < 3; g++)
                {
                    float a = g * 2.1f + t.x;
                    Stein(glow, b, t + new Vector3(Mathf.Cos(a) * 0.45f, 1.06f, Mathf.Sin(a) * 0.45f), 0.5f + 0.2f * g);
                }
            }
            b.Col = Palette.Roof;
            b.Box(new Vector3(-6.3f, 0.15f, 9.2f), new Vector3(3f, 0.3f, 3.2f)); // stage
            _blocks.Add(new Rect(-7.8f, 7.6f, 3f, 3.2f));

            // --- New Year: banner, balloons, strings of lights ---------------------------------
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

            b.ToObject("Tavern", Mats.Props, transform);
            glow.ToObject("Tavern glow", Mats.Glow(Color.white, 1.5f), transform, false, false);
            bulbs.ToObject("Tavern bulbs", Mats.Glow(Color.white, 2.3f), transform, false, false);
            var sign = VillageBuilder.Sign(transform, "HAPPY NEW YEAR!", Vector3.zero, Quaternion.identity, 0.5f, Palette.White);
            sign.transform.localPosition = new Vector3(7.86f, 3.75f, 0f);
            sign.transform.localRotation = Quaternion.Euler(0, 90, 0);

            // Warm lamps down the room, pulsing gently with the music.
            foreach (var p in new[] { new Vector3(-3f, 3.4f, 6f), new Vector3(-3f, 3.4f, -4f), new Vector3(2.5f, 3.4f, 5f), new Vector3(2.5f, 3.4f, -5f), new Vector3(5.6f, 2.6f, 0f) })
                _lights.Add(AddLight(p, Palette.Lamp, 12f, 14f));

            BuildCrowd(rng);

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
                AudioDirector.I.Music("music_tavern");
                AudioDirector.I.Ambience("crowd");
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

        Light AddLight(Vector3 localPos, Color colour, float range, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = colour; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
            return l;
        }

        static void Stool(MeshBuilder b, Vector3 at)
        {
            b.Col = Palette.TimberDark; b.Cylinder(at, 0.05f, 0.05f, 0.72f, 5); b.Cylinder(at, 0.22f, 0.2f, 0.04f, 8);
            b.Col = Palette.SignalRed; b.Cylinder(at + Vector3.up * 0.72f, 0.2f, 0.2f, 0.08f, 10);
        }

        /// <summary>A beer stein: glass body, beer inside, foam on top and a handle.</summary>
        public static void Stein(MeshBuilder glow, MeshBuilder solid, Vector3 at, float fill)
        {
            glow.Col = Palette.Lamp;
            glow.Cylinder(at, 0.062f, 0.066f, 0.2f * Mathf.Clamp01(fill), 10);
            if (fill > 0.05f) { solid.Col = Palette.White; solid.Cylinder(at + Vector3.up * 0.2f * fill, 0.068f, 0.06f, 0.035f, 10); }
            solid.Col = Palette.Mix(Palette.Snow, Palette.SnowShadow, 0.35f);
            solid.Cylinder(at + Vector3.down * 0.01f, 0.072f, 0.072f, 0.012f, 10);
            solid.Tube(at + new Vector3(0.07f, 0.04f, 0), at + new Vector3(0.12f, 0.1f, 0), 0.012f, 0.012f, 4, false);
            solid.Tube(at + new Vector3(0.12f, 0.1f, 0), at + new Vector3(0.07f, 0.16f, 0), 0.012f, 0.012f, 4, false);
        }

        void BuildCrowd(Rng rng)
        {
            PersonLook Partygoer(bool tall = false)
            {
                var look = PersonLook.Random(rng, false);
                look.Gear = GearKind.None; look.Poles = false; look.HasHelmet = false; look.GogglesDown = false;
                look.Beanie = rng.Chance(0.55f); look.Helmet = Palette.Jackets[rng.Range(0, Palette.Jackets.Length)];
                look.Hair = rng.Chance(0.5f) ? Palette.Bark : rng.Chance(0.5f) ? Palette.Hex("#E0B050") : Palette.Ink;
                look.Boots = Palette.Ink;
                return look;
            }
            void Add(Vector3 at, float yaw, int kind)
            {
                var p = Person.Create(Partygoer(), transform, "Guest");
                p.transform.localPosition = at;
                p.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                p.Snap(kind == 1 ? Pose.Drink : kind == 2 ? Pose.Cheer : Pose.Stand);
                p.SetGogglesUp(true, true);
                p.Talking = kind == 0;
                _crowd.Add(p); _crowdKind.Add(kind);
                _blocks.Add(new Rect(at.x - 0.3f, at.z - 0.3f, 0.6f, 0.6f));
            }
            // Groups chatting around the tables.
            Vector3[] centres = { new Vector3(-4.5f, 0, 6.2f), new Vector3(-1.2f, 0, 7.6f), new Vector3(-4.6f, 0, -5.2f), new Vector3(0.6f, 0, -6.6f), new Vector3(-0.8f, 0, 2.6f) };
            foreach (var c in centres)
            {
                int n = rng.Range(3, 5);
                float a0 = rng.Range(0f, 6f);
                for (int i = 0; i < n; i++)
                {
                    float a = a0 + i * Mathf.PI * 2f / n;
                    Vector3 at = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.35f;
                    if (Mathf.Abs(at.x) > HalfX - 0.5f || Mathf.Abs(at.z) > HalfZ - 0.5f) continue;
                    Add(at, Mathf.Atan2(c.x - at.x, c.z - at.z) * Mathf.Rad2Deg, rng.Range(0, 3) == 0 ? 1 : 0);
                }
            }
            // Dancers on the stage and in front of it, in ski boots, obviously.
            for (int i = 0; i < 6; i++)
            {
                bool onStage = i < 3;
                Vector3 at = onStage ? new Vector3(-7f + i * 0.8f, 0.3f, 9.2f + (i % 2) * 0.6f) : new Vector3(-5.6f + (i - 3) * 1.1f, 0f, 6.9f - (i % 2) * 0.5f);
                Add(at, onStage ? 140f : -30f + i * 20f, 2);
            }
            // At the bar.
            foreach (float z in new[] { -4.4f, -2.9f, 2.7f, 4.6f })
                Add(new Vector3(3.75f, 0, z + rng.Range(-0.2f, 0.2f)), 90f + rng.Range(-25f, 25f), 1);

            var barLook = Partygoer();
            barLook.Jacket = Palette.White; barLook.Trim = Palette.SignalRed; barLook.Beanie = false; barLook.Hair = Palette.Ink; barLook.Hero = true;
            _bartender = Person.Create(barLook, transform, "Bartender");
            _bartender.transform.localPosition = new Vector3(6.2f, 0, 0f);
            _bartender.transform.localRotation = Quaternion.Euler(0, -90, 0);
            _bartender.Snap(Pose.Stand.WithArms(40, 10, 70, 40, 10, 70));
        }

        // ---- frame -----------------------------------------------------------------------

        void PlaceCamera(float dt)
        {
            float sway = Mathf.Sin(_time * 1.3f) * Wobble;
            Cam.transform.localPosition = _pos + new Vector3(0f, Eye + Mathf.Sin(_bob) * 0.035f, 0f);
            Cam.transform.localRotation = Quaternion.Euler(-_pitch + Mathf.Cos(_time * 0.9f) * Wobble * 0.6f, _yaw + sway, sway * 1.4f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _time += dt;
            // The room bounces to the beat (132 bpm).
            float beat = Mathf.Abs(Mathf.Sin(_time * Mathf.PI * 132f / 60f));
            for (int i = 0; i < _lights.Count; i++) _lights[i].intensity = 11f + beat * 5f * (i % 2 == 0 ? 1f : 0.5f);
            _fireLight.intensity = 8f + Mathf.PerlinNoise(_time * 6f, 0f) * 5f;
            _fire.localScale = new Vector3(1f, 0.85f + Mathf.PerlinNoise(_time * 5f, 3f) * 0.4f, 1f);
            for (int i = 0; i < _crowd.Count; i++)
            {
                var p = _crowd[i];
                float ph = _time * Mathf.PI * 132f / 60f + i;
                if (_crowdKind[i] == 2)
                {
                    // Dancing: bounce and pump the arms.
                    var d = Pose.Cheer;
                    float k = Mathf.Sin(ph);
                    d.ThighL = 10f + k * 9f; d.KneeL = 20f + k * 18f; d.ThighR = 10f - k * 9f; d.KneeR = 20f - k * 18f;
                    d.ArmOutL = 130f + k * 25f; d.ArmOutR = 130f - k * 25f; d.TorsoRoll = k * 7f;
                    p.Target = d;
                }
                else if (_crowdKind[i] == 1)
                {
                    // Drinkers lift the glass now and then.
                    p.Target = Mathf.Sin(_time * 0.5f + i * 1.7f) > 0.55f ? Pose.Drink : Pose.Stand.WithArms(10, 10, 20, 40, 14, 80);
                }
                else p.Target.TorsoRoll = Mathf.Sin(ph * 0.5f) * 3f;
            }
            if (WorldLook.Instance != null) WorldLook.Instance.Indoors = true;

            if (Game != null) { Game.Tick(dt); PlaceCameraForGame(dt); return; }
            if (Dialogs.Open) return;

            // Walk.
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (mouse != null && (UnityEngine.Cursor.lockState == CursorLockMode.Locked || mouse.rightButton.isPressed))
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
