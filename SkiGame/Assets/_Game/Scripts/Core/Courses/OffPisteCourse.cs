using System.Collections.Generic;
using UnityEngine;

namespace Ski.Core
{
    /// <summary>A sheer drop into an enclosed pocket: the end of a wrong turn.</summary>
    public sealed class CliffPocket : Feature
    {
        public float Zc, Depth;
        public CliffPocket(float x, float z, float depth) { X = x; Zc = z; Depth = depth; Z0 = z; Z1 = z + 44f; HalfW = 13f; }
        public override float Add(float x, float z)
        {
            float dz = z - Zc;
            if (dz < 0f) return 0f;
            float lat = 1f - M.Smooth((Mathf.Abs(x - X) - 9f) / 3.5f);
            return -Depth * M.Smooth(dz / 2.5f) * lat * (1f - M.Smooth((dz - 32f) / 11f));
        }
        public override void Breakpoints(List<float> zs) { zs.Add(Zc); zs.Add(Zc + 2.5f); }
    }

    /// <summary>A small ledge across a corridor that you can drop off.</summary>
    public sealed class Drop : Feature
    {
        public float Zc, H, Width;
        public Drop(float x, float z, float h, float width) { X = x; Zc = z; H = h; Width = width; Z0 = z; Z1 = z + 52f; HalfW = width * 0.5f + 5f; }
        public override float Add(float x, float z)
        {
            float dz = z - Zc;
            if (dz < 0f) return 0f;
            float lat = 1f - M.Smooth((Mathf.Abs(x - X) - Width * 0.5f) / 5f);
            return -H * M.Smooth(dz / 1.2f) * (1f - M.Smooth((dz - 6f) / 45f)) * lat;
        }
        public override void Breakpoints(List<float> zs) { zs.Add(Zc); zs.Add(Zc + 1.2f); }
    }

    /// <summary>
    /// Off-piste: a wide forested face with a maze of glades. A few routes reach the village;
    /// more of them end at a cliff or in a thicket. Generated from a fixed seed, so the mountain
    /// is the same every run and can be learned.
    /// </summary>
    public sealed class OffPisteCourse : PisteCourse
    {
        public sealed class Segment
        {
            public Vector2 A, B;
            public float HwA, HwB;
            public bool Solution;
        }

        public enum DeadEndKind { Cliff, Thicket }
        public struct DeadEnd { public Zone Zone; public DeadEndKind Kind; }

        public readonly List<Segment> Segments = new List<Segment>();
        public readonly List<DeadEnd> DeadEnds = new List<DeadEnd>();
        /// <summary>Way-points of the valid routes, top to bottom.</summary>
        public readonly List<List<Vector2>> Solutions = new List<List<Vector2>>();

        const int Rows = 11, Slots = 5;
        const float RowLen = 104f, FirstRowZ = 80f, SlotGap = 44f, FieldHalfWidth = 128f;
        const float Cell = 2f;

        float[] _clear; int _nx, _nz; float _gx0, _gz0;
        readonly Rng _rng;
        readonly Vector2[,] _node = new Vector2[Rows + 1, Slots];
        readonly bool[,] _used = new bool[Rows + 1, Slots];
        readonly List<(int r, int a, int b)> _edges = new List<(int, int, int)>();

        public OffPisteCourse()
        {
            Id = SlopeId.OffPiste; Name = "Ravinen"; Seed = 71;
            BaseHalfWidth = FieldHalfWidth; NoiseAmp = 0.09f; NoiseLen = 9f;
            MuScale = 2.9f; // deep snow
            _rng = new Rng(Seed);
            float finish = FirstRowZ + Rows * RowLen + 34f;
            BuildProfile(finish, (30f, 14f), (110f, 22f), (330f, 19f), (520f, 25f), (760f, 20f), (980f, 24f), (1150f, 19f));
            MinX = -FieldHalfWidth - 16f; MaxX = FieldHalfWidth + 16f;

            for (int r = 0; r <= Rows; r++)
                for (int s = 0; s < Slots; s++)
                {
                    bool end = r == 0 || r == Rows;
                    _node[r, s] = new Vector2((s - 2) * SlotGap + (end ? 0f : _rng.Range(-9f, 9f)), FirstRowZ + r * RowLen + (end ? 0f : _rng.Range(-12f, 12f)));
                }

            // Start glade and the funnel into the village.
            AddSegment(new Vector2(0f, -12f), _node[0, 2], 11f, 11f, true);
            AddSegment(_node[Rows, 2], new Vector2(0f, finish + 12f), 13f, 20f, true);

            var pathA = MakeSolution(1, null);
            var pathB = MakeSolution(3, pathA);
            AddSolution(pathA);
            AddSolution(pathB);
            AddDeadEnds(pathA, pathB);
            BuildClearMask();
            ScatterCorridorObstacles();
            PlantForest();

            for (int r = 0; r <= Rows; r++)
                for (int s = 0; s < Slots; s++)
                    if (_used[r, s]) Checkpoints.Add(_node[r, s]);
        }

        // ---- generation --------------------------------------------------------------------

        int[] MakeSolution(int homeSlot, int[] avoid)
        {
            var p = new int[Rows + 1];
            p[0] = 2;
            for (int r = 1; r < Rows; r++)
            {
                int s = p[r - 1];
                int want = s < homeSlot ? 1 : s > homeSlot ? -1 : 0;
                int step = _rng.Chance(0.55f) ? want : _rng.Range(-1, 2);
                // The last rows must be able to reach slot 2 again.
                int remaining = Rows - r;
                int next = Mathf.Clamp(s + step, 0, Slots - 1);
                if (Mathf.Abs(next - 2) > remaining) next = s + (2 > s ? 1 : -1);
                if (avoid != null && r > 1 && r < Rows - 1 && next == avoid[r])
                {
                    int alt = Mathf.Clamp(s + (homeSlot >= 2 ? 1 : -1), 0, Slots - 1);
                    if (alt != avoid[r] && Mathf.Abs(alt - 2) <= remaining && !Crosses(r - 1, s, alt)) next = alt;
                }
                p[r] = next;
                _edges.Add((r - 1, s, next));
            }
            p[Rows] = 2;
            _edges.Add((Rows - 1, p[Rows - 1], 2));
            return p;
        }

        bool Crosses(int row, int a, int b)
        {
            foreach (var e in _edges)
                if (e.r == row && (e.a - a) * (e.b - b) < 0) return true;
            return false;
        }

        void AddSolution(int[] p)
        {
            var pts = new List<Vector2> { new Vector2(0f, StartZ) };
            for (int r = 0; r < Rows; r++)
            {
                _used[r, p[r]] = true; _used[r + 1, p[r + 1]] = true;
                Vector2 a = _node[r, p[r]], b = _node[r + 1, p[r + 1]];
                Vector2 m = AddEdge(a, b, 8f, true);
                if (pts[pts.Count - 1] != a) pts.Add(a);
                pts.Add(m);
                // A ledge to drop off on some of the valid routes.
                if (r > 0 && _rng.Chance(0.38f))
                {
                    Vector2 d = Vector2.Lerp(m, b, 0.45f);
                    AddFeature(new Drop(d.x, d.y, _rng.Range(1.5f, 2.6f), 20f));
                }
            }
            pts.Add(_node[Rows, 2]);
            pts.Add(new Vector2(0f, FinishZ + 20f));
            Solutions.Add(pts);
        }

        /// <summary>Adds a two-piece corridor with a kink in the middle; returns the kink point.</summary>
        Vector2 AddEdge(Vector2 a, Vector2 b, float hw, bool solution)
        {
            Vector2 m = (a + b) * 0.5f + new Vector2(_rng.Range(-9f, 9f), _rng.Range(-8f, 8f));
            AddSegment(a, m, hw, hw, solution);
            AddSegment(m, b, hw, hw, solution);
            return m;
        }

        void AddSegment(Vector2 a, Vector2 b, float hwA, float hwB, bool solution)
        {
            Segments.Add(new Segment { A = a, B = b, HwA = hwA, HwB = hwB, Solution = solution });
        }

        void AddDeadEnds(int[] pathA, int[] pathB)
        {
            int made = 0, tries = 0;
            while (made < 7 && tries++ < 400)
            {
                var path = _rng.Chance(0.5f) ? pathA : pathB;
                int r = _rng.Range(1, Rows - 2);
                int s = path[r];
                int dir = _rng.Chance(0.5f) ? -1 : 1;
                int t = s + dir;
                if (t < 0 || t >= Slots || _used[r + 1, t] || Crosses(r, s, t)) continue;

                // Branch off, wander for up to two more rows, then end badly.
                int cr = r + 1, cs = t;
                _used[cr, cs] = true;
                _edges.Add((r, s, t));
                AddEdge(_node[r, s], _node[cr, cs], 7.5f, false);
                int extra = _rng.Range(0, 3);
                for (int k = 0; k < extra && cr < Rows - 2; k++)
                {
                    int ns = cs + _rng.Range(-1, 2);
                    if (ns < 0 || ns >= Slots || _used[cr + 1, ns] || Crosses(cr, cs, ns)) break;
                    _edges.Add((cr, cs, ns));
                    AddEdge(_node[cr, cs], _node[cr + 1, ns], 7.5f, false);
                    cr++; cs = ns;
                    _used[cr, cs] = true;
                }
                Vector2 n = _node[cr, cs];
                Vector2 end = n + new Vector2(_rng.Range(-12f, 12f), _rng.Range(44f, 56f));
                if (_rng.Chance(0.5f))
                {
                    // Cliff: a blind roll, then nothing.
                    AddSegment(n, end, 7.5f, 8f, false);
                    AddSegment(end, end + new Vector2(0f, 40f), 9f, 9f, false);
                    AddFeature(new Roller(end.x, end.y - 9f, 8f, 0.85f, 16f));
                    AddFeature(new CliffPocket(end.x, end.y, _rng.Range(15f, 21f)));
                    DeadEnds.Add(new DeadEnd { Kind = DeadEndKind.Cliff, Zone = new Zone(end.x, end.y + 20f, 21f) });
                }
                else
                {
                    // Thicket: the glade closes in.
                    AddSegment(n, end, 7.5f, 0.3f, false);
                    DeadEnds.Add(new DeadEnd { Kind = DeadEndKind.Thicket, Zone = new Zone(end.x, end.y - 6f, 16f) });
                }
                made++;
            }
        }

        void BuildClearMask()
        {
            _gx0 = -FieldHalfWidth - 6f; _gz0 = MinZ;
            _nx = Mathf.CeilToInt((FieldHalfWidth * 2f + 12f) / Cell) + 1;
            _nz = Mathf.CeilToInt((FinishZ + 30f - _gz0) / Cell) + 1;
            _clear = new float[_nx * _nz];
            for (int i = 0; i < _clear.Length; i++) _clear[i] = -50f;
            foreach (var s in Segments)
            {
                float reach = Mathf.Max(s.HwA, s.HwB) + 14f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(s.A.x, s.B.x) - reach - _gx0) / Cell));
                int x1 = Mathf.Min(_nx - 1, Mathf.CeilToInt((Mathf.Max(s.A.x, s.B.x) + reach - _gx0) / Cell));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(s.A.y, s.B.y) - reach - _gz0) / Cell));
                int z1 = Mathf.Min(_nz - 1, Mathf.CeilToInt((Mathf.Max(s.A.y, s.B.y) + reach - _gz0) / Cell));
                for (int iz = z0; iz <= z1; iz++)
                    for (int ix = x0; ix <= x1; ix++)
                    {
                        float px = _gx0 + ix * Cell, pz = _gz0 + iz * Cell;
                        float d = M.DistToSegment(px, pz, s.A.x, s.A.y, s.B.x, s.B.y, out float t);
                        float c = Mathf.Lerp(s.HwA, s.HwB, t) - d;
                        int idx = iz * _nx + ix;
                        if (c > _clear[idx]) _clear[idx] = c;
                    }
            }
            // Junction clearings.
            for (int r = 0; r <= Rows; r++)
                for (int sl = 0; sl < Slots; sl++)
                {
                    if (!_used[r, sl]) continue;
                    Vector2 n = _node[r, sl];
                    int x0 = Mathf.Max(0, Mathf.FloorToInt((n.x - 26f - _gx0) / Cell)), x1 = Mathf.Min(_nx - 1, Mathf.CeilToInt((n.x + 26f - _gx0) / Cell));
                    int z0 = Mathf.Max(0, Mathf.FloorToInt((n.y - 26f - _gz0) / Cell)), z1 = Mathf.Min(_nz - 1, Mathf.CeilToInt((n.y + 26f - _gz0) / Cell));
                    for (int iz = z0; iz <= z1; iz++)
                        for (int ix = x0; ix <= x1; ix++)
                        {
                            float dx = _gx0 + ix * Cell - n.x, dz = _gz0 + iz * Cell - n.y;
                            float c = 11f - Mathf.Sqrt(dx * dx + dz * dz);
                            int idx = iz * _nx + ix;
                            if (c > _clear[idx]) _clear[idx] = c;
                        }
                }
        }

        /// <summary>Metres inside open ground (positive) or into the forest (negative).</summary>
        public float Clear(float x, float z)
        {
            if (z > FinishZ + 10f) return 20f;
            float fx = (x - _gx0) / Cell, fz = (z - _gz0) / Cell;
            int ix = Mathf.Clamp((int)fx, 0, _nx - 2), iz = Mathf.Clamp((int)fz, 0, _nz - 2);
            float tx = Mathf.Clamp01(fx - ix), tz = Mathf.Clamp01(fz - iz);
            int i = iz * _nx + ix;
            return Mathf.Lerp(Mathf.Lerp(_clear[i], _clear[i + 1], tx), Mathf.Lerp(_clear[i + _nx], _clear[i + _nx + 1], tx), tz);
        }

        public override bool Blocked(float x, float z) => z < FinishZ + 6f && Clear(x, z) < -2.4f;

        void ScatterCorridorObstacles()
        {
            foreach (var s in Segments)
            {
                float len = (s.B - s.A).magnitude;
                if (len < 30f || s.A.y < FirstRowZ - 1f || s.B.y > FinishZ - 20f) continue;
                Vector2 dir = (s.B - s.A) / len, perp = new Vector2(dir.y, -dir.x);
                float d = _rng.Range(12f, 18f);
                while (d < len - 12f)
                {
                    float t = d / len;
                    float hw = Mathf.Lerp(s.HwA, s.HwB, t);
                    if (hw > 4f)
                    {
                        Vector2 p = s.A + dir * d + perp * _rng.Range(-(hw - 2.2f), hw - 2.2f);
                        float pick = _rng.Value();
                        if (pick < 0.35f) AddCircle(ObstacleKind.Rock, p.x, p.y, _rng.Range(0.7f, 1.25f), 0.8f, _rng.Range(0.8f, 1.4f), _rng.Range(0, 3));
                        else if (pick < 0.62f) AddCircle(ObstacleKind.Bush, p.x, p.y, 0.9f, 0.8f, _rng.Range(0.8f, 1.3f), _rng.Range(0, 3), false);
                        else if (pick < 0.9f) AddCircle(ObstacleKind.Tree, p.x, p.y, 0.45f, 6f, _rng.Range(0.6f, 1f), _rng.Range(0, 3));
                        else AddCircle(ObstacleKind.DeadTree, p.x, p.y, 0.4f, 3f, _rng.Range(0.8f, 1.2f), _rng.Range(0, 2));
                    }
                    d += _rng.Range(13f, 23f);
                }
            }
        }

        void PlantForest()
        {
            const float gap = 2.3f;
            int nx = Mathf.FloorToInt(FieldHalfWidth * 2f / gap), nz = Mathf.FloorToInt((FinishZ + 4f - 16f) / gap);
            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    float x = -FieldHalfWidth + (ix + 0.5f) * gap + _rng.Range(-0.8f, 0.8f);
                    float z = 16f + (iz + 0.5f) * gap + _rng.Range(-0.8f, 0.8f);
                    float skip = _rng.Value(), kind = _rng.Value();
                    float scale = _rng.Range(0.8f, 1.5f);
                    int variant = _rng.Range(0, 3);
                    float c = Clear(x, z);
                    if (c > -0.7f) continue;
                    if (c < -9f && ((ix & 1) != 0 || (iz & 1) != 0 || skip < 0.3f)) continue; // sparse deep forest
                    if (z > FinishZ - 6f && Mathf.Abs(x) < HalfWidth(z) + 2f) continue;
                    AddCircle(kind < 0.06f ? ObstacleKind.DeadTree : ObstacleKind.Tree, x, z, 0.5f, 9f, scale, variant);
                }
        }

        // ---- queries -----------------------------------------------------------------------

        public override float Height(float x, float z)
        {
            float h = base.Height(x, z);
            float wild = M.Smooth((z - 20f) / 40f) * (1f - M.Smooth((z - (FinishZ - 30f)) / 40f));
            if (wild > 0f)
            {
                h += wild * (3.2f * Noise.Fbm(x / 150f, z / 170f, Seed + 3) + 0.55f * Noise.Fbm(x / 38f, z / 38f, Seed + 5));
            }
            return h;
        }

        public override float RacingLineX(float z)
        {
            var p = Solutions[0];
            for (int i = 0; i < p.Count - 1; i++)
                if (z <= p[i + 1].y)
                {
                    float t = Mathf.InverseLerp(p[i].y, p[i + 1].y, z);
                    return Mathf.Lerp(p[i].x, p[i + 1].x, t);
                }
            return p[p.Count - 1].x;
        }

        public override Vector2 RecoverPoint(Vector3 pos)
        {
            if (pos.z > FinishZ) return base.RecoverPoint(pos);
            if (Clear(pos.x, pos.z) > 2f && !HitObstacle(pos.x, pos.z, 1.2f, 0f, out _)) return new Vector2(pos.x, pos.z);
            // Back to the middle of the nearest glade.
            float best = float.MaxValue; Vector2 bp = new Vector2(pos.x, pos.z);
            foreach (var s in Segments)
            {
                float d = M.DistToSegment(pos.x, pos.z, s.A.x, s.A.y, s.B.x, s.B.y, out float t);
                if (d < best) { best = d; bp = Vector2.Lerp(s.A, s.B, t); }
            }
            for (int k = 0; k < 6 && HitObstacle(bp.x, bp.y, 1.2f, 0f, out var o); k++) bp.x += (o.X > bp.x ? -1f : 1f) * 1.8f;
            return bp;
        }

        public bool InDeadEnd(float x, float z, out DeadEndKind kind)
        {
            for (int i = 0; i < DeadEnds.Count; i++)
                if (DeadEnds[i].Zone.Contains(x, z)) { kind = DeadEnds[i].Kind; return true; }
            kind = default;
            return false;
        }
    }
}
