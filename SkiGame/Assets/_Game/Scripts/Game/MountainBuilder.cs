using System.Collections.Generic;
using Ski.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ski.Game
{
    /// <summary>
    /// The rest of the mountain around a run: the forested flanks with the four other runs cut
    /// through them (so that from the village you look back up at the whole ski area), the summit
    /// above the start, the chairlift that climbs beside the piste, and old ski tracks in the
    /// off-piste forest.
    /// </summary>
    public static class MountainBuilder
    {
        const float SkirtEdge = 13f + 75f;   // TerrainBuilder's snow reaches this far beyond the piste edge
        const float LiftOffset = 11f, CableHeight = 9.5f, CableHalf = 2.1f;

        public static void Build(PisteCourse c, Transform parent)
        {
            var root = new GameObject("Mountain").transform;
            root.SetParent(parent, false);
            BuildFlanks(c, root);
            if (c is OffPisteCourse off) BuildTracks(off, root);
            BuildLift(c, root);
        }

        // ---- the lift ----------------------------------------------------------------------------
        // On the piste map the lift climbs up the middle, between Parken and Slalombacken: skiing
        // Parken it is on your left, skiing Slalombacken on your right. From the other runs only its
        // bottom station in the village is seen, with the line leaving into the forest behind it.

        const float StubLength = 240f;

        public static bool LiftAlongside(PisteCourse c) => c.Id == SlopeId.Blue || c.Id == SlopeId.Red;

        public static float LiftSide(PisteCourse c) => c.Id == SlopeId.Red ? 1f : -1f;

        /// <summary>Where the lift line runs at a given z (far away where there is none).</summary>
        public static float LiftX(PisteCourse c, float z)
        {
            float station = c.LiftZone.X + 5f, z1 = c.LiftZone.Z - 4f;
            if (!LiftAlongside(c))
            {
                if (z < z1 - StubLength || z > z1 + 1f) return 1e6f;
                float x = station - (z1 - z) * 0.55f;
                if (z < c.FinishZ + 10f) x = Mathf.Min(x, c.CenterX(Mathf.Min(z, c.FinishZ)) - c.HalfWidth(Mathf.Min(z, c.FinishZ)) - 26f);
                return x;
            }
            float bendZ = c.FinishZ - 70f;
            float zz = Mathf.Clamp(z, 0f, bendZ);
            float beside = c.CenterX(zz) + LiftSide(c) * (c.HalfWidth(zz) + LiftOffset);
            if (z <= bendZ) return beside;
            // Near the bottom it swings across to the station (over the piste, on Slalombacken).
            return Mathf.Lerp(beside, station, M.Smooth(Mathf.InverseLerp(bendZ, c.LiftZone.Z - 12f, z)));
        }

        /// <summary>Whether a point is on the run or the village plaza, where no pylon may stand.</summary>
        static bool OnTheRun(PisteCourse c, float x, float z)
        {
            if (z < c.MinZ || z > c.FinishZ + Course.PlazaLength) return false;
            float zz = Mathf.Min(z, c.FinishZ);
            float half = z <= c.FinishZ ? c.HalfWidth(zz) : Course.PlazaHalfWidthAt(z - c.FinishZ, c.HalfWidth(c.FinishZ));
            return Mathf.Abs(x - c.CenterX(zz)) < half + 4f;
        }

        static void BuildLift(PisteCourse c, Transform parent)
        {
            bool along = LiftAlongside(c);
            float z1 = c.LiftZone.Z - 4f, z0 = along ? -26f : z1 - StubLength;
            var steel = new MeshBuilder();
            var pts = new List<Vector3>();
            int pylons = Mathf.Max(2, Mathf.RoundToInt((z1 - z0) / 58f));
            for (int i = 0; i <= pylons; i++)
            {
                float z = Mathf.Lerp(z0, z1, i / (float)pylons);
                float x = LiftX(c, z);
                if (i > 0 && i < pylons && OnTheRun(c, x, z)) continue;   // the cable spans over the piste
                float ground = c.SnowHeight(x, z);
                steel.Push(new Vector3(x, ground - 0.3f, z));
                Props.Pylon(steel, CableHeight + 0.3f, CableHalf);
                steel.Pop();
                pts.Add(new Vector3(x, ground + CableHeight, z));
            }
            // The two cables: up on the piste side, down on the forest side.
            float side = LiftSide(c);
            steel.Col = Palette.Ink;
            for (int i = 0; i < pts.Count - 1; i++)
                for (int k = -1; k <= 1; k += 2)
                    steel.Tube(pts[i] + Vector3.right * k * CableHalf, pts[i + 1] + Vector3.right * k * CableHalf, 0.035f, 0.035f, 4, false);
            // Stations: a drive house at the top, a return wheel at the bottom.
            foreach (int end in new[] { 0, pts.Count - 1 })
            {
                if (end == 0 && !along) continue;   // the line goes on, out of sight
                Vector3 p = pts[end];
                steel.Col = Palette.SteelDark;
                steel.Box(p + new Vector3(0f, 0.2f, 0f), new Vector3(CableHalf * 2f + 1.6f, 0.5f, 3.4f));
                steel.Col = Palette.SignalYellow;
                steel.Box(p + new Vector3(0f, 0.55f, 0f), new Vector3(CableHalf * 2f + 1.2f, 0.2f, 3f));
            }
            steel.ToObject("Lift line", Mats.Props, parent, true);

            // Chairs, running round the loop: up the line nearer the piste, down the other.
            var cb = new MeshBuilder();
            Props.Chair(cb);
            var chairMesh = cb.Build("Lift chair");
            var rng = new Rng(c.Seed * 7 + 3);
            var lift = new GameObject("Lift chairs").AddComponent<LiftChairs>();
            lift.transform.SetParent(parent, false);
            var chairs = lift.Setup(pts, chairMesh, Mats.Props, side, CableHalf, 19f);
            // A few people riding up, for life. They sit still: no per-frame cost of their own.
            int riderCount = 0;
            for (int j = 0; j < chairs.Count && riderCount < 12; j++)
            {
                if (!rng.Chance(0.3f)) continue;
                int seats = rng.Range(1, 4);
                for (int s = 0; s < seats; s++)
                {
                    var person = Person.Create(PersonLook.Random(rng, false), chairs[j], "Rider");
                    person.transform.localPosition = new Vector3(0.86f - (s + rng.Range(0, 2)) * 0.573f, -3.07f, -0.02f);
                    person.transform.localRotation = Quaternion.identity;
                    person.Poles = PoleMode.Lap;
                    person.Alive = false;
                    person.Snap(Pose.Sit);
                    person.enabled = false;
                    lift.Seat(j, person.gameObject);
                    riderCount++;
                }
            }
        }

        // ---- the flanks: forest and the other runs ---------------------------------------------

        static float Hash(int a, int b) { unchecked { uint h = (uint)(a * 374761393 + b * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000; } }

        static void BuildFlanks(PisteCourse c, Transform parent)
        {
            int me = (int)c.Id;
            float top = c.MinZ, bottom = c.MaxZ;
            float len = Mathf.Max(c.FinishZ, 200f);
            // Lateral offsets of the other four runs: they fan out from the top station and come
            // together again at the village, in the order they have on the piste map.
            var bandOff = new List<float>(); var bandWidth = new List<float>(); var bandBroken = new List<bool>();
            for (int j = 0; j < 5; j++)
            {
                if (j == me) continue;
                bandOff.Add((me - j) * 150f);   // runs to the left on the map are on your right, skiing down
                bandWidth.Add(j == (int)SlopeId.OffPiste ? 6f : j == (int)SlopeId.Blue ? 22f : 15f);
                bandBroken.Add(j == (int)SlopeId.OffPiste);
            }
            float Fan(float z) => Mathf.Pow(Mathf.Clamp01(Mathf.Sin(Mathf.PI * Mathf.Clamp01((z + 30f) / (len + 90f)))), 0.6f);

            float EdgeX(float z, int side)
            {
                float zz = Mathf.Clamp(z, top, bottom);
                return c.CenterX(zz) + side * (c.HalfWidth(zz) + SkirtEdge - 6f);
            }
            float Ground(float x, float z, int side, float beyond)
            {
                float zz = Mathf.Clamp(z, top, bottom);
                float h = c.SnowHeight(EdgeX(zz, side), zz) - 0.8f;
                if (z < top) h += (top - z) * 0.42f;                     // the summit keeps rising above the start
                if (z > bottom) h -= (z - bottom) * 0.02f;
                h += beyond * 0.11f + 26f * (Mathf.PerlinNoise(x / 140f + 7f, z / 140f + 3f) - 0.45f) * M.Smooth(beyond / 110f);
                return h;
            }
            Color Paint(float x, float z, float beyond, float cx)
            {
                float n = Mathf.PerlinNoise(x / 23f + 11f, z / 23f + 5f), n2 = Mathf.PerlinNoise(x / 6f, z / 6f);
                Color forest = Palette.Mix(Palette.PineDark, Palette.Pine, n * 0.6f + n2 * 0.3f);   // the dark of a forest floor seen between crowns
                // Above the start: bare alpine snow and rock.
                float alpine = M.Smooth((top + 30f - z) / 90f + (n - 0.5f) * 0.6f);
                Color snow = Palette.Mix(Palette.Snow, Palette.SnowShadow, 0.12f + n2 * 0.12f);
                Color rock = Palette.Mix(Palette.Rock, Palette.RockDark, n2);
                Color high = n > 0.62f ? Palette.Mix(snow, rock, M.Smooth((n - 0.62f) * 8f)) : snow;
                Color col = Palette.Mix(forest, high, alpine);
                // The other runs: white ribbons through the trees.
                float fan = Fan(z);
                for (int b = 0; b < bandOff.Count; b++)
                {
                    float centre = cx + bandOff[b] * fan + 22f * Mathf.Sin(z / 130f + b * 2.1f) * fan;
                    float d = Mathf.Abs(x - centre) - bandWidth[b] * (1f + 0.5f * fan);
                    if (d > 5f) continue;
                    if (bandBroken[b] && Mathf.PerlinNoise(z / 35f + b, 0.5f) < 0.5f) continue;   // off-piste: glades, not a road
                    col = Palette.Mix(Palette.Snow, col, M.Smooth(d / 5f));
                }
                // Village lights and roofs do not belong up here; keep the valley floor white.
                if (z > c.FinishZ + 40f) col = Palette.Mix(col, snow, M.Smooth((z - c.FinishZ - 40f) / 60f));
                return col;
            }

            // Columns: fine near the piste (so the ribbons keep their shape), coarse far out.
            var offs = new List<float>();
            for (float d = 0f; d <= 700f; d += d < 460f ? 11f : 60f) offs.Add(d);
            var zs = new List<float>();
            for (float z = top - 260f; z <= bottom + 140f; z += 15f) zs.Add(z);

            for (int side = -1; side <= 1; side += 2)
            {
                int cols = offs.Count, rows = zs.Count;
                var v = new Vector3[rows * cols]; var col = new Color32[rows * cols];
                for (int r = 0; r < rows; r++)
                {
                    float z = zs[r];
                    float cx = c.CenterX(Mathf.Clamp(z, top, bottom));
                    float ex = EdgeX(z, side);
                    for (int k = 0; k < cols; k++)
                    {
                        float x = ex + side * offs[k];
                        v[r * cols + k] = new Vector3(x, Ground(x, z, side, offs[k]), z);
                        col[r * cols + k] = Paint(x, z, offs[k], cx);
                    }
                }
                AddGrid(parent, "Flank " + (side < 0 ? "L" : "R"), v, col, rows, cols, side > 0);
            }
            // The summit behind the start gate, between the two flanks.
            {
                // It starts exactly where the piste ends, at the piste's own height, so there is no
                // gap to see the sky through when you look back up from the start.
                int cols = 16, rows = 0;
                var rowZ = new List<float>();
                for (float z = top - 260f; z < top - 20f; z += 15f) { rowZ.Add(z); rows++; }
                foreach (float z in new[] { top - 20f, top - 12f, top - 6f, top - 2f, top + 2f }) { rowZ.Add(z); rows++; }
                var v = new Vector3[rows * cols]; var col = new Color32[rows * cols];
                float pisteL = c.CenterX(top) - c.HalfWidth(top), pisteR = c.CenterX(top) + c.HalfWidth(top);
                for (int r = 0; r < rows; r++)
                {
                    float z = rowZ[r];
                    float xl = EdgeX(z, -1), xr = EdgeX(z, 1), cx = c.CenterX(top);
                    for (int k = 0; k < cols; k++)
                    {
                        float t = k / (float)(cols - 1);
                        float x = Mathf.Lerp(xl, xr, t);
                        float hl = Ground(xl, z, -1, 0f), hr = Ground(xr, z, 1, 0f);
                        float edgeY = Mathf.Lerp(hl, hr, t);
                        float centreY = c.SnowHeight(Mathf.Clamp(x, pisteL, pisteR), top) - 0.25f + Mathf.Max(0f, top - z) * 0.42f;
                        float y = Mathf.Lerp(centreY, edgeY, Mathf.Pow(Mathf.Abs(t - 0.5f) * 2f, 3f)) + 5f * Mathf.Sin(t * Mathf.PI) * M.Smooth((top - z) / 60f);
                        v[r * cols + k] = new Vector3(x, y, z);
                        col[r * cols + k] = Paint(x, z, 0f, cx);
                    }
                }
                AddGrid(parent, "Summit", v, col, rows, cols, true);
            }

            // Far trees standing on the flanks, so the forest has an edge against the sky.
            var builders = new Dictionary<int, MeshBuilder>();
            const float cell = 15f;
            for (int side = -1; side <= 1; side += 2)
                for (float z = top - 10f; z < bottom + 40f; z += cell)
                    for (float d = -56f; d < 330f; d += cell)
                    {
                        int iz = Mathf.RoundToInt(z / cell), id = Mathf.RoundToInt(d / cell) * side;
                        float jx = (Hash(iz, id) - 0.5f) * cell * 0.8f, jz = (Hash(id, iz + 99) - 0.5f) * cell * 0.8f;
                        float zz = z + jz, beyond = d + jx;
                        float cx = c.CenterX(Mathf.Clamp(zz, top, bottom));
                        float x = EdgeX(zz, side) + side * beyond;
                        bool skirt = beyond < 2f;   // on the run's own snow, behind its near trees
                        if (skirt && (zz < top + 4f || zz > c.FinishZ - 6f || Mathf.Abs(x - LiftX(c, zz)) < 6f)) continue;
                        Color32 here = Paint(x, zz, Mathf.Max(beyond, 0f), cx);
                        if (here.g < here.r + 12 || here.r > 120) continue;   // only where the ground is forest
                        int key = Mathf.FloorToInt(zz / 220f) * 2 + (side > 0 ? 1 : 0);
                        if (!builders.TryGetValue(key, out var b)) builders[key] = b = new MeshBuilder();
                        float s = (skirt ? 1.5f : 2.2f) + Hash(iz * 3, id * 5) * 1.5f;
                        float y = skirt ? c.SnowHeight(x, Mathf.Clamp(zz, top, bottom)) : Ground(x, zz, side, beyond);
                        b.Push(new Vector3(x, y - 0.5f, zz), Quaternion.Euler(0f, Hash(id, iz) * 360f, 0f), Vector3.one * s);
                        Props.PineFar(b, (iz + id) & 1);
                        b.Pop();
                    }
            foreach (var kv in builders) kv.Value.ToObject("Flank trees " + kv.Key, Mats.Trees, parent, false, false);
        }

        static void AddGrid(Transform parent, string name, Vector3[] v, Color32[] col, int rows, int cols, bool flip)
        {
            var nrm = new Vector3[v.Length];
            for (int r = 0; r < rows; r++)
                for (int k = 0; k < cols; k++)
                {
                    Vector3 a = v[r * cols + Mathf.Max(k - 1, 0)], b = v[r * cols + Mathf.Min(k + 1, cols - 1)];
                    Vector3 d = v[Mathf.Max(r - 1, 0) * cols + k], e = v[Mathf.Min(r + 1, rows - 1) * cols + k];
                    Vector3 n = Vector3.Cross(e - d, b - a).normalized;
                    nrm[r * cols + k] = n.y < 0f ? -n : n;
                }
            var tris = new int[(rows - 1) * (cols - 1) * 6];
            int t = 0;
            for (int r = 0; r < rows - 1; r++)
                for (int k = 0; k < cols - 1; k++)
                {
                    int a = r * cols + k, b = a + cols;
                    if (flip) { tris[t++] = a; tris[t++] = b; tris[t++] = b + 1; tris[t++] = a; tris[t++] = b + 1; tris[t++] = a + 1; }
                    else { tris[t++] = a; tris[t++] = b + 1; tris[t++] = b; tris[t++] = a; tris[t++] = a + 1; tris[t++] = b + 1; }
                }
            var mesh = new Mesh { name = name, indexFormat = v.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = v; mesh.normals = nrm; mesh.colors32 = col; mesh.triangles = tris;
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.PropsNoOutline;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---- ski tracks in the off-piste forest --------------------------------------------------

        /// <summary>
        /// Others have skied here before you. Their tracks follow the three ways through; a few
        /// also wander into dead ends and stop, so a track is a hint, not a promise.
        /// </summary>
        static void BuildTracks(OffPisteCourse c, Transform parent)
        {
            var b = new MeshBuilder();
            b.Col = Palette.Mix(Palette.SnowOffPiste, Palette.SnowShadow, 0.55f);
            var rng = new Rng(c.Seed * 13 + 5);
            float end = c.FinishZ - 4f;
            for (int route = 0; route < OffPisteCourse.RouteNames.Length; route++)
            {
                int skiers = route == c.PreferredRoute ? 3 : 2;
                for (int s = 0; s < skiers; s++)
                {
                    float lateral = rng.Range(-1.3f, 1.3f), phase = rng.Range(0f, 6f), wave = rng.Range(7f, 12f), amp = rng.Range(0.35f, 0.9f);
                    // Tracks are snowed over in places: draw them in stretches.
                    float z = 14f + rng.Range(0f, 20f);
                    while (z < end)
                    {
                        float stretch = rng.Range(45f, 120f);
                        Ribbon(b, c, zz => c.RouteX(route, zz) + lateral + Mathf.Sin(zz / wave + phase) * amp, z, Mathf.Min(z + stretch, end));
                        z += stretch + rng.Range(10f, 40f);
                    }
                }
            }
            // A lone track into every other dead-end branch.
            int n = 0;
            foreach (var seg in c.Segments)
            {
                if (seg.Route >= 0 || (n++ & 1) == 1) continue;
                Vector2 a = seg.A, e = seg.B;
                if (e.y - a.y < 8f) continue;
                float off = rng.Range(-0.8f, 0.8f);
                Ribbon(b, c, zz => Mathf.Lerp(a.x, e.x, Mathf.InverseLerp(a.y, e.y, zz)) + off + Mathf.Sin(zz / 9f) * 0.5f, a.y + 2f, e.y - 2f);
            }
            if (b.VertexCount > 0) b.ToObject("Ski tracks", Mats.PropsNoOutline, parent, false, false);
        }

        /// <summary>One pair of ski grooves from z0 to z1 along x = path(z).</summary>
        static void Ribbon(MeshBuilder b, OffPisteCourse c, System.Func<float, float> path, float z0, float z1)
        {
            const float step = 1.6f, half = 0.045f, apart = 0.17f, lift = 0.035f;
            for (int ski = -1; ski <= 1; ski += 2)
            {
                Vector3 prevL = default, prevR = default;
                bool has = false;
                for (float z = z0; z <= z1; z += step)
                {
                    float x = path(z) + ski * apart;
                    float dx = path(z + 0.5f) - path(z - 0.5f);
                    Vector3 across = new Vector3(1f, 0f, -dx).normalized * half;
                    Vector3 p = new Vector3(x, c.SnowHeight(x, z) + lift, z);
                    Vector3 l = p - across, r = p + across;
                    if (has) b.Quad(prevL, l, r, prevR);
                    prevL = l; prevR = r; has = true;
                }
            }
        }
    }
}
