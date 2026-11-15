using System.Collections.Generic;
using Ski.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ski.Game
{
    /// <summary>
    /// Turns a <see cref="PisteCourse"/> into scene objects: the snow surface, the forest, park
    /// features, slalom gates, arches and markers. The village is added by <see cref="VillageBuilder"/>.
    /// </summary>
    public static class TerrainBuilder
    {
        const int RowsPerChunk = 72;

        public static GameObject Build(PisteCourse c, RunSession session, Transform parent)
        {
            var root = new GameObject("Course " + c.Name);
            root.transform.SetParent(parent, false);
            BuildSnow(c, root.transform);
            if (c is OffPisteCourse op) BuildOffPisteForest(op, root.transform);
            else BuildPisteForest(c, root.transform);
            BuildFeatureProps(c, root.transform);
            BuildMarkers(c, session, root.transform);
            return root;
        }

        // ---- snow ------------------------------------------------------------------------

        static void BuildSnow(PisteCourse c, Transform parent)
        {
            bool off = c is OffPisteCourse;
            float step = off ? 2.2f : 1.5f;

            // Rows: regular spacing plus the sharp edges of features.
            var zs = new List<float>();
            for (float z = c.MinZ; z <= c.MaxZ; z += step) zs.Add(z);
            var extra = new List<float>();
            foreach (var f in c.Features) if (!(f is BoxFeature)) f.Breakpoints(extra);
            foreach (float e in extra) if (e > c.MinZ && e < c.MaxZ) zs.Add(e);
            zs.Sort();
            for (int i = zs.Count - 1; i > 0; i--) if (zs[i] - zs[i - 1] < 0.12f) zs.RemoveAt(i);

            // Columns: across the piste and its banks, plus a far skirt into the forest.
            float maxHw = 0f;
            for (float z = 0f; z < c.FinishZ; z += 10f) maxHw = Mathf.Max(maxHw, c.HalfWidth(z));
            int inner = off ? Mathf.CeilToInt((c.MaxX - c.MinX) / step) : Mathf.CeilToInt(2f * (maxHw + 13f) / step);
            int cols = inner + 1 + (off ? 0 : 4);
            int rows = zs.Count;

            var pos = new Vector3[rows * cols];
            var col = new Color32[rows * cols];
            Color32 piste = Palette.Snow, wild = Palette.SnowOffPiste, lip = Palette.Mix(Palette.SignalBlue, Palette.Snow, 0.35f);
            for (int r = 0; r < rows; r++)
            {
                float z = zs[r];
                float cx = c.CenterX(z), hw = c.HalfWidth(z);
                for (int k = 0; k < cols; k++)
                {
                    float x;
                    if (off) x = c.MinX + k * step;
                    else
                    {
                        float span = hw + 13f;
                        int j = k - 2;
                        if (k == 0) x = cx - span - 75f;
                        else if (k == 1) x = cx - span - 22f;
                        else if (k == cols - 2) x = cx + span + 22f;
                        else if (k == cols - 1) x = cx + span + 75f;
                        else x = cx - span + j / (float)inner * 2f * span;
                    }
                    int i = r * cols + k;
                    pos[i] = new Vector3(x, c.SnowHeight(x, z), z);
                    float edge = Mathf.Abs(x - cx) - hw;
                    bool groomed = !off && edge < -0.6f || off && z > c.FinishZ + 12f;
                    Color32 cc = groomed ? piste : wild;
                    cc.a = (byte)(groomed ? 255 : 0);
                    col[i] = cc;
                }
            }
            // Paint the lips of the jumps so they read from a distance.
            foreach (var f in c.Features)
            {
                if (!(f is Kicker kicker)) continue;
                for (int r = 0; r < rows; r++)
                {
                    if (Mathf.Abs(zs[r] - (kicker.LipZ - 0.02f)) > 0.05f) continue;
                    for (int k = 0; k < cols; k++)
                        if (Mathf.Abs(pos[r * cols + k].x - kicker.X) < kicker.Width * 0.5f) col[r * cols + k] = lip;
                }
            }

            // Normals from the grid.
            var nrm = new Vector3[rows * cols];
            for (int r = 0; r < rows; r++)
                for (int k = 0; k < cols; k++)
                {
                    Vector3 a = pos[r * cols + Mathf.Max(k - 1, 0)], b = pos[r * cols + Mathf.Min(k + 1, cols - 1)];
                    Vector3 d = pos[Mathf.Max(r - 1, 0) * cols + k], e = pos[Mathf.Min(r + 1, rows - 1) * cols + k];
                    nrm[r * cols + k] = Vector3.Cross(e - d, b - a).normalized;
                }

            for (int r0 = 0; r0 < rows - 1; r0 += RowsPerChunk)
            {
                int r1 = Mathf.Min(r0 + RowsPerChunk, rows - 1);
                int n = (r1 - r0 + 1) * cols;
                var v = new Vector3[n]; var nn = new Vector3[n]; var cc = new Color32[n];
                System.Array.Copy(pos, r0 * cols, v, 0, n);
                System.Array.Copy(nrm, r0 * cols, nn, 0, n);
                System.Array.Copy(col, r0 * cols, cc, 0, n);
                var tris = new int[(r1 - r0) * (cols - 1) * 6];
                int t = 0;
                for (int r = 0; r < r1 - r0; r++)
                    for (int k = 0; k < cols - 1; k++)
                    {
                        int a = r * cols + k, b = a + cols;
                        tris[t++] = a; tris[t++] = b; tris[t++] = b + 1;
                        tris[t++] = a; tris[t++] = b + 1; tris[t++] = a + 1;
                    }
                var mesh = new Mesh { name = "Snow " + r0, indexFormat = n > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.vertices = v; mesh.normals = nn; mesh.colors32 = cc; mesh.triangles = tris;
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                var go = new GameObject(mesh.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Mats.Snow;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // ---- forest ----------------------------------------------------------------------

        static void BuildPisteForest(PisteCourse c, Transform parent)
        {
            var rng = new Rng(c.Seed * 31 + 7);
            const float chunkLen = 90f;
            MeshBuilder b = null;
            float chunkStart = c.MinZ;
            int chunk = 0;
            float endZ = c.MaxZ - 4f;
            for (float z = c.MinZ + 2f; z < endZ; z += 3.1f)
            {
                if (b == null || z - chunkStart > chunkLen)
                {
                    if (b != null && b.VertexCount > 0) b.ToObject("Forest " + chunk++, Mats.Trees, parent, true, false);
                    b = new MeshBuilder();
                    chunkStart = z;
                }
                for (int side = -1; side <= 1; side += 2)
                    for (int row = 0; row < 7; row++)
                    {
                        float jx = rng.Range(-1.3f, 1.3f), jz = rng.Range(-1.4f, 1.4f), scale = rng.Range(0.8f, 1.55f);
                        int variant = rng.Range(0, 3);
                        float keep = rng.Value(), yaw = rng.Range(0f, 360f);
                        if (keep > 1f - row * 0.1f) continue;
                        float zz = z + jz;
                        float x = c.CenterX(zz) + side * (c.HalfWidth(zz) + 2.6f + row * 3.6f + jx);
                        b.Push(new Vector3(x, c.SnowHeight(x, zz) - 0.15f, zz), Quaternion.Euler(0, yaw, 0), Vector3.one * (scale * (1f + row * 0.05f)));
                        Props.Pine(b, variant, rng);
                        b.Pop();
                    }
            }
            if (b != null && b.VertexCount > 0) b.ToObject("Forest " + chunk, Mats.Trees, parent, true, false);
        }

        static void BuildOffPisteForest(OffPisteCourse c, Transform parent)
        {
            // Chunk the obstacles on a grid so the camera only draws what is near.
            const float cell = 64f;
            var trees = new Dictionary<Vector2Int, MeshBuilder>();
            var farTrees = new Dictionary<Vector2Int, MeshBuilder>();
            var props = new Dictionary<Vector2Int, MeshBuilder>();
            var rng = new Rng(c.Seed + 99);
            foreach (var o in c.Obstacles)
            {
                if (o.IsBox) continue;
                bool isTree = o.Kind == ObstacleKind.Tree;
                // Trees more than a few metres into the forest are never seen up close.
                bool far = isTree && c.Clear(o.X, o.Z) < -4.5f;
                var dict = far ? farTrees : isTree ? trees : props;
                var key = new Vector2Int(Mathf.FloorToInt(o.X / cell), Mathf.FloorToInt(o.Z / cell));
                if (!dict.TryGetValue(key, out var b)) dict[key] = b = new MeshBuilder();
                float s = o.Scale;
                b.Push(new Vector3(o.X, c.SnowHeight(o.X, o.Z) - 0.12f, o.Z), Quaternion.Euler(0, rng.Range(0f, 360f), 0), Vector3.one * s);
                if (far) Props.PineFar(b, o.Variant); else Props.Obstacle(b, o, rng);
                b.Pop();
            }
            foreach (var kv in trees) kv.Value.ToObject("Trees " + kv.Key, Mats.Trees, parent, true, false);
            foreach (var kv in farTrees) kv.Value.ToObject("Far trees " + kv.Key, Mats.Trees, parent, false, false);
            foreach (var kv in props) kv.Value.ToObject("Rocks " + kv.Key, Mats.Props, parent, true, true);
        }

        // ---- park features, gates, arches ------------------------------------------------

        static void BuildFeatureProps(PisteCourse c, Transform parent)
        {
            var b = new MeshBuilder();
            Color[] colours = { Palette.SignalYellow, Palette.Jackets[1], Palette.Jackets[0], Palette.SafetyOrange, Palette.Jackets[3] };
            int n = 0;
            foreach (var f in c.Features)
            {
                if (f is BoxFeature box)
                {
                    float y0 = c.SnowHeight(box.X, box.ZStart), y1 = c.SnowHeight(box.X, box.ZStart + box.Length);
                    Vector3 dir = new Vector3(0f, y1 - y0, box.Length).normalized;
                    b.Push(new Vector3(box.X, y0, box.ZStart), Quaternion.LookRotation(dir));
                    Props.JibBox(b, box.Length / dir.z * 1f, box.Width, box.Top, colours[n++ % colours.Length]);
                    b.Pop();
                }
                else if (f is Kicker k)
                {
                    // Little flags at the corners of the lip.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float x = k.X + s * (k.Width * 0.5f + 0.4f);
                        b.Push(new Vector3(x, c.Height(x, k.LipZ - 0.3f), k.LipZ - 0.3f));
                        Props.GatePole(b, Palette.SignalBlue, -s, false);
                        b.Pop();
                    }
                }
            }
            if (b.VertexCount > 0) b.ToObject("Park features", Mats.Props, parent);
        }

        static void BuildMarkers(PisteCourse c, RunSession session, Transform parent)
        {
            var b = new MeshBuilder();
            Color slope = Palette.Slope(c.Id);

            // Start and finish arches.
            float zs = RunSession.StartLineZ;
            float ws = c.HalfWidth(zs) * 2f - 3f;
            b.Push(new Vector3(c.CenterX(zs), c.Height(c.CenterX(zs), zs), zs));
            Props.Arch(b, Mathf.Min(ws, 16f), slope);
            b.Pop();
            float zf = c.FinishZ;
            float wf = Mathf.Min(c.HalfWidth(zf) * 2f - 2f, 40f);
            b.Push(new Vector3(c.CenterX(zf), c.Height(c.CenterX(zf), zf), zf));
            Props.Arch(b, wf, Palette.SignalRed, 5f);
            b.Pop();

            // Piste markers down both edges (off-piste has none: you are on your own).
            if (!(c is OffPisteCourse))
            {
                for (float z = 30f; z < c.FinishZ; z += 45f)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float x = c.CenterX(z) + s * (c.HalfWidth(z) - 0.2f);
                        b.Push(new Vector3(x, c.Height(x, z), z));
                        Props.PisteMarker(b, s < 0 ? slope : Palette.SafetyOrange);
                        b.Pop();
                    }
            }

            // Slalom gates.
            if (session != null)
                foreach (var g in session.Gates)
                {
                    Color gc = g.Red ? Palette.SignalRed : Palette.SignalBlue;
                    b.Push(new Vector3(g.InnerX, c.Height(g.InnerX, g.Z), g.Z));
                    Props.GatePole(b, gc, g.Side, true);
                    b.Pop();
                    b.Push(new Vector3(g.OuterX, c.Height(g.OuterX, g.Z), g.Z));
                    Props.GatePole(b, gc, -g.Side, true);
                    b.Pop();
                }

            b.ToObject("Markers", Mats.Props, parent);
        }
    }
}
