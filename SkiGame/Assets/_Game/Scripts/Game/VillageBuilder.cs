using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// The village at the bottom of every run: the après-ski tavern, the lift base, fences, lamps
    /// and festive lights. Positions come from the <see cref="Course"/> so triggers and models agree.
    /// </summary>
    public static class VillageBuilder
    {
        public static Vector3 TavernDoor(Course c) => c.PointOnSurface(c.TavernZone.X, c.TavernZone.Z);
        public static Vector3 LiftGate(Course c) => c.PointOnSurface(c.LiftZone.X, c.LiftZone.Z);

        public static GameObject Build(PisteCourse c, Transform parent)
        {
            var root = new GameObject("Village");
            root.transform.SetParent(parent, false);
            float x0 = c.VillageX, z0 = c.FinishZ;
            var b = new MeshBuilder();
            var glow = new MeshBuilder();
            var bulbs = new MeshBuilder();

            // --- tavern -----------------------------------------------------------------------
            Vector3 tav = c.PointOnSurface(x0 + 33f, z0 + 94f);
            tav.y = c.Height(x0 + 25f, z0 + 94f) - 0.1f;
            b.Push(tav);
            Props.Cabin(b, new Vector3(16f, 5.8f, 22f), -1, true, Palette.Timber);
            for (int i = -2; i <= 2; i++)
            {
                if (i == 0) continue;
                Props.WindowPane(glow, b, new Vector3(-8.03f, 2.1f, i * 4.2f), -1, 1.5f, 1.3f);
                Props.WindowPane(glow, b, new Vector3(-8.03f, 4.4f, i * 4.2f), -1, 1.1f, 0.9f);
            }
            // Porch roof and steps at the door.
            b.Col = Palette.TimberDark;
            b.Box(new Vector3(-9.3f, 3.1f, 0f), new Vector3(2.6f, 0.16f, 4.6f));
            b.Cylinder(new Vector3(-10.4f, 0f, 2.1f), 0.1f, 0.1f, 3.1f, 6);
            b.Cylinder(new Vector3(-10.4f, 0f, -2.1f), 0.1f, 0.1f, 3.1f, 6);
            b.Col = Palette.Snow;
            b.Box(new Vector3(-9.3f, 3.26f, 0f), new Vector3(2.8f, 0.2f, 4.8f));
            b.Col = Palette.TimberLight;
            b.Box(new Vector3(-8.9f, 0.5f, 0f), new Vector3(1.8f, 0.2f, 3.2f));
            // Sign board above the porch.
            b.Col = Palette.TimberDark;
            b.Box(new Vector3(-8.4f, 4.1f, 0f), new Vector3(0.14f, 1.1f, 5.6f));
            glow.Col = Palette.Lamp;
            glow.Box(new Vector3(-8.5f, 4.1f, 0f), new Vector3(0.06f, 0.85f, 5.3f));
            b.Pop();
            Sign(root.transform, "AFTER SKI", tav + new Vector3(-8.56f, 4.1f, 0f), Quaternion.Euler(0, 90, 0), 0.62f, Palette.Ink);

            // --- lift base --------------------------------------------------------------------
            Vector3 hut = c.PointOnSurface(x0 - 33f, z0 + 82f);
            hut.y = c.Height(x0 - 27f, z0 + 82f) - 0.1f;
            b.Push(hut);
            Props.Cabin(b, new Vector3(10f, 3.6f, 14f), 1, false, Palette.TimberLight);
            Props.WindowPane(glow, b, new Vector3(5.03f, 2.0f, 3.6f), 1, 1.6f, 1.1f);
            Props.WindowPane(glow, b, new Vector3(5.03f, 2.0f, -3.6f), 1, 1.6f, 1.1f);
            b.Pop();
            Vector3 gate = LiftGate(c);
            b.Push(gate, Quaternion.Euler(0, 90, 0));
            Props.Arch(b, 7.5f, Palette.SignalYellow, 3.6f);
            b.Pop();
            Sign(root.transform, "LIFT", gate + new Vector3(0.12f, 3.15f, 0f), Quaternion.Euler(0, -90, 0), 0.55f, Palette.Ink);
            // Queue lane.
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 2; i++)
                {
                    b.Push(gate + new Vector3(1f + i * 3f, 0f, s * 3.6f));
                    Props.Fence(b);
                    b.Pop();
                }

            // --- trigger pads: visible spots to ski onto ----------------------------------------
            Pad(glow, gate + new Vector3(-1.5f, 0.05f, 0f), c.LiftZone.Radius * 0.55f, Palette.SignalYellow);
            Pad(glow, TavernDoor(c) + new Vector3(0f, 0.05f, 0f), c.TavernZone.Radius * 0.8f, Palette.Lamp);

            // --- fences around the plaza --------------------------------------------------------
            float far = z0 + Course.PlazaLength;
            for (float x = -Course.PlazaHalfWidth; x < Course.PlazaHalfWidth; x += 3f)
            {
                b.Push(c.PointOnSurface(x0 + x, far - 0.4f));
                Props.Fence(b);
                b.Pop();
            }
            for (int s = -1; s <= 1; s += 2)
                for (float z = z0 + 40f; z < far - 2f; z += 3f)
                {
                    float x = x0 + s * (c.HalfWidth(z) + 0.4f);
                    if (Mathf.Abs(z - (s > 0 ? z0 + 94f : z0 + 82f)) < (s > 0 ? 13f : 9f)) continue; // buildings stand here
                    b.Push(c.PointOnSurface(x, z), Quaternion.Euler(0, -90, 0));
                    Props.Fence(b);
                    b.Pop();
                }

            // --- lamps and strings of lights ----------------------------------------------------
            Vector3[] lamps =
            {
                c.PointOnSurface(x0 + 18f, z0 + 70f), c.PointOnSurface(x0 + 18f, z0 + 116f),
                c.PointOnSurface(x0 - 16f, z0 + 66f), c.PointOnSurface(x0 - 16f, z0 + 108f),
                c.PointOnSurface(x0 + 2f, z0 + 124f),
            };
            for (int i = 0; i < lamps.Length; i++)
            {
                float yaw = lamps[i].x > x0 ? 180f : 0f;
                b.Push(lamps[i], Quaternion.Euler(0, yaw, 0));
                Props.LampPost(b);
                b.Pop();
                Vector3 head = lamps[i] + Quaternion.Euler(0, yaw, 0) * new Vector3(0.7f, 4.05f, 0f);
                glow.Col = Palette.Lamp;
                glow.Sphere(head, 0.17f, 8, 5);
                var lg = new GameObject("Lamp light");
                lg.transform.SetParent(root.transform, false);
                lg.transform.position = head + Vector3.down * 0.6f;
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point; l.color = Palette.Lamp; l.range = 15f; l.intensity = 9f; l.shadows = LightShadows.None;
            }
            Color[] party = { Palette.Lamp, Palette.SignalRed, Palette.Jackets[0], Palette.Jackets[3], Palette.PisteGreen };
            Vector3 eave = tav + new Vector3(-8.6f, 4.9f, 0f);
            Festoon(b, bulbs, eave + new Vector3(0, 0, 8f), lamps[1] + Vector3.up * 4.2f, party, 0);
            Festoon(b, bulbs, eave + new Vector3(0, 0, -8f), lamps[0] + Vector3.up * 4.2f, party, 2);
            Festoon(b, bulbs, lamps[0] + Vector3.up * 4.2f, lamps[2] + Vector3.up * 4.2f, party, 1);
            Festoon(b, bulbs, lamps[1] + Vector3.up * 4.2f, lamps[3] + Vector3.up * 4.2f, party, 3);
            Festoon(b, bulbs, lamps[1] + Vector3.up * 4.2f, lamps[4] + Vector3.up * 4.2f, party, 4);

            // --- small life: a snowman, ski racks, benches --------------------------------------
            Vector3 sm = c.PointOnSurface(x0 + 12f, z0 + 112f);
            b.Push(sm);
            b.Col = Palette.Snow;
            b.Sphere(new Vector3(0, 0.45f, 0), 0.55f, 10, 7); b.Sphere(new Vector3(0, 1.15f, 0), 0.4f, 10, 7); b.Sphere(new Vector3(0, 1.7f, 0), 0.28f, 10, 7);
            b.Col = Palette.SafetyOrange; b.Push(new Vector3(0, 1.7f, 0), Quaternion.Euler(0, -120, 0) * Quaternion.Euler(90, 0, 0)); b.Cylinder(new Vector3(0, 0.25f, 0), 0.05f, 0f, 0.3f, 6); b.Pop();
            b.Col = Palette.Ink; b.Cylinder(new Vector3(0, 1.93f, 0), 0.3f, 0.3f, 0.03f, 10); b.Cylinder(new Vector3(0, 1.95f, 0), 0.19f, 0.17f, 0.3f, 10);
            b.Col = Palette.SignalRed; b.Cylinder(new Vector3(0, 1.38f, 0), 0.33f, 0.3f, 0.12f, 10, true, false);
            b.Pop();
            for (int i = 0; i < 2; i++)
            {
                Vector3 rack = c.PointOnSurface(x0 + 21.5f, z0 + 82f + i * 22f);
                b.Push(rack, Quaternion.Euler(0, 90, 0));
                SkiRack(b, i);
                b.Pop();
            }

            b.ToObject("Village props", Mats.Props, root.transform);
            glow.ToObject("Village glow", Mats.Glow(Color.white, 1.7f), root.transform, false, false);
            bulbs.ToObject("Village bulbs", Mats.Glow(Color.white, 2.2f), root.transform, false, false);
            BuildVillageForest(c, root.transform);
            return root;
        }

        static void Pad(MeshBuilder glow, Vector3 centre, float radius, Color colour)
        {
            glow.Col = colour;
            // A ring, so it reads as a marker rather than a solid disc.
            const int n = 28;
            for (int i = 0; i < n; i++)
            {
                if (i % 2 == 1) continue;
                float a0 = i / (float)n * Mathf.PI * 2f, a1 = (i + 1) / (float)n * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                glow.DoubleQuad(centre + d0 * radius * 0.82f, centre + d0 * radius, centre + d1 * radius, centre + d1 * radius * 0.82f);
            }
        }

        static void Festoon(MeshBuilder wire, MeshBuilder bulbs, Vector3 a, Vector3 b, Color[] colours, int offset)
        {
            const int n = 14;
            Vector3 prev = a;
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 1.1f;
                wire.Col = Palette.Ink;
                wire.Tube(prev, p, 0.012f, 0.012f, 3, false);
                if (i < n)
                {
                    bulbs.Col = colours[(i + offset) % colours.Length];
                    bulbs.Sphere(p + Vector3.down * 0.09f, 0.075f, 6, 4);
                }
                prev = p;
            }
        }

        static void SkiRack(MeshBuilder b, int seed)
        {
            b.Col = Palette.Timber;
            b.Box(new Vector3(0, 1.0f, 0), new Vector3(3.2f, 0.08f, 0.08f));
            b.Box(new Vector3(-1.5f, 0.5f, 0), new Vector3(0.1f, 1.0f, 0.1f));
            b.Box(new Vector3(1.5f, 0.5f, 0), new Vector3(0.1f, 1.0f, 0.1f));
            var rng = new Rng(40 + seed);
            for (int i = 0; i < 6; i++)
            {
                Color col = Palette.Jackets[rng.Range(0, Palette.Jackets.Length)];
                b.Push(new Vector3(-1.2f + i * 0.48f, 0.02f, 0.12f), Quaternion.Euler(-82f, 0, rng.Range(-4f, 4f)));
                GearModels.Ski(b, rng.Range(1.4f, 1.7f), rng.Chance(0.4f), col, Palette.White);
                b.Pop();
            }
        }

        static void BuildVillageForest(PisteCourse c, Transform parent)
        {
            // Trees behind the fences and buildings, framing the plaza.
            var b = new MeshBuilder();
            var rng = new Rng(c.Seed + 900);
            float z0 = c.FinishZ + 30f, z1 = c.FinishZ + Course.PlazaLength + 40f;
            for (int i = 0; i < 260; i++)
            {
                float z = rng.Range(z0, z1), side = rng.Sign();
                float x = z > c.FinishZ + Course.PlazaLength + 3f
                    ? c.VillageX + rng.Range(-70f, 70f)
                    : c.VillageX + side * (c.HalfWidth(z) + rng.Range(14f, 60f));
                float scale = rng.Range(0.9f, 1.8f);
                int variant = rng.Range(0, 3);
                b.Push(new Vector3(x, c.SnowHeight(x, z) - 0.15f, z), Quaternion.Euler(0, rng.Range(0f, 360f), 0), Vector3.one * scale);
                Props.Pine(b, variant, rng);
                b.Pop();
            }
            b.ToObject("Village trees", Mats.Trees, parent, true, false);
        }

        /// <summary>Flat text in the world, for signs.</summary>
        public static TextMesh Sign(Transform parent, string text, Vector3 position, Quaternion rotation, float height, Color colour)
        {
            var go = new GameObject("Sign " + text);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var tm = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            tm.text = text;
            tm.fontSize = 96;
            tm.characterSize = height * 0.105f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = colour;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return tm;
        }
    }
}
