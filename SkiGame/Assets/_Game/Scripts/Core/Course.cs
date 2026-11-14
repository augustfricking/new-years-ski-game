using System.Collections.Generic;
using UnityEngine;

namespace Ski.Core
{
    public enum SlopeId { Green = 0, Blue = 1, Red = 2, Black = 3, OffPiste = 4 }

    public enum ObstacleKind { Tree, DeadTree, Rock, Bush, Fence, Building, Sign, Pylon }

    public enum SurfaceKind { Snow, Box }

    public struct Obstacle
    {
        public float X, Z;
        public float Radius;      // circle obstacles
        public float Hx, Hz;      // box obstacles (half extents); zero for circles
        public float Height;      // you can fly over it above this height
        public ObstacleKind Kind;
        public float Scale;
        public int Variant;
        public bool Solid;        // false: brushes you (bush) and only slows you down
        public bool IsBox => Hx > 0f;
    }

    /// <summary>A circular trigger area on the ground.</summary>
    public struct Zone
    {
        public float X, Z, Radius;
        public Zone(float x, float z, float r) { X = x; Z = z; Radius = r; }
        public bool Contains(float x, float z) { float dx = x - X, dz = z - Z; return dx * dx + dz * dz <= Radius * Radius; }
    }

    /// <summary>
    /// A ski run as pure data and maths: a height function, limits, obstacles and the village at
    /// the bottom. Downhill is +Z; height decreases with Z. The simulation and the mesh builder
    /// both read this, so what you see is what you ski on.
    /// </summary>
    public abstract class Course
    {
        public SlopeId Id;
        public string Name;
        public int Seed;

        public float StartX, StartZ = 4f;
        public float FinishZ;
        /// <summary>True when tree walls stop the skier at CenterX +- HalfWidth.</summary>
        public bool Walled = true;

        // Village at the bottom (shared layout, relative to the finish line).
        public const float PlazaLength = 130f;
        public const float PlazaHalfWidth = 46f;
        public Zone LiftZone, TavernZone;
        public float VillageX; // x of the plaza centre

        // Mesh extents.
        public float MinX, MaxX, MinZ = -40f, MaxZ;

        public readonly List<Obstacle> Obstacles = new List<Obstacle>();
        public readonly List<Vector2> Checkpoints = new List<Vector2>();

        const float BucketSize = 8f;
        List<int>[] _buckets;
        float _maxObstacleExtent;

        public abstract float Height(float x, float z);
        public virtual float CenterX(float z) => 0f;
        public virtual float HalfWidth(float z) => 1e6f;
        public virtual SurfaceKind SurfaceAt(float x, float z) => SurfaceKind.Snow;
        /// <summary>True where the forest is too dense to ski through (off-piste).</summary>
        public virtual bool Blocked(float x, float z) => false;
        /// <summary>Snow friction multiplier (deep snow off-piste is slower).</summary>
        public float MuScale = 1f;
        /// <summary>Extra acceleration while airborne (used by the halfpipe to bring riders back in).</summary>
        public virtual Vector3 AirAssist(Vector3 pos, Vector3 vel) => Vector3.zero;
        /// <summary>The line a sensible skier follows; used by bots and for respawns.</summary>
        public virtual float RacingLineX(float z) => CenterX(z);

        public bool InVillage(float z) => z > FinishZ;

        public Vector3 Normal(float x, float z)
        {
            const float e = 0.3f;
            float hx = Height(x + e, z) - Height(x - e, z);
            float hz = Height(x, z + e) - Height(x, z - e);
            return new Vector3(-hx, 2f * e, -hz).normalized;
        }

        /// <summary>Heading (0 = +Z, positive toward +X) of the fall line at a point.</summary>
        public float FallHeading(float x, float z)
        {
            const float e = 1.5f;
            float hx = Height(x + e, z) - Height(x - e, z);
            float hz = Height(x, z + e) - Height(x, z - e);
            if (hx * hx + hz * hz < 1e-5f) return 0f;
            return Mathf.Atan2(-hx, -hz);
        }

        public Vector3 PointOnSurface(float x, float z) => new Vector3(x, Height(x, z), z);

        /// <summary>Where to put a skier back on their feet after a crash near pos.</summary>
        public virtual Vector2 RecoverPoint(Vector3 pos)
        {
            if (!Walled) return new Vector2(pos.x, pos.z);
            float c = CenterX(pos.z), hw = HalfWidth(pos.z) - 2.5f;
            return new Vector2(Mathf.Clamp(pos.x, c - hw, c + hw), pos.z);
        }

        /// <summary>Last checkpoint at or above the given z (smaller z = higher up the hill).</summary>
        public Vector2 CheckpointBefore(float z, float x)
        {
            Vector2 best = new Vector2(StartX, StartZ);
            float bestScore = float.MaxValue;
            for (int i = 0; i < Checkpoints.Count; i++)
            {
                var c = Checkpoints[i];
                if (c.y > z - 6f) continue;
                float score = (z - c.y) + Mathf.Abs(x - c.x) * 0.35f;
                if (score < bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        // ---- obstacles -------------------------------------------------------------------

        public void AddObstacle(Obstacle o)
        {
            Obstacles.Add(o);
            _buckets = null;
        }

        public void AddCircle(ObstacleKind kind, float x, float z, float radius, float height, float scale = 1f, int variant = 0, bool solid = true)
        {
            AddObstacle(new Obstacle { Kind = kind, X = x, Z = z, Radius = radius, Height = height, Scale = scale, Variant = variant, Solid = solid });
        }

        public void AddBox(ObstacleKind kind, float x, float z, float hx, float hz, float height)
        {
            AddObstacle(new Obstacle { Kind = kind, X = x, Z = z, Hx = hx, Hz = hz, Height = height, Scale = 1f, Solid = true });
        }

        void BuildBuckets()
        {
            int n = Mathf.CeilToInt((MaxZ - MinZ) / BucketSize) + 2;
            _buckets = new List<int>[n];
            _maxObstacleExtent = 0f;
            for (int i = 0; i < Obstacles.Count; i++)
            {
                var o = Obstacles[i];
                float ext = o.IsBox ? o.Hz : o.Radius;
                _maxObstacleExtent = Mathf.Max(_maxObstacleExtent, ext);
                int b = Mathf.Clamp(Mathf.FloorToInt((o.Z - MinZ) / BucketSize), 0, n - 1);
                (_buckets[b] ??= new List<int>()).Add(i);
            }
        }

        /// <summary>First obstacle overlapping a circle at ground level, if any.</summary>
        public bool HitObstacle(float x, float z, float radius, float heightAboveGround, out Obstacle hit)
        {
            if (_buckets == null) BuildBuckets();
            float reach = radius + _maxObstacleExtent;
            int b0 = Mathf.Clamp(Mathf.FloorToInt((z - reach - MinZ) / BucketSize), 0, _buckets.Length - 1);
            int b1 = Mathf.Clamp(Mathf.FloorToInt((z + reach - MinZ) / BucketSize), 0, _buckets.Length - 1);
            for (int b = b0; b <= b1; b++)
            {
                var list = _buckets[b];
                if (list == null) continue;
                for (int k = 0; k < list.Count; k++)
                {
                    var o = Obstacles[list[k]];
                    if (heightAboveGround > o.Height) continue;
                    if (o.IsBox)
                    {
                        float dx = Mathf.Max(Mathf.Abs(x - o.X) - o.Hx, 0f);
                        float dz = Mathf.Max(Mathf.Abs(z - o.Z) - o.Hz, 0f);
                        if (dx * dx + dz * dz < radius * radius) { hit = o; return true; }
                    }
                    else
                    {
                        float dx = x - o.X, dz = z - o.Z, rr = radius + o.Radius;
                        if (dx * dx + dz * dz < rr * rr) { hit = o; return true; }
                    }
                }
            }
            hit = default;
            return false;
        }

        /// <summary>Distance to the nearest solid obstacle surface within maxDist (for bots and placement).</summary>
        public float Clearance(float x, float z, float maxDist)
        {
            if (_buckets == null) BuildBuckets();
            float best = maxDist;
            float reach = maxDist + _maxObstacleExtent;
            int b0 = Mathf.Clamp(Mathf.FloorToInt((z - reach - MinZ) / BucketSize), 0, _buckets.Length - 1);
            int b1 = Mathf.Clamp(Mathf.FloorToInt((z + reach - MinZ) / BucketSize), 0, _buckets.Length - 1);
            for (int b = b0; b <= b1; b++)
            {
                var list = _buckets[b];
                if (list == null) continue;
                for (int k = 0; k < list.Count; k++)
                {
                    var o = Obstacles[list[k]];
                    if (!o.Solid) continue;
                    float d;
                    if (o.IsBox)
                    {
                        float dx = Mathf.Max(Mathf.Abs(x - o.X) - o.Hx, 0f);
                        float dz = Mathf.Max(Mathf.Abs(z - o.Z) - o.Hz, 0f);
                        d = Mathf.Sqrt(dx * dx + dz * dz);
                    }
                    else
                    {
                        float dx = x - o.X, dz = z - o.Z;
                        d = Mathf.Sqrt(dx * dx + dz * dz) - o.Radius;
                    }
                    if (d < best) best = d;
                }
            }
            return best;
        }

        // ---- village ---------------------------------------------------------------------

        /// <summary>Places the lift base, the tavern and the plaza fences. Call once FinishZ is known.</summary>
        protected void BuildVillage()
        {
            float x0 = VillageX, z0 = FinishZ;
            // Lift base on the skier's left, tavern on the right, both facing the plaza.
            LiftZone = new Zone(x0 - 24f, z0 + 78f, 5.5f);
            TavernZone = new Zone(x0 + 23f, z0 + 92f, 3.2f);
            AddBox(ObstacleKind.Building, x0 - 33f, z0 + 82f, 5f, 7f, 50f);      // lift station hut
            AddBox(ObstacleKind.Building, x0 + 33f, z0 + 94f, 8f, 11f, 50f);     // tavern
            AddBox(ObstacleKind.Fence, x0, z0 + PlazaLength + 1f, PlazaHalfWidth, 1f, 50f); // end of the plaza
        }

        public static float PlazaHalfWidthAt(float distPastFinish, float pisteHalfWidth)
        {
            return Mathf.Lerp(pisteHalfWidth, PlazaHalfWidth, M.Smooth(distPastFinish / 35f));
        }
    }
}
