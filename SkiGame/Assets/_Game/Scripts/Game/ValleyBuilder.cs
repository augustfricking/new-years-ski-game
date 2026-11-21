using System.Collections.Generic;
using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// What lies past the village: the valley the runs come down into. The mountain's flanks carry on
    /// as the valley's sides, the floor falls gently to a frozen lake ringed with forest, and across the
    /// lake rises another mountain, snow over rock, so the view down the runs ends in a landscape and
    /// not in the sky. Coarse meshes and far trees only: it is seen from a distance, through the fog.
    /// </summary>
    public static class ValleyBuilder
    {
        // Distances past the bottom of the run's own snow (the end of the village plaza).
        // (Close enough to see through the fog, which thickens past a few hundred metres.)
        const float LakeZ = 340f, LakeHalfZ = 150f, LakeHalfX = 320f;
        const float FloorDrop = 18f;                    // the valley floor falls this far to the lake
        const float MountainFoot = 540f, MountainTop = 800f;
        // The far mountain's peaks: offset across the valley, height, half-width (metres).
        static readonly Vector3[] Peaks =
        {
            new Vector3(-760f, 250f, 300f), new Vector3(-300f, 330f, 230f), new Vector3(120f, 210f, 260f),
            new Vector3(520f, 300f, 240f), new Vector3(980f, 240f, 320f),
        };
        const float Length = 1300f, HalfWidth = 1500f;

        public static void Build(PisteCourse c, Transform parent)
        {
            var root = new GameObject("Valley").transform;
            root.SetParent(parent, false);
            float bottom = c.MaxZ, cx = c.VillageX;
            float hB = c.SnowHeight(cx, bottom) - 0.6f;
            float lake = hB - FloorDrop - 1.5f;

            float Height(float x, float z)
            {
                float d = z - bottom, across = Mathf.Abs(x - cx);
                float h = hB - FloorDrop * M.Smooth(Mathf.Clamp01(d / (LakeZ - LakeHalfZ)));
                // The valley's sides: the mountain's flanks carry on, rising as they do beside the runs.
                h += Mathf.Max(0f, across - 120f) * 0.11f * M.Smooth(Mathf.Clamp01(across / 400f))
                   + 22f * (Mathf.PerlinNoise(x / 160f + 3f, z / 160f + 9f) - 0.45f) * M.Smooth(Mathf.Clamp01((across - 80f) / 200f));
                // The lake lies in a hollow; the mountain beyond climbs out of it.
                float u = (x - cx) / LakeHalfX, v = (d - LakeZ) / LakeHalfZ, r = u * u + v * v;
                if (r < 1.25f) h = Mathf.Lerp(h, lake - 2f, M.Smooth(Mathf.Clamp01((1.25f - r) / 0.3f)));
                // The far mountain: a row of peaks with saddles between them, rising behind the lake.
                float rise = M.Smooth(Mathf.Clamp01((d - MountainFoot) / (MountainTop - MountainFoot)));
                float peaks = 60f;
                foreach (var p in Peaks)
                {
                    float t = (x - cx - p.x) / p.z;
                    peaks = Mathf.Max(peaks, p.y * Mathf.Exp(-t * t * 2.2f));
                }
                peaks *= 0.85f + 0.3f * Mathf.PerlinNoise(x / 70f + 2f, z / 70f);
                h += rise * peaks;
                return h;
            }
            Color Paint(float x, float z, float h)
            {
                float n = Mathf.PerlinNoise(x / 37f + 5f, z / 37f + 2f), n2 = Mathf.PerlinNoise(x / 9f, z / 9f);
                Color snow = Palette.Mix(Palette.Snow, Palette.SnowShadow, 0.1f + n2 * 0.15f);
                Color forest = Palette.Mix(Palette.PineDark, Palette.Pine, n * 0.6f + n2 * 0.3f);
                Color rock = Palette.Mix(Palette.Rock, Palette.RockDark, n2);
                float above = h - lake;
                // Low down: forest in belts; higher on the far mountain: rock, then snow on top.
                Color col = Forested(x, z) ? forest : snow;
                if (above > 120f)
                {
                    float rocky = M.Smooth(Mathf.Clamp01((above - 120f) / 90f)) * (n > 0.45f ? 1f : 0.35f);
                    col = Palette.Mix(col, rock, rocky);
                    col = Palette.Mix(col, snow, M.Smooth(Mathf.Clamp01((above - 260f) / 80f + (n2 - 0.5f) * 0.6f)));
                }
                return col;
            }
            bool Forested(float x, float z)
            {
                float d = z - bottom, across = Mathf.Abs(x - cx);
                if (d < 60f && across < 260f) return false;                                     // open fields below the village
                float u = (x - cx) / (LakeHalfX + 25f), v = (d - LakeZ) / (LakeHalfZ + 25f);
                if (u * u + v * v < 1f) return false;                                          // the lake and its shore
                return Mathf.PerlinNoise(x / 90f + 13f, z / 90f + 7f) > 0.42f;
            }

            // The ground: rows down the valley, columns across it (finer near the middle).
            var zs = new List<float>();
            for (float d = -24f; d <= Length; d += d < 200f ? 20f : 40f) zs.Add(bottom + d);
            var xs = new List<float>();
            for (float a = -HalfWidth; a <= HalfWidth; a += Mathf.Abs(a) < 500f ? 40f : 100f) xs.Add(cx + a);
            int rows = zs.Count, cols = xs.Count;
            var vtx = new Vector3[rows * cols];
            var colr = new Color32[rows * cols];
            for (int r = 0; r < rows; r++)
                for (int k = 0; k < cols; k++)
                {
                    float x = xs[k], z = zs[r];
                    float h = Height(x, z);
                    if (z < bottom + 2f) h = Mathf.Min(h, c.SnowHeight(Mathf.Clamp(x, cx - 60f, cx + 60f), Mathf.Min(z, bottom)) - 0.4f);   // tucked under the plaza's snow
                    vtx[r * cols + k] = new Vector3(x, h, z);
                    colr[r * cols + k] = Paint(x, z, h);
                }
            MountainBuilder.AddGrid(root, "Valley floor", vtx, colr, rows, cols, true);

            // The frozen lake: a flat oval of ice, lighter in the middle, with snow blown across it.
            {
                var b = new MeshBuilder();
                const int ring = 48, bands = 5;
                var lakeC = new Vector3(cx, lake, bottom + LakeZ);
                for (int band = 0; band < bands; band++)
                {
                    float r0 = band / (float)bands, r1 = (band + 1) / (float)bands;
                    for (int i = 0; i < ring; i++)
                    {
                        float a0 = i / (float)ring * Mathf.PI * 2f, a1 = (i + 1) / (float)ring * Mathf.PI * 2f;
                        Vector3 P(float rr, float aa) => lakeC + new Vector3(Mathf.Cos(aa) * LakeHalfX * rr, 0f, Mathf.Sin(aa) * LakeHalfZ * rr);
                        // Dark clear ice in the middle, paler toward the shore, a dark rim where it meets the snow.
                        Color ice = Palette.Mix(Palette.IceDark, Palette.Ice, r1 * r1);
                        if (band == bands - 1) ice = Palette.Mix(ice, Palette.Ink, 0.2f);
                        b.Col = ice;
                        b.Tri(P(r0, a0), P(r1, a1), P(r1, a0));                 // facing up
                        if (band > 0) b.Tri(P(r0, a0), P(r0, a1), P(r1, a1));
                    }
                }
                b.ToObject("Frozen lake", Mats.PropsNoOutline, root, false, false);
            }

            // Far trees in the forest belts (and none on the ice, the open fields or high on the mountain).
            var builders = new Dictionary<int, MeshBuilder>();
            const float cell = 26f;
            int placed = 0;
            for (float d = 40f; d < MountainFoot + 120f; d += cell)
                for (float a = -900f; a <= 900f; a += cell)
                {
                    int iz = Mathf.RoundToInt(d / cell), ix = Mathf.RoundToInt(a / cell);
                    if (Hash(ix, iz) < 0.35f) continue;
                    float x = cx + a + (Hash(iz, ix + 7) - 0.5f) * cell * 0.8f, z = bottom + d + (Hash(ix + 3, iz) - 0.5f) * cell * 0.8f;
                    if (!Forested(x, z)) continue;
                    float h = Height(x, z);
                    if (h - lake > 140f) continue;
                    int key = Mathf.FloorToInt(d / 300f) * 4 + Mathf.FloorToInt((a + 900f) / 600f);
                    if (!builders.TryGetValue(key, out var b)) builders[key] = b = new MeshBuilder();
                    float s = 2.4f + Hash(ix * 5, iz * 3) * 1.6f;
                    b.Push(new Vector3(x, h - 0.5f, z), Quaternion.Euler(0f, Hash(ix, iz * 2) * 360f, 0f), Vector3.one * s);
                    Props.PineFar(b, (ix + iz) & 1);
                    b.Pop();
                    placed++;
                }
            foreach (var kv in builders) kv.Value.ToObject("Valley trees " + kv.Key, Mats.Trees, root, false, false);
        }

        static float Hash(int a, int b) { unchecked { uint h = (uint)(a * 374761393 + b * 668265263 + 977); h = (h ^ (h >> 13)) * 1274126177u; return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000; } }
    }
}
