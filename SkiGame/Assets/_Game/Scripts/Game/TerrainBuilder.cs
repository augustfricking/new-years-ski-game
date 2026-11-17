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
            MountainBuilder.Build(c, root.transform);
            BuildMarkers(c, session, root.transform);
            return root;
        }

        // ---- snow ------------------------------------------------------------------------

        static void BuildSnow(PisteCourse c, Transform parent)
        {
            bool off = c is OffPisteCourse;
            float step = off ? 2.2f : 1.5f;
            Halfpipe pipe = null;
            foreach (var f in c.Features) if (f is Halfpipe hp) pipe = hp;

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
            Color32 bumpShade = Palette.Mix(Palette.Snow, Palette.SnowShadow, 0.95f), bumpLight = Color.white;
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
                        // Around the lips of a halfpipe the grid closes up, so the wall ends in a crisp vertical edge.
                        if (pipe != null && z > pipe.ZA && z < pipe.ZB) x = PipeWarp(pipe, x);
                    }
                    int i = r * cols + k;
                    pos[i] = new Vector3(x, c.SnowHeight(x, z), z);
                    float edge = Mathf.Abs(x - cx) - hw;
                    bool groomed = !off && edge < -0.6f || off && z > c.FinishZ + 12f;
                    Color32 cc = groomed ? piste : wild;
                    if (groomed)
                    {
                        // Hollows and the lee side of rollers are painted in shadow, crests are picked
                        // out bright, so the bumps can be read from well uphill.
                        float bump = BumpShade(c, x, z);
                        if (bump > 0f) cc = Color32.Lerp(piste, bumpShade, bump);
                        else if (bump < 0f) cc = Color32.Lerp(piste, bumpLight, -bump);
                    }
                    if (pipe != null && z > pipe.ZA + 6f && z < pipe.ZB - 6f)
                    {
                        // Coping: a painted line along each lip, and a shaded wall so the shape reads.
                        float pd = Mathf.Abs(x - pipe.X);
                        if (Mathf.Abs(pd - pipe.HalfWidth) < 0.22f) cc = lip;
                        else if (pd < pipe.HalfWidth) cc = Color32.Lerp(cc, bumpShade, pipe.WallT(x) * pipe.WallT(x) * 0.55f);
                    }
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

        /// <summary>Pulls grid columns toward the lips of the pipe (within three metres either side of each).</summary>
        static float PipeWarp(Halfpipe pipe, float x)
        {
            float d = Mathf.Abs(x - pipe.X), off = d - pipe.HalfWidth;
            const float reach = 3.2f;
            if (Mathf.Abs(off) >= reach) return x;
            float pulled = Mathf.Sign(off) * Mathf.Pow(Mathf.Abs(off) / reach, 2.4f) * reach;
            return pipe.X + Mathf.Sign(x - pipe.X) * (pipe.HalfWidth + pulled);
        }

        /// <summary>How much a point lies in the shade of a pit or roller (+) or on a lit crest (-).</summary>
        static float BumpShade(PisteCourse c, float x, float z)
        {
            float shade = 0f;
            foreach (var f in c.Features)
            {
                if (z < f.Z0 || z > f.Z1 || Mathf.Abs(x - f.X) > f.HalfW) continue;
                if (f is Pit pit)
                {
                    // The whole hollow is painted in deep shadow, darkest round the rim, so that it
                    // shows as a blue ellipse even at the shallow angle you approach it from.
                    float r = Mathf.Sqrt((x - pit.X) * (x - pit.X) + (z - pit.Zc) * (z - pit.Zc)) / pit.Radius;
                    if (r < 1f) shade = Mathf.Max(shade, 0.62f + 0.38f * Mathf.Clamp01((r - 0.55f) / 0.4f));
                }
                else if (f is Roller roller && roller.Amp > 0f)
                {
                    float k = Mathf.Clamp01(roller.Add(x, z) / roller.Amp);
                    float u = (z - roller.Zc) / roller.Len;
                    if (k <= 0.02f) continue;
                    if (u > 0.05f) shade = Mathf.Max(shade, Mathf.Clamp01(k * 1.6f) * Mathf.Clamp01(u * 3f) * 0.9f);        // lee side: you cannot see over it
                    else if (u > -0.3f) shade = Mathf.Max(shade, k * 0.75f * Mathf.Clamp01((u + 0.3f) / 0.12f));               // a shadow line along the crest
                    else if (u > -0.75f) shade = Mathf.Min(shade, -k * 0.9f * Mathf.Clamp01((u + 0.75f) / 0.2f));              // the lit face rising toward it
                }
                else if (f is Roller dip && dip.Amp < 0f)
                {
                    shade = Mathf.Max(shade, Mathf.Clamp01(dip.Add(x, z) / dip.Amp) * 0.6f);
                }
            }
            return Mathf.Clamp(shade, -1f, 1f);
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
                        if (Mathf.Abs(x - MountainBuilder.LiftX(c, zz)) < 4.6f) continue;   // the lift line is cut through the trees
                        if (MountainBuilder.InOtherRun(c, x, zz, 2.5f)) continue;               // and so are the other runs
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
                else if (f is Pit || f is Roller)
                {
                    // Crossed orange poles either side, as on a real black run. No flags.
                    float halfW = f is Pit pit ? pit.Radius + 0.9f : Mathf.Min(((Roller)f).Width * 0.5f + 1f, c.HalfWidth(f.Z0) - 0.6f);
                    float zc = (f.Z0 + f.Z1) * 0.5f;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float x = f.X + s * halfW;
                        if (Mathf.Abs(x - c.CenterX(zc)) > c.HalfWidth(zc) + 1.5f) continue;
                        b.Push(new Vector3(x, c.Height(x, zc), zc));
                        b.Col = Palette.SafetyOrange;
                        b.Tube(new Vector3(-0.42f, 0f, 0f), new Vector3(0.42f, 1.55f, 0f), 0.024f, 5);
                        b.Tube(new Vector3(0.42f, 0f, 0f), new Vector3(-0.42f, 1.55f, 0f), 0.024f, 5);
                        b.Pop();
                    }
                }
                else if (f is Halfpipe hpipe)
                {
                    // Orange safety netting along the outside of both decks: the pipe is fenced, not open to the forest.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float x = hpipe.X + s * (hpipe.HalfWidth + Halfpipe.Deck - 0.8f);
                        Vector3 prev = default; bool has = false;
                        for (float z = hpipe.ZA + 18f; z <= hpipe.ZB - 18f; z += 5f)
                        {
                            Vector3 p = new Vector3(x, c.SnowHeight(x, z), z);
                            b.Col = Palette.SteelDark;
                            b.Tube(p, p + Vector3.up * 1.7f, 0.035f, 5);
                            if (has)
                            {
                                b.Col = Palette.SafetyOrange;
                                b.DoubleQuad(prev + Vector3.up * 0.25f, prev + Vector3.up * 1.6f, p + Vector3.up * 1.6f, p + Vector3.up * 0.25f);
                                b.Col = Palette.White;
                                b.DoubleQuad(prev + Vector3.up * 0.85f, prev + Vector3.up * 1.0f, p + Vector3.up * 1.0f, p + Vector3.up * 0.85f);
                            }
                            prev = p; has = true;
                        }
                    }
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
