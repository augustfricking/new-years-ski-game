using System.Collections.Generic;
using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// Procedural props, drawn into a <see cref="MeshBuilder"/> at its current transform.
    /// Local space: origin on the ground, +Y up. All colours come from the palette.
    /// </summary>
    public static class Props
    {
        /// <summary>A snow-dusted pine. About 26 triangles; height about 6 m at scale 1.</summary>
        public static void Pine(MeshBuilder b, int variant, Rng rng = null)
        {
            float h = variant == 0 ? 6.2f : variant == 1 ? 7.4f : 5.2f;
            float w = variant == 0 ? 1.55f : variant == 1 ? 1.35f : 1.8f;
            int tiers = variant == 1 ? 4 : 3;
            float lean = rng != null ? rng.Range(-0.04f, 0.04f) : 0f;
            b.Col = Palette.Bark;
            b.Cylinder(Vector3.zero, 0.17f, 0.11f, h * 0.3f, 4, true, false);
            float y = h * 0.16f;
            for (int t = 0; t < tiers; t++)
            {
                float k = t / (float)tiers;
                float r = w * (1f - k * 0.62f);
                float th = h * (variant == 1 ? 0.34f : 0.42f) * (1f - k * 0.2f);
                Cone(b, new Vector3(lean * y, y, 0f), r, th, 6, t % 2 == 0 ? Palette.Pine : Palette.PineLight, t * 0.5f);
                y += th * 0.52f;
            }
        }

        /// <summary>A cheaper pine for trees nobody gets close to: two plain tiers, no trunk. 20 triangles.</summary>
        public static void PineFar(MeshBuilder b, int variant)
        {
            float h = variant == 0 ? 6.2f : variant == 1 ? 7.4f : 5.2f;
            float w = variant == 0 ? 1.55f : variant == 1 ? 1.35f : 1.8f;
            for (int t = 0; t < 2; t++)
            {
                Vector3 c = new Vector3(0f, h * (0.12f + t * 0.36f), 0f);
                float r = w * (1f - t * 0.38f), th = h * 0.52f;
                Vector3 apex = c + Vector3.up * th;
                for (int i = 0; i < 5; i++)
                {
                    float a0 = i / 5f * Mathf.PI * 2f + t, a1 = (i + 1) / 5f * Mathf.PI * 2f + t;
                    Vector3 p0 = c + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r, p1 = c + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r;
                    Vector3 m0 = Vector3.Lerp(p0, apex, 0.45f), m1 = Vector3.Lerp(p1, apex, 0.45f);
                    b.Col = t == 0 ? Palette.Pine : Palette.PineLight; b.Tri(p0, m0, p1); b.Tri(p1, m0, m1);
                    b.Col = Palette.Mix(Palette.PineLight, Palette.Snow, 0.85f); b.Tri(m0, apex, m1);
                }
            }
        }

        /// <summary>A cone whose tip is snow-white, fading to the given colour at the skirt.</summary>
        static void Cone(MeshBuilder b, Vector3 baseCentre, float r, float h, int sides, Color skirt, float twist)
        {
            Color snow = Palette.Mix(skirt, Palette.Snow, 0.85f);
            Vector3 apex = baseCentre + Vector3.up * h;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f + twist, a1 = (i + 1) / (float)sides * Mathf.PI * 2f + twist;
                Vector3 p0 = baseCentre + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r;
                Vector3 p1 = baseCentre + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r;
                // Skirt band in pine green, upper part snow: two colours, hard edge.
                Vector3 m0 = Vector3.Lerp(p0, apex, 0.42f), m1 = Vector3.Lerp(p1, apex, 0.42f);
                b.Col = skirt; b.Quad(p0, m0, m1, p1);
                b.Col = snow; b.Tri(m0, apex, m1);
            }
        }

        public static void DeadTree(MeshBuilder b, int variant)
        {
            b.Col = Palette.DeadWood;
            float h = variant == 0 ? 4.2f : 3.2f;
            b.Cylinder(Vector3.zero, 0.2f, 0.07f, h, 5, true, false);
            b.Tube(new Vector3(0, h * 0.55f, 0), new Vector3(0.9f, h * 0.8f, 0.2f), 0.06f, 0.02f, 4, false);
            b.Tube(new Vector3(0, h * 0.4f, 0), new Vector3(-0.7f, h * 0.62f, -0.3f), 0.06f, 0.02f, 4, false);
            b.Tube(new Vector3(0, h * 0.7f, 0), new Vector3(-0.3f, h * 0.95f, 0.5f), 0.05f, 0.02f, 4, false);
        }

        /// <summary>A lumpy boulder with snow on top. Radius about 1 at scale 1.</summary>
        public static void Rock(MeshBuilder b, int variant)
        {
            var rng = new Rng(100 + variant);
            Vector3 radii = new Vector3(rng.Range(0.9f, 1.2f), rng.Range(0.55f, 0.85f), rng.Range(0.85f, 1.15f));
            const int seg = 7, rings = 4;
            var ring = new Vector3[rings + 1, seg];
            for (int y = 0; y <= rings; y++)
                for (int x = 0; x < seg; x++)
                {
                    float phi = y / (float)rings * Mathf.PI * 0.62f, th = x / (float)seg * Mathf.PI * 2f;
                    Vector3 d = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                    ring[y, x] = Vector3.Scale(d, radii) * rng.Range(0.82f, 1.12f) + Vector3.down * 0.12f;
                }
            for (int y = 0; y < rings; y++)
                for (int x = 0; x < seg; x++)
                {
                    int x1 = (x + 1) % seg;
                    b.Col = y == 0 ? Palette.Snow : y == 1 ? Palette.Rock : Palette.RockDark;
                    if (y == 0) b.Tri(ring[0, 0], ring[1, x1], ring[1, x]);
                    else b.Quad(ring[y, x], ring[y, x1], ring[y + 1, x1], ring[y + 1, x]);
                }
        }

        public static void Bush(MeshBuilder b, int variant)
        {
            var rng = new Rng(300 + variant);
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new Vector3(rng.Range(-0.5f, 0.5f), rng.Range(0.2f, 0.45f), rng.Range(-0.5f, 0.5f));
                float r = rng.Range(0.35f, 0.55f);
                b.Col = Palette.Bush;
                b.Sphere(p, new Vector3(r, r * 0.8f, r), 6, 3);
                b.Col = Palette.Snow;
                b.Sphere(p + Vector3.up * r * 0.45f, new Vector3(r * 0.8f, r * 0.45f, r * 0.8f), 6, 3);
            }
            // Bare twigs.
            b.Col = Palette.TimberDark;
            for (int i = 0; i < 5; i++)
                b.Tube(new Vector3(rng.Range(-0.3f, 0.3f), 0.3f, rng.Range(-0.3f, 0.3f)), new Vector3(rng.Range(-0.8f, 0.8f), rng.Range(0.9f, 1.3f), rng.Range(-0.8f, 0.8f)), 0.025f, 0.01f, 3, false);
        }

        public static void Obstacle(MeshBuilder b, in Obstacle o, Rng rng)
        {
            switch (o.Kind)
            {
                case ObstacleKind.Tree: Pine(b, o.Variant, rng); break;
                case ObstacleKind.DeadTree: DeadTree(b, o.Variant); break;
                case ObstacleKind.Rock: Rock(b, o.Variant); break;
                case ObstacleKind.Bush: Bush(b, o.Variant); break;
            }
        }

        /// <summary>A slalom pole with a flag panel hanging toward +X (or -X).</summary>
        public static void GatePole(MeshBuilder b, Color colour, float flagDir, bool flag)
        {
            b.Col = colour;
            b.Cylinder(Vector3.zero, 0.045f, 0.04f, 2.1f, 6);
            b.Col = Palette.White;
            b.Cylinder(new Vector3(0, 0.55f, 0), 0.05f, 0.05f, 0.2f, 6, true, false);
            if (flag)
            {
                b.Col = colour;
                b.DoubleQuad(new Vector3(0, 1.1f, 0), new Vector3(0, 2.0f, 0), new Vector3(flagDir * 1.15f, 2.0f, 0), new Vector3(flagDir * 1.15f, 1.1f, 0));
                b.Col = Palette.White;
                b.DoubleQuad(new Vector3(flagDir * 0.2f, 1.42f, 0.01f), new Vector3(flagDir * 0.2f, 1.68f, 0.01f), new Vector3(flagDir * 0.95f, 1.68f, 0.01f), new Vector3(flagDir * 0.95f, 1.42f, 0.01f));
            }
        }

        /// <summary>A wooden post-and-rail fence segment along +X, 3 m long.</summary>
        public static void Fence(MeshBuilder b, float length = 3f)
        {
            b.Col = Palette.Timber;
            b.Box(new Vector3(0, 0.55f, 0), new Vector3(0.14f, 1.1f, 0.14f));
            b.Col = Palette.TimberLight;
            b.Box(new Vector3(length * 0.5f, 0.85f, 0), new Vector3(length, 0.1f, 0.06f));
            b.Box(new Vector3(length * 0.5f, 0.45f, 0), new Vector3(length, 0.1f, 0.06f));
            b.Col = Palette.Snow;
            b.Box(new Vector3(0, 1.13f, 0), new Vector3(0.2f, 0.07f, 0.2f));
        }

        /// <summary>Orange safety netting between poles, along +X.</summary>
        public static void SafetyNet(MeshBuilder b, float length)
        {
            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 4f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float x = i / (float)(posts - 1) * length;
                b.Col = Palette.SignalBlue;
                b.Cylinder(new Vector3(x, 0, 0), 0.03f, 0.03f, 1.5f, 5);
            }
            b.Col = Palette.SafetyOrange;
            b.DoubleQuad(new Vector3(0, 0.25f, 0), new Vector3(0, 1.35f, 0), new Vector3(length, 1.35f, 0), new Vector3(length, 0.25f, 0));
        }

        /// <summary>A banner arch across the piste (start or finish), spanning -w/2..w/2 on X.</summary>
        public static void Arch(MeshBuilder b, float width, Color colour, float height = 4.2f)
        {
            float hw = width * 0.5f;
            b.Col = Palette.SteelDark;
            b.Cylinder(new Vector3(-hw, 0, 0), 0.13f, 0.1f, height, 6);
            b.Cylinder(new Vector3(hw, 0, 0), 0.13f, 0.1f, height, 6);
            b.Col = colour;
            b.Box(new Vector3(0, height - 0.45f, 0), new Vector3(width + 0.3f, 0.9f, 0.14f));
            b.Col = Palette.White;
            // Chequer strip.
            int n = Mathf.RoundToInt(width / 0.6f);
            for (int i = 0; i < n; i += 2)
                b.Box(new Vector3(-hw + (i + 0.5f) * width / n, height - 0.95f, 0), new Vector3(width / n, 0.14f, 0.16f));
            b.Col = Palette.Snow;
            b.Box(new Vector3(0, height + 0.04f, 0), new Vector3(width + 0.4f, 0.1f, 0.26f));
        }

        public static void LampPost(MeshBuilder b, float height = 4.2f)
        {
            b.Col = Palette.SteelDark;
            b.Cylinder(Vector3.zero, 0.09f, 0.06f, height, 6);
            b.Tube(new Vector3(0, height, 0), new Vector3(0.7f, height + 0.15f, 0), 0.04f, 4);
            b.Col = Palette.Roof;
            b.Cylinder(new Vector3(0.7f, height - 0.05f, 0), 0.26f, 0.08f, 0.2f, 6);
        }

        /// <summary>A lift pylon: tapered tower with a cross arm. Cable passes at (+-armHalf, height).</summary>
        public static void Pylon(MeshBuilder b, float height, float armHalf = 2.1f)
        {
            b.Col = Palette.Steel;
            b.TaperedBox(Vector3.zero, new Vector3(0.7f, 0, 0.7f), new Vector2(0.45f, 0.45f), height);
            b.Col = Palette.SteelDark;
            b.Box(new Vector3(0, height, 0), new Vector3(armHalf * 2f + 0.6f, 0.28f, 0.34f));
            for (int s = -1; s <= 1; s += 2)
            {
                b.Col = Palette.SteelDark;
                b.Box(new Vector3(s * armHalf, height - 0.25f, 0), new Vector3(0.16f, 0.5f, 1.5f));
                b.Col = Palette.Ink;
                for (int w = -1; w <= 1; w++)
                {
                    b.Push(new Vector3(s * armHalf, height - 0.45f, w * 0.55f), Quaternion.Euler(0, 0, 90));
                    b.Cylinder(new Vector3(0, -0.06f, 0), 0.17f, 0.17f, 0.12f, 8);
                    b.Pop();
                }
            }
            b.Col = Palette.Snow;
            b.Box(new Vector3(0, height + 0.17f, 0), new Vector3(armHalf * 2f + 0.7f, 0.08f, 0.42f));
            b.Col = Palette.SignalYellow;
            b.Box(new Vector3(0, 1.4f, -0.37f), new Vector3(0.5f, 0.5f, 0.04f));
        }

        /// <summary>A four-seat lift chair hanging from the cable. Origin at the cable grip; seat is 3.1 m below.</summary>
        public static void Chair(MeshBuilder b, bool withBar = true)
        {
            const float drop = 3.1f, w = 2.3f;
            b.Col = Palette.SteelDark;
            b.Box(new Vector3(0, -0.06f, 0), new Vector3(0.14f, 0.2f, 0.42f)); // grip
            b.Tube(new Vector3(0, -0.1f, 0), new Vector3(0, -1.5f, -0.25f), 0.045f, 6);
            b.Tube(new Vector3(0, -1.5f, -0.25f), new Vector3(0, -drop + 1.25f, -0.42f), 0.045f, 6);
            // Frame: top bar, side hoops.
            b.Tube(new Vector3(-w * 0.5f, -drop + 1.25f, -0.42f), new Vector3(w * 0.5f, -drop + 1.25f, -0.42f), 0.04f, 6);
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * w * 0.5f;
                b.Tube(new Vector3(x, -drop + 1.25f, -0.42f), new Vector3(x, -drop + 0.05f, -0.34f), 0.04f, 6);
                b.Tube(new Vector3(x, -drop + 0.05f, -0.34f), new Vector3(x, -drop, 0.3f), 0.04f, 6);
            }
            // Seat and backrest slats.
            b.Col = Palette.Timber;
            b.Box(new Vector3(0, -drop, 0f), new Vector3(w, 0.07f, 0.52f));
            b.Col = Palette.TimberLight;
            b.Box(new Vector3(0, -drop + 0.42f, -0.36f), new Vector3(w, 0.2f, 0.05f));
            b.Box(new Vector3(0, -drop + 0.72f, -0.4f), new Vector3(w, 0.2f, 0.05f));
            if (withBar)
            {
                // Safety bar, down, with a foot rest.
                b.Col = Palette.Steel;
                b.Tube(new Vector3(-w * 0.5f, -drop + 0.55f, 0.42f), new Vector3(w * 0.5f, -drop + 0.55f, 0.42f), 0.035f, 6);
                for (int s = -1; s <= 1; s += 2)
                    b.Tube(new Vector3(s * w * 0.5f, -drop + 1.2f, -0.4f), new Vector3(s * w * 0.5f, -drop + 0.55f, 0.42f), 0.035f, 6);
                b.Tube(new Vector3(0, -drop + 0.55f, 0.42f), new Vector3(0, -drop - 0.55f, 0.62f), 0.03f, 6);
                b.Tube(new Vector3(-w * 0.42f, -drop - 0.55f, 0.62f), new Vector3(w * 0.42f, -drop - 0.55f, 0.62f), 0.03f, 6);
            }
        }

        /// <summary>A timber cabin with a pitched, snow-covered roof. Footprint size.x by size.z, door on -X or +X.</summary>
        public static void Cabin(MeshBuilder b, Vector3 size, int doorSide, bool chimney, Color wall)
        {
            float w = size.x, h = size.y, d = size.z;
            // Stone base.
            b.Col = Palette.RockDark;
            b.Box(new Vector3(0, 0.3f, 0), new Vector3(w + 0.2f, 0.6f, d + 0.2f));
            // Log walls as stacked planks with alternating tone.
            int rows = Mathf.RoundToInt((h - 0.6f) / 0.42f);
            for (int i = 0; i < rows; i++)
            {
                b.Col = i % 2 == 0 ? wall : Palette.Mul(wall, 0.86f);
                b.Box(new Vector3(0, 0.6f + (i + 0.5f) * 0.42f, 0), new Vector3(w + (i % 2 == 0 ? 0.08f : 0f), 0.42f, d + (i % 2 == 0 ? 0f : 0.08f)));
            }
            // Roof: two slabs with an overhang, plus snow on top.
            float top = 0.6f + rows * 0.42f, rise = w * 0.33f, over = 0.7f;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 eave = new Vector3(s * (w * 0.5f + over), top - 0.25f, 0), ridge = new Vector3(0, top + rise, 0);
                Vector3 dz = new Vector3(0, 0, d * 0.5f + over);
                Vector3 up = new Vector3(0, 0.16f, 0);
                b.Col = Palette.Roof;
                Slab(b, eave - dz, eave + dz, ridge + dz, ridge - dz, s > 0, 0.16f);
                b.Col = Palette.Snow;
                Slab(b, eave - dz + up, eave + dz + up, ridge + dz + up, ridge - dz + up, s > 0, 0.26f);
            }
            // Gables.
            b.Col = Palette.Mul(wall, 0.93f);
            for (int s = -1; s <= 1; s += 2)
            {
                float z = s * d * 0.5f;
                Vector3 a = new Vector3(-w * 0.5f, top, z), c = new Vector3(w * 0.5f, top, z), r = new Vector3(0, top + rise - 0.1f, z);
                if (s > 0) b.Tri(a, r, c); else b.Tri(c, r, a);
            }
            // Door.
            float dx = doorSide * (w * 0.5f + 0.03f);
            b.Col = Palette.TimberDark;
            b.Box(new Vector3(dx, 1.65f, 0), new Vector3(0.1f, 2.1f, 1.3f));
            b.Col = Palette.Lamp;
            b.Box(new Vector3(dx + doorSide * 0.06f, 1.7f, 0.42f), new Vector3(0.05f, 0.12f, 0.12f));
            if (chimney)
            {
                b.Col = Palette.Rock;
                b.Box(new Vector3(-w * 0.22f, top + rise * 0.7f + 0.5f, d * 0.2f), new Vector3(0.8f, 1.8f, 0.8f));
                b.Col = Palette.Snow;
                b.Box(new Vector3(-w * 0.22f, top + rise * 0.7f + 1.45f, d * 0.2f), new Vector3(0.95f, 0.14f, 0.95f));
            }
        }

        static void Slab(MeshBuilder b, Vector3 e0, Vector3 e1, Vector3 r1, Vector3 r0, bool flip, float thick)
        {
            Vector3 n = Vector3.Cross(e1 - e0, r0 - e0).normalized;
            if (n.y < 0f) n = -n;
            Vector3 t = n * thick;
            if (flip) { b.Quad(e0 + t, r0 + t, r1 + t, e1 + t); b.Quad(e1, r1, r0, e0); b.Quad(e0, e0 + t, e1 + t, e1); }
            else { b.Quad(e1 + t, r1 + t, r0 + t, e0 + t); b.Quad(e0, r0, r1, e1); b.Quad(e1, e1 + t, e0 + t, e0); }
            b.Quad(e0, r0, r0 + t, e0 + t); b.Quad(e1 + t, r1 + t, r1, e1);
            b.Quad(e0 + t, r0 + t, r0, e0); b.Quad(e1, r1, r1 + t, e1 + t);
        }

        /// <summary>A glowing window pane (drawn with an emissive material), in the YZ plane facing +X * side.</summary>
        public static void WindowPane(MeshBuilder glow, MeshBuilder frame, Vector3 centre, int side, float w = 1.1f, float h = 1.0f)
        {
            Vector3 off = new Vector3(side * 0.02f, 0, 0);
            glow.Col = Palette.Window;
            glow.Box(centre + off, new Vector3(0.06f, h, w));
            frame.Col = Palette.TimberDark;
            frame.Box(centre + off * 2f, new Vector3(0.08f, h + 0.16f, 0.07f));
            frame.Box(centre + off * 2f, new Vector3(0.08f, 0.07f, w + 0.16f));
            frame.Box(centre + off + new Vector3(0, -h * 0.5f - 0.06f, 0), new Vector3(0.22f, 0.08f, w + 0.3f));
            frame.Col = Palette.Snow;
            frame.Box(centre + off + new Vector3(side * 0.05f, -h * 0.5f, 0), new Vector3(0.2f, 0.07f, w + 0.26f));
        }

        /// <summary>A piste marker disc on a pole, coloured for the slope.</summary>
        public static void PisteMarker(MeshBuilder b, Color colour)
        {
            b.Col = Palette.SteelDark;
            b.Cylinder(Vector3.zero, 0.035f, 0.035f, 2.1f, 5);
            b.Col = colour;
            b.Push(new Vector3(0, 2.0f, 0), Quaternion.Euler(90, 0, 0));
            b.Cylinder(new Vector3(0, -0.03f, 0), 0.24f, 0.24f, 0.06f, 12);
            b.Pop();
        }

        /// <summary>A jib box: a low coloured box with a rail-like top edge. Length along +Z from the origin.</summary>
        public static void JibBox(MeshBuilder b, float length, float width, float top, Color colour)
        {
            b.Col = colour;
            b.Box(new Vector3(0, top * 0.5f - 0.05f, length * 0.5f), new Vector3(width, top + 0.1f, length));
            b.Col = Palette.White;
            b.Box(new Vector3(0, top + 0.01f, length * 0.5f), new Vector3(width + 0.06f, 0.05f, length + 0.06f));
            b.Col = Palette.Ink;
            b.Box(new Vector3(width * 0.5f + 0.02f, top - 0.02f, length * 0.5f), new Vector3(0.05f, 0.06f, length));
            b.Box(new Vector3(-width * 0.5f - 0.02f, top - 0.02f, length * 0.5f), new Vector3(0.05f, 0.06f, length));
        }
    }
}
