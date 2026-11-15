using System.Collections.Generic;
using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// Paints the printed piste map as a texture, in the manner of a real resort panorama: a
    /// hill-shaded snow mountain with rock bands, stippled forest with corridors cut for the
    /// pistes, thin coloured piste lines, a lift line with pylons, the village, a header band,
    /// a legend panel, fold creases and paper grain. Symbols and lettering are added on top by
    /// <see cref="MapScene"/>.
    /// </summary>
    public static class MapPainter
    {
        public const int W = 1280, H = 900;
        const float Aspect = H / (float)W;          // map units: x in -0.5..0.5, y in -Aspect/2..Aspect/2
        public const float FrameX = 0.488f, FrameY = 0.338f, HeaderY = 0.298f;
        public static readonly Vector2 Peak = new Vector2(0.01f, 0.262f);
        public static readonly Vector2 Village = new Vector2(0f, -0.296f);
        public static readonly Vector2 LegendCentre = new Vector2(-0.385f, 0.2f), LegendSize = new Vector2(0.2f, 0.17f);

        static Texture2D _tex;

        static float Fbm(float x, float y, int oct)
        {
            float a = 0.5f, s = 0f, f = 1f;
            for (int i = 0; i < oct; i++) { s += a * Mathf.PerlinNoise(x * f + 31.7f, y * f + 57.3f); a *= 0.5f; f *= 2.03f; }
            return s / (1f - a * 2f);   // 0..1
        }

        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }
        static Vector3 Mix(Vector3 a, Vector3 b, float t) => a + (b - a) * Mathf.Clamp01(t);
        static Vector3 V(Color c) => new Vector3(c.r, c.g, c.b);

        /// <summary>The skyline of the mountain at a map x.</summary>
        static float Ridge(float x)
        {
            float d = Mathf.Abs(x - Peak.x);
            float r = Peak.y - 0.6f * Mathf.Pow(d, 1.22f);
            r += 0.034f * Mathf.Exp(-((x + 0.3f) * (x + 0.3f)) / 0.0045f) + 0.03f * Mathf.Exp(-((x - 0.33f) * (x - 0.33f)) / 0.0035f);
            return r + (Mathf.PerlinNoise(x * 23f + 4f, 2.5f) - 0.5f) * 0.016f;
        }

        static void Stamp(List<Vector2> line, float radius, float[] dist, byte[] who, byte id, float[] along)
        {
            float acc = 0f;
            int rpx = Mathf.CeilToInt(radius * W) + 1;
            for (int i = 0; i < line.Count - 1; i++)
            {
                Vector2 a = line[i], b = line[i + 1];
                float len = (b - a).magnitude;
                int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, b.x) + 0.5f) * W) - rpx, 0, W - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x, b.x) + 0.5f) * W) + rpx, 0, W - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.y, b.y) / Aspect + 0.5f) * H) - rpx, 0, H - 1), y1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.y, b.y) / Aspect + 0.5f) * H) + rpx, 0, H - 1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float mx = (x + 0.5f) / W - 0.5f, my = ((y + 0.5f) / H - 0.5f) * Aspect;
                        float d = M.DistToSegment(mx, my, a.x, a.y, b.x, b.y, out float t);
                        int k = y * W + x;
                        if (d < dist[k]) { dist[k] = d; if (who != null) who[k] = id; if (along != null) along[k] = acc + t * len; }
                    }
                acc += len;
            }
        }

        public static Texture2D Get(List<Vector2>[] runs, List<Vector2> lift)
        {
            if (_tex != null) return _tex;
            int n = W * H;
            var dPiste = new float[n]; var dOff = new float[n]; var dLift = new float[n];
            var who = new byte[n]; var alongOff = new float[n]; var alongLift = new float[n];
            for (int i = 0; i < n; i++) { dPiste[i] = 1f; dOff[i] = 1f; dLift[i] = 1f; }
            for (int r = 0; r < 4; r++) Stamp(runs[r], 0.026f, dPiste, who, (byte)r, null);
            Stamp(runs[4], 0.014f, dOff, null, 0, alongOff);
            Stamp(lift, 0.012f, dLift, null, 0, alongLift);

            Vector3 paper = new Vector3(0.975f, 0.965f, 0.94f), ink = V(Palette.Ink);
            Vector3 skyTop = new Vector3(0.5f, 0.68f, 0.88f), skyLow = new Vector3(0.87f, 0.92f, 0.96f);
            Vector3 snowLit = new Vector3(1f, 1f, 1f), snowShade = new Vector3(0.58f, 0.68f, 0.88f);
            Vector3 rockLit = new Vector3(0.7f, 0.69f, 0.72f), rockShade = new Vector3(0.36f, 0.38f, 0.5f);
            Vector3 treeDark = new Vector3(0.08f, 0.2f, 0.17f), treeLit = new Vector3(0.27f, 0.47f, 0.31f);
            Vector3 header = new Vector3(0.11f, 0.26f, 0.5f);
            Vector3 offCol = V(Palette.Slope(SlopeId.OffPiste));
            var runCol = new Vector3[4];
            for (int r = 0; r < 4; r++) runCol[r] = V(Palette.Slope((SlopeId)r));
            // Village: a cluster of snowy roofs (centre x, centre y, width, height).
            var houses = new[]
            {
                new Vector4(-0.052f, 0.004f, 0.022f, 0.012f), new Vector4(-0.022f, -0.008f, 0.027f, 0.014f), new Vector4(0.012f, 0.002f, 0.021f, 0.012f),
                new Vector4(0.042f, -0.01f, 0.025f, 0.013f), new Vector4(-0.036f, -0.022f, 0.018f, 0.01f), new Vector4(0.024f, -0.024f, 0.02f, 0.011f),
                new Vector4(0f, 0.014f, 0.016f, 0.01f), new Vector4(0.066f, 0.006f, 0.016f, 0.01f),
            };

            var px = new Color32[n];
            float pix = 1f / W;
            for (int y = 0; y < H; y++)
            {
                float my = ((y + 0.5f) / H - 0.5f) * Aspect;
                for (int x = 0; x < W; x++)
                {
                    float mx = (x + 0.5f) / W - 0.5f;
                    int k = y * W + x;
                    Vector3 c;
                    float ax = Mathf.Abs(mx), ay = Mathf.Abs(my);
                    if (ax > FrameX || ay > FrameY)
                    {
                        c = paper;   // white margin
                        float edge = Mathf.Max(ax - FrameX, ay - FrameY);
                        if (edge < pix * 1.6f) c = Mix(ink, paper, edge / (pix * 1.6f));
                    }
                    else if (my > HeaderY)
                    {
                        c = header;
                        if (my < HeaderY + pix * 2.2f) c = V(Palette.SignalYellow);
                    }
                    else
                    {
                        float ridge = Ridge(mx);
                        if (my > ridge)
                        {
                            // Sky, with two far ranges fading into the haze.
                            float t = Mathf.InverseLerp(0f, HeaderY, my);
                            c = Mix(skyLow, skyTop, t * t);
                            c = Mix(c, Vector3.one, Smooth(0.52f, 0.75f, Fbm(mx * 5f + 9f, my * 14f, 3)) * 0.35f);
                            float f1 = 0.1f + 0.1f * Fbm(mx * 3.1f + 5f, 0.3f, 3), f2 = 0.035f + 0.11f * Fbm(mx * 5.3f + 11f, 0.7f, 3);
                            if (my < f1)
                            {
                                float lit = Fbm(mx * 30f, my * 30f, 2);
                                c = Mix(new Vector3(0.66f, 0.74f, 0.88f), new Vector3(0.93f, 0.95f, 0.99f), Smooth(0.42f, 0.6f, lit) * Smooth(f1 - 0.07f, f1, my));
                            }
                            if (my < f2)
                            {
                                float lit = Fbm(mx * 42f + 3f, my * 42f, 2);
                                c = Mix(new Vector3(0.5f, 0.6f, 0.8f), new Vector3(0.9f, 0.93f, 0.98f), Smooth(0.45f, 0.62f, lit) * Smooth(f2 - 0.06f, f2, my));
                            }
                            float haze = Smooth(0.1f, -0.02f, my);
                            c = Mix(c, skyLow, haze * 0.5f);
                        }
                        else
                        {
                            // The mountain. Light comes from the upper left.
                            float e = Mathf.InverseLerp(-FrameY, ridge, my);
                            float n1 = Fbm(mx * 7f, my * 7f, 4), n2 = Fbm(mx * 7f - 0.04f, my * 7f + 0.04f, 4);
                            float relief = (n1 - n2) * 10f;
                            float ang = Mathf.Atan2(mx - Peak.x, Peak.y + 0.05f - my);
                            float ribPhase = ang * 9f + n1 * 6f;
                            float ribLit = Mathf.Cos(ribPhase) * Smooth(0.15f, 0.7f, e);
                            float flank = Smooth(-0.04f, 0.3f, mx - Peak.x);
                            float light = 0.66f + relief * 0.55f + ribLit * 0.24f - flank * 0.2f;
                            c = Mix(snowShade, snowLit, light);

                            // Rock shows in thin bands where the ribs are steep, mostly high up and on the far flank.
                            float rockN = Fbm(mx * 26f + 3f, my * 9f, 3) + (ribLit < -0.05f ? 0.1f : 0f) + 0.12f * Smooth(0.2f, 0.42f, mx) + 0.1f * Smooth(0.8f, 1f, e);
                            float rock = Smooth(0.5f, 0.8f, e) * Smooth(0.62f, 0.7f, rockN);
                            c = Mix(c, Mix(rockShade, rockLit, light), rock * 0.8f);

                            // Forest below the tree line, in ragged patches, cut open for pistes and the lift.
                            float tl = -0.045f + 0.14f * (Fbm(mx * 4f + 3f, 1.7f, 2) - 0.5f) + 0.07f * ax;
                            float patch = Fbm(mx * 12f + 40f, my * 12f + 7f, 3);
                            float forest = Smooth(tl + 0.03f, tl - 0.025f, my + (patch - 0.5f) * 0.1f);
                            forest *= Smooth(0.3f, 0.42f, patch + 0.35f * Smooth(tl, tl - 0.1f, my));
                            float corridor = 0.013f + 0.006f * Smooth(0.1f, -0.25f, my);
                            forest *= Smooth(corridor, corridor + 0.007f, dPiste[k]);
                            forest *= Smooth(0.0035f, 0.0075f, dLift[k]);
                            forest *= 0.72f + 0.28f * Smooth(0.003f, 0.012f, dOff[k]);
                            float dv = Mathf.Sqrt((mx - Village.x) * (mx - Village.x) * 0.45f + (my - Village.y) * (my - Village.y));
                            forest *= Smooth(0.04f, 0.075f, dv);
                            if (forest > 0.01f)
                            {
                                float st = Mathf.PerlinNoise(mx * 250f + 3f, my * 250f + 8f);      // single trees
                                Vector3 tree = Mix(treeDark, treeLit, 0.2f + light * 0.45f + (st - 0.5f) * 1.7f);
                                tree = Mix(tree, snowLit, Smooth(tl - 0.07f, tl + 0.03f, my) * 0.4f + (st > 0.7f ? 0.12f : 0f));
                                c = Mix(c, tree, forest * Smooth(0.28f, 0.42f, st + forest * 0.5f));
                            }

                            // Village roofs.
                            for (int h = 0; h < houses.Length; h++)
                            {
                                float hx = mx - (Village.x + houses[h].x), hy = my - (Village.y + houses[h].y);
                                float hwid = houses[h].z * 0.5f, hhei = houses[h].w * 0.5f;
                                if (Mathf.Abs(hx) < hwid + pix && Mathf.Abs(hy) < hhei + pix)
                                {
                                    bool border = Mathf.Abs(hx) > hwid - pix || Mathf.Abs(hy) > hhei - pix;
                                    c = border ? ink : hy > 0f ? new Vector3(0.96f, 0.96f, 0.99f) : new Vector3(0.5f, 0.3f, 0.2f);
                                }
                            }
                        }

                        // Printed over everything, sky included.
                        // Lift: a black line with pylon ticks.
                        float dl = dLift[k];
                        float tick = Mathf.Abs(alongLift[k] % 0.045f - 0.0225f) < 0.0016f ? 0.0052f : 0.0016f;
                        c = Mix(ink, c, Smooth(tick - pix, tick + pix * 0.6f, dl));

                        // Off-piste: a dashed orange line.
                        if (alongOff[k] % 0.022f < 0.014f)
                        {
                            c = Mix(ink, c, Smooth(0.0042f - pix, 0.0042f + pix * 0.6f, dOff[k]));
                            c = Mix(offCol, c, Smooth(0.0027f - pix, 0.0027f + pix * 0.6f, dOff[k]));
                        }
                        // Pistes: a coloured line with a dark edge.
                        float dp = dPiste[k];
                        if (dp < 0.008f)
                        {
                            c = Mix(ink, c, Smooth(0.0056f - pix, 0.0056f + pix * 0.6f, dp));
                            c = Mix(runCol[who[k]], c, Smooth(0.004f - pix, 0.004f + pix * 0.6f, dp));
                        }

                        // Legend panel with a soft shadow.
                        float lx = Mathf.Abs(mx - LegendCentre.x) - LegendSize.x * 0.5f, ly = Mathf.Abs(my - LegendCentre.y) - LegendSize.y * 0.5f;
                        float sx = Mathf.Abs(mx - LegendCentre.x - 0.004f) - LegendSize.x * 0.5f, sy = Mathf.Abs(my - LegendCentre.y + 0.004f) - LegendSize.y * 0.5f;
                        if (sx < 0f && sy < 0f) c *= 0.72f;
                        if (lx < 0f && ly < 0f) c = Mathf.Max(lx, ly) > -pix * 1.5f ? ink : paper;
                    }

                    // The sheet itself: fold creases and paper grain.
                    float crease = Mathf.Min(Mathf.Min(Mathf.Abs(mx + 1f / 6f), Mathf.Abs(mx - 1f / 6f)), Mathf.Abs(my));
                    float fold = 1f - 0.1f * Smooth(pix * 2.5f, 0f, crease) + 0.03f * Smooth(pix * 7f, pix * 2.5f, crease);
                    float grain = 0.97f + 0.05f * Mathf.PerlinNoise(mx * 420f, my * 420f);
                    c *= fold * grain;
                    px[k] = new Color32((byte)(Mathf.Clamp01(c.x * 0.995f) * 255f), (byte)(Mathf.Clamp01(c.y * 0.985f) * 255f), (byte)(Mathf.Clamp01(c.z * 0.955f) * 255f), 255);
                }
            }
            _tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "PisteMap", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            _tex.SetPixels32(px);
            _tex.Apply(true, true);
            return _tex;
        }
    }
}
