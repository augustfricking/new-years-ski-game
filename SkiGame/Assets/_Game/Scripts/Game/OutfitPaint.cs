using System.Collections.Generic;
using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// Printed patterns for the sculpted clothes. The jacket and pants faces that carry a print
    /// get their own material with a small generated texture, wrapped round the body by where
    /// each vertex is (model space: metres, y up from the feet), so the print stays crisp.
    /// </summary>
    public static class OutfitPaint
    {
        public static readonly Color[] Colours =
        {
            Palette.Hex("#F7F4EE"), Palette.Hex("#22202E"), Palette.Hex("#E8455A"), Palette.Hex("#F2803A"), Palette.Hex("#FFC64A"),
            Palette.Hex("#8FD14F"), Palette.Hex("#2F9E6E"), Palette.Hex("#18A6A6"), Palette.Hex("#3A78E0"), Palette.Hex("#22305C"),
            Palette.Hex("#6C58C9"), Palette.Hex("#D8437F"), Palette.Hex("#8FE0C0"), Palette.Hex("#8A5A34"),
            // Rare, earned (Unlocks.RareColours): toxic green, neon pink, electric blue, ultraviolet.
            Palette.Hex("#7CFF3A"), Palette.Hex("#FF2E93"), Palette.Hex("#1FD1FF"), Palette.Hex("#7B2CFF"),
        };

        static readonly Color FlameRed = Palette.Hex("#D9342B"), FlameOrange = Palette.Hex("#F2803A"), FlameYellow = Palette.Hex("#FFD54A");
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Cache.Clear(); Previews.Clear(); }

        /// <summary>Whether a pattern prints on this colour slot.</summary>
        public static bool Prints(int pattern, int slot)
        {
            if (pattern <= 0) return false;
            if (slot == SkinModel.SlotJacket) return true;
            return slot == SkinModel.SlotPants && (pattern == 4 || pattern == 5 || pattern == 6 || pattern == 8 || pattern == 9 || pattern == 10
                                                    || pattern == Unlocks.PatternFur || pattern == Unlocks.PatternRainbow || pattern == Unlocks.PatternLeopard);
        }

        /// <summary>Where a vertex reads the print (the texture repeats across, and up for all but flames).</summary>
        public static Vector2 Uv(int pattern, Vector3 p, int slot, float hipY)
        {
            float across = p.x + p.z * 0.6f;
            if (pattern == 6)
            {
                float baseY = slot == SkinModel.SlotJacket ? hipY - 0.1f : 0.04f;
                return new Vector2(across / 0.32f, Mathf.Clamp01((p.y - baseY) / 0.5f));
            }
            if (pattern == 9) return new Vector2(p.x / 0.5f + 0.5f, p.y / 0.5f);                  // one pair of stripes down the middle
            if (pattern == Unlocks.PatternRainbow) return new Vector2(across / 0.5f, p.y / 1.1f); // the bands go once from ankle to shoulder
            float period = pattern == 4 || pattern == 5 || pattern == 8 ? 0.42f : pattern == 1 ? 0.2f : pattern == 11 ? 0.24f
                         : pattern == Unlocks.PatternDucks ? 0.22f : pattern == Unlocks.PatternHearts ? 0.17f
                         : pattern == Unlocks.PatternLightning ? 0.2f : pattern == Unlocks.PatternLeopard ? 0.26f : 0.15f;
            return new Vector2(across / period, p.y / period);
        }

        /// <summary>The material for printed faces: like <paramref name="plain"/>, with the print as its texture.</summary>
        public static Material Material(Material plain, int pattern, Color col, Color second)
        {
            if (ColourDistance(second, col) < 0.25f)
                second = col.grayscale > 0.5f ? Color.Lerp(col, Palette.Ink, 0.5f) : Color.Lerp(col, Color.white, 0.55f);
            string key = plain.name + pattern + ColorUtility.ToHtmlStringRGB(col) + ColorUtility.ToHtmlStringRGB(second);
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(plain) { name = plain.name + " print" };
            m.SetTexture("_BaseMap", Texture(pattern, col, second));
            Cache[key] = m;
            return m;
        }

        static readonly Dictionary<string, Texture2D> Previews = new Dictionary<string, Texture2D>();

        /// <summary>A patch of a print on these colours, for pictures in the interface.</summary>
        public static Texture2D Preview(int pattern, Color col, Color second)
        {
            if (ColourDistance(second, col) < 0.25f)
                second = col.grayscale > 0.5f ? Color.Lerp(col, Palette.Ink, 0.5f) : Color.Lerp(col, Color.white, 0.55f);
            string key = pattern + ColorUtility.ToHtmlStringRGB(col) + ColorUtility.ToHtmlStringRGB(second);
            if (Previews.TryGetValue(key, out var t) && t != null) return t;
            return Previews[key] = Texture(pattern, col, second);
        }

        static Texture2D Texture(int pattern, Color col, Color second)
        {
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "Print" + pattern, filterMode = FilterMode.Bilinear, anisoLevel = 2 };
            t.wrapModeU = TextureWrapMode.Repeat;
            t.wrapModeV = pattern == 6 ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (pattern == 9) t.wrapModeU = TextureWrapMode.Clamp;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                    px[y * N + x] = Pixel(pattern, u, v, col, second);
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        static Color Pixel(int pattern, float u, float v, Color col, Color second)
        {
            const float Tau = Mathf.PI * 2f;
            switch (pattern)
            {
                case 1: return v < 0.4f ? second : col;                                                 // stripes
                case 2: return (u < 0.5f) != (v < 0.5f) ? second : col;                                  // checks
                case 3:                                                                                   // dots, staggered
                {
                    float du = u - 0.25f, dv = v - 0.25f, du2 = u - 0.75f, dv2 = v - 0.75f;
                    return du * du + dv * dv < 0.022f || du2 * du2 + dv2 * dv2 < 0.022f ? second : col;
                }
                case 4:                                                                                   // camo: blobs that tile
                {
                    float n = 0.5f + 0.25f * Mathf.Sin(Tau * (u + 0.3f * Mathf.Sin(Tau * v))) + 0.25f * Mathf.Sin(Tau * (2f * v + 0.4f * Mathf.Cos(Tau * 2f * u)));
                    float n2 = 0.5f + 0.5f * Mathf.Sin(Tau * (3f * u + 2f * v) + 2f * Mathf.Sin(Tau * v));
                    float k = n * 0.75f + n2 * 0.25f;
                    return k < 0.38f ? Color.Lerp(col, Palette.Ink, 0.45f) : k > 0.66f ? Color.Lerp(col, second, 0.8f) : col;
                }
                case 5:                                                                                   // tiger: wavy dark stripes
                {
                    float w = Mathf.Sin(Tau * (3f * v + 0.25f * Mathf.Sin(Tau * 2f * u) + 0.08f * Mathf.Sin(Tau * 5f * u)));
                    float taper = 0.55f + 0.35f * Mathf.Sin(Tau * u);
                    return w > taper ? Color.Lerp(Palette.Ink, col, 0.12f) : col;
                }
                case 7:                                                                                   // candy: diagonal pink and white
                    return Mathf.Repeat(u + v, 0.5f) < 0.22f ? Palette.Hex("#F27BA8") : Mathf.Repeat(u + v, 0.5f) < 0.27f ? Palette.White : col;
                case 8:                                                                                   // graffiti: splashes and drips
                {
                    Color[] spray = { Palette.Hex("#FF4FA3"), Palette.Hex("#4FD8FF"), Palette.Hex("#FFC64A"), Palette.Hex("#8FD14F") };
                    float[,] blobs = { { 0.2f, 0.3f, 0.13f }, { 0.65f, 0.7f, 0.16f }, { 0.75f, 0.2f, 0.1f }, { 0.35f, 0.82f, 0.09f } };
                    for (int i = 0; i < 4; i++)
                    {
                        float dx = Mathf.Abs(u - blobs[i, 0]); dx = Mathf.Min(dx, 1f - dx);
                        float dy = Mathf.Abs(v - blobs[i, 1]); dy = Mathf.Min(dy, 1f - dy);
                        float r = blobs[i, 2] * (0.85f + 0.15f * Mathf.Sin(Mathf.Atan2(dy, dx) * 7f + i));
                        if (dx * dx + dy * dy < r * r) return spray[i];
                        if (dx < 0.015f && v < blobs[i, 1] && blobs[i, 1] - v < r + 0.12f) return spray[i];   // a drip running down
                    }
                    return col;
                }
                case 9:                                                                                   // race stripes, down the front and back
                    return Mathf.Abs(u - 0.43f) < 0.035f || Mathf.Abs(u - 0.57f) < 0.035f ? second : col;
                case 10:                                                                                  // skulls
                {
                    float x = Mathf.Repeat(u, 1f) - 0.5f, y = Mathf.Repeat(v, 1f) - 0.55f;
                    bool head = x * x + y * y < 0.05f, jaw = Mathf.Abs(x) < 0.11f && y < -0.12f && y > -0.26f;
                    bool eye = (x + 0.09f) * (x + 0.09f) + (y + 0.02f) * (y + 0.02f) < 0.0035f || (x - 0.09f) * (x - 0.09f) + (y + 0.02f) * (y + 0.02f) < 0.0035f;
                    return eye ? Color.Lerp(Palette.Ink, col, 0.2f) : head || jaw ? Palette.Hex("#EEE8DA") : col;
                }
                case 11:                                                                                  // pines, with snow on the tips
                {
                    float x = Mathf.Abs(Mathf.Repeat(u + (Mathf.Floor(v) % 2f) * 0.5f, 1f) - 0.5f), y = Mathf.Repeat(v, 1f);
                    if (y > 0.12f && y < 0.85f && x < (0.85f - y) * 0.5f) return y > 0.72f ? Palette.White : Palette.Hex("#2F6E4E");
                    if (y <= 0.12f && x < 0.05f) return Palette.Hex("#6B4A2E");
                    return col;
                }
                case Unlocks.PatternFur:                                                                  // shaggy fur: short strokes, light and dark
                {
                    float n = Mathf.Sin(Tau * (u * 7f + Mathf.Sin(Tau * v * 3f) * 0.3f)) * Mathf.Sin(Tau * (v * 11f + u * 2f));
                    return n > 0.55f ? Color.Lerp(col, Palette.Ink, 0.18f) : n < -0.6f ? Color.Lerp(col, Color.white, 0.5f) : col;
                }
                case Unlocks.PatternRainbow:                                                              // rainbow bands, ankle to shoulder
                {
                    Color[] bands = { Palette.Hex("#6C58C9"), Palette.Hex("#3A78E0"), Palette.Hex("#2F9E6E"), Palette.Hex("#FFC64A"), Palette.Hex("#F2803A"), Palette.Hex("#E8455A") };
                    return bands[Mathf.Clamp((int)(Mathf.Repeat(v, 1f) * bands.Length), 0, bands.Length - 1)];
                }
                case Unlocks.PatternDucks:                                                                // rubber ducks, every other row facing back
                {
                    bool flip = Mathf.Repeat(v, 1f) > 0.5f;
                    float x = Mathf.Repeat(u + (flip ? 0.5f : 0f), 1f) - 0.5f, y = Mathf.Repeat(v * 2f, 1f) - 0.45f;
                    x = (flip ? -x : x) * 2f;
                    bool body = (x * x) / 0.16f + (y * y) / 0.045f < 1f && y > -0.2f;
                    bool tail = x < -0.25f && x > -0.46f && y > -0.02f && y < 0.12f + (x + 0.25f) * -0.8f;
                    float hx = x - 0.24f, hy = y - 0.2f;
                    bool head = hx * hx + hy * hy < 0.022f;
                    bool beak = x > 0.36f && x < 0.52f && Mathf.Abs(y - 0.17f) < 0.045f - (x - 0.36f) * 0.15f;
                    bool eye = (hx - 0.04f) * (hx - 0.04f) + (hy - 0.03f) * (hy - 0.03f) < 0.0012f;
                    bool wing = ((x + 0.04f) * (x + 0.04f)) / 0.03f + ((y - 0.02f) * (y - 0.02f)) / 0.006f < 1f;
                    if (eye) return Palette.Ink;
                    if (beak) return Palette.Hex("#F2803A");
                    if (wing && body) return Palette.Hex("#E8B020");
                    if (body || head || tail) return Palette.Hex("#FFD84A");
                    return col;
                }
                case Unlocks.PatternHearts:                                                               // hearts, staggered
                {
                    float x = Mathf.Repeat(u + (Mathf.Floor(v) % 2f) * 0.5f, 1f) - 0.5f, y = Mathf.Repeat(v, 1f) - 0.5f;
                    x *= 2.6f; y = y * 2.6f + 0.15f;
                    float a = x * x + y * y - 0.3f;
                    bool heart = a * a * a - x * x * y * y * y < 0f;
                    return heart ? (ColourDistance(col, Palette.Hex("#E8455A")) < 0.5f ? Palette.White : Palette.Hex("#E8455A")) : col;
                }
                case Unlocks.PatternLightning:                                                            // lightning bolts, yellow with an ink edge
                {
                    float x = Mathf.Repeat(u + (Mathf.Floor(v) % 2f) * 0.5f, 1f), y = Mathf.Repeat(v, 1f);
                    if (InPolygon(x, y, Bolt)) return EdgeDistance(x, y, Bolt) < 0.03f ? Palette.Ink : FlameYellow;
                    return col;
                }
                case Unlocks.PatternLeopard:                                                              // leopard rosettes: dark rings round warm spots
                {
                    float best = 9f;
                    float[,] spots = { { 0.15f, 0.2f }, { 0.55f, 0.12f }, { 0.85f, 0.45f }, { 0.35f, 0.55f }, { 0.65f, 0.78f }, { 0.12f, 0.85f }, { 0.9f, 0.95f } };
                    for (int i = 0; i < spots.GetLength(0); i++)
                    {
                        float dx = Mathf.Abs(u - spots[i, 0]); dx = Mathf.Min(dx, 1f - dx);
                        float dy = Mathf.Abs(v - spots[i, 1]); dy = Mathf.Min(dy, 1f - dy);
                        float r = Mathf.Sqrt(dx * dx + dy * dy) * (1f + 0.18f * Mathf.Sin(Mathf.Atan2(dy, dx) * 3f + i));
                        best = Mathf.Min(best, r);
                    }
                    if (best < 0.05f) return Color.Lerp(col, Palette.Hex("#8A5A34"), 0.6f);
                    if (best < 0.085f) return Color.Lerp(Palette.Ink, col, 0.1f);
                    return col;
                }
                case 6:                                                                                   // flames from the bottom
                {
                    float top = 0.55f + 0.25f * Mathf.Sin(Tau * u) * Mathf.Sin(Tau * 3f * u + 1f) + 0.15f * Mathf.Sin(Tau * 5f * u);
                    if (v > top) return col;
                    float t = v / Mathf.Max(top, 0.05f);
                    return t < 0.55f ? Color.Lerp(FlameRed, FlameOrange, t / 0.55f) : Color.Lerp(FlameOrange, FlameYellow, (t - 0.55f) / 0.45f);
                }
            }
            return col;
        }

        /// <summary>A lightning bolt in one tile of the print (x, y pairs, y up).</summary>
        static readonly float[] Bolt = { 0.56f, 0.95f, 0.30f, 0.48f, 0.48f, 0.48f, 0.36f, 0.05f, 0.74f, 0.58f, 0.55f, 0.58f, 0.72f, 0.95f };

        static bool InPolygon(float x, float y, float[] p)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 2; i < p.Length; j = i, i += 2)
                if ((p[i + 1] > y) != (p[j + 1] > y) && x < (p[j] - p[i]) * (y - p[i + 1]) / (p[j + 1] - p[i + 1]) + p[i]) inside = !inside;
            return inside;
        }

        static float EdgeDistance(float x, float y, float[] p)
        {
            float best = 9f;
            for (int i = 0, j = p.Length - 2; i < p.Length; j = i, i += 2)
            {
                float ax = p[j], ay = p[j + 1], bx = p[i], by = p[i + 1];
                float dx = bx - ax, dy = by - ay;
                float t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / Mathf.Max(dx * dx + dy * dy, 1e-6f));
                float ex = ax + dx * t - x, ey = ay + dy * t - y;
                best = Mathf.Min(best, Mathf.Sqrt(ex * ex + ey * ey));
            }
            return best;
        }

        static float ColourDistance(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
    }
}
