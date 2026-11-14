using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// The ski-goggle frame around the first-person view: a dark foam rim with a nose notch and a
    /// faint lens tint, drawn once into a texture.
    /// </summary>
    public static class GoggleTexture
    {
        static Texture2D _tex;

        public static Texture2D Get()
        {
            if (_tex != null) return _tex;
            const int w = 640, h = 360;
            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Goggles", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            Color ink = Palette.Ink, rim = Palette.InkSoft, tint = new Color(1f, 0.72f, 0.45f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                    // Lens outline: a rounded rectangle (super-ellipse) reaching past the screen at the sides.
                    float d = Mathf.Pow(Mathf.Abs(u) / 1.035f, 5f) + Mathf.Pow(Mathf.Abs(v) / 1.02f, 4.2f);
                    // Nose notch at the bottom centre.
                    float nose = Mathf.Exp(-(u * u) / (0.13f * 0.13f)) * 0.2f;
                    float bottom = -1.02f + nose;
                    float noseD = v < bottom + 0.2f ? Mathf.InverseLerp(bottom + 0.09f, bottom, v) : 0f;
                    float frame = Mathf.Max(Mathf.InverseLerp(0.8f, 1.0f, d), noseD);
                    Color c;
                    float a;
                    if (frame >= 1f) { c = ink; a = 1f; }
                    else if (frame > 0.72f) { c = Color.Lerp(rim, ink, (frame - 0.72f) / 0.28f); a = 1f; }             // foam rim
                    else if (frame > 0.6f) { c = rim; a = Mathf.InverseLerp(0.6f, 0.72f, frame); a *= a; }               // soft inner edge
                    else
                    {
                        // Inside the lens: a whisper of warm tint, stronger toward the top, and a reflection streak.
                        float streak = Mathf.Clamp01(1f - Mathf.Abs(u * 0.55f + v - 0.92f) * 9f) * 0.05f;
                        c = Color.Lerp(tint, Color.white, streak > 0f ? 0.8f : 0f);
                        a = 0.035f + Mathf.Clamp01(v) * 0.03f + streak;
                    }
                    px[y * w + x] = new Color(c.r, c.g, c.b, a);
                }
            _tex.SetPixels32(px);
            _tex.Apply(false, true);
            return _tex;
        }
    }
}
