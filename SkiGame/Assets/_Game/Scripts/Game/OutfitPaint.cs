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
        };

        static readonly Color FlameRed = Palette.Hex("#D9342B"), FlameOrange = Palette.Hex("#F2803A"), FlameYellow = Palette.Hex("#FFD54A");
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Cache.Clear();

        /// <summary>Whether a pattern prints on this colour slot.</summary>
        public static bool Prints(int pattern, int slot)
        {
            if (pattern <= 0) return false;
            if (slot == SkinModel.SlotJacket) return true;
            return slot == SkinModel.SlotPants && (pattern == 4 || pattern == 5 || pattern == 6);
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
            float period = pattern == 4 || pattern == 5 ? 0.42f : pattern == 1 ? 0.2f : 0.15f;
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

        static Texture2D Texture(int pattern, Color col, Color second)
        {
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "Print" + pattern, filterMode = FilterMode.Bilinear, anisoLevel = 2 };
            t.wrapModeU = TextureWrapMode.Repeat;
            t.wrapModeV = pattern == 6 ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
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

        static float ColourDistance(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
    }
}
