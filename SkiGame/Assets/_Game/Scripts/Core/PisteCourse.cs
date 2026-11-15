using System.Collections.Generic;
using UnityEngine;

namespace Ski.Core
{
    /// <summary>Something added on top of the base slope: a jump, a pit, a box...</summary>
    public abstract class Feature
    {
        public float X;            // lateral centre (world x at the feature's z)
        public float Z0, Z1;       // z extent, for culling
        public float HalfW;        // lateral extent, for culling
        public abstract float Add(float x, float z);
        public virtual SurfaceKind Surface(float x, float z) => SurfaceKind.Snow;
        /// <summary>Z positions where the shape has a sharp edge; the mesh builder adds rows there.</summary>
        public virtual void Breakpoints(List<float> zs) { }
    }

    /// <summary>
    /// A park jump: curved take-off, a short drop to a flat deck, then a landing that dips a little
    /// below the piste so it is steep enough to be soft, and a long run-out back up.
    /// </summary>
    public sealed class Kicker : Feature
    {
        public float LipZ, LipHeight, TakeoffLen, DeckLen, LandingLen, RecoverLen, Width, Dip;
        public float DeckHeight => LipHeight * 0.72f;

        public Kicker(float x, float lipZ, float lipHeight, float width = 9f)
        {
            X = x; LipZ = lipZ; LipHeight = lipHeight; Width = width;
            TakeoffLen = 4f + lipHeight * 3.4f;
            DeckLen = 3f + lipHeight * 3f;
            LandingLen = 9f + lipHeight * 6f;
            RecoverLen = 16f + lipHeight * 6f;
            Dip = lipHeight * 0.8f;
            Z0 = lipZ - TakeoffLen; Z1 = lipZ + DeckLen + LandingLen + RecoverLen;
            HalfW = width * 0.5f + 2.5f;
        }

        public override float Add(float x, float z)
        {
            float dz = z - LipZ;
            if (dz < -TakeoffLen || dz > DeckLen + LandingLen + RecoverLen) return 0f;
            float lat = 1f - M.Smooth((Mathf.Abs(x - X) - Width * 0.5f) / 2.5f);
            if (lat <= 0f) return 0f;
            float a;
            if (dz <= 0f) { float u = (dz + TakeoffLen) / TakeoffLen; a = LipHeight * Mathf.Pow(u, 1.7f); }
            else if (dz < 0.6f) a = Mathf.Lerp(LipHeight, DeckHeight, dz / 0.6f);
            else if (dz < DeckLen) a = DeckHeight;
            else if (dz < DeckLen + LandingLen) a = DeckHeight - (DeckHeight + Dip) * M.Smooth((dz - DeckLen) / LandingLen);
            else a = -Dip * (1f - M.Smooth((dz - DeckLen - LandingLen) / RecoverLen));
            return a * lat;
        }

        public override void Breakpoints(List<float> zs)
        {
            zs.Add(LipZ - TakeoffLen); zs.Add(LipZ - 0.02f); zs.Add(LipZ + 0.6f); zs.Add(LipZ + DeckLen); zs.Add(LipZ + DeckLen + LandingLen);
        }
    }

    /// <summary>A round hollow. Harmless slowly, launches and slams you at speed.</summary>
    public sealed class Pit : Feature
    {
        public float Zc, Radius, Depth;
        public Pit(float x, float z, float radius, float depth)
        {
            X = x; Zc = z; Radius = radius; Depth = depth;
            Z0 = z - radius; Z1 = z + radius; HalfW = radius;
        }
        public override float Add(float x, float z)
        {
            float dx = x - X, dz = z - Zc;
            float u2 = (dx * dx + dz * dz) / (Radius * Radius);
            if (u2 >= 1f) return 0f;
            float k = 1f - u2;
            return -Depth * k * k;
        }
    }

    /// <summary>A rounded bump across part of the piste (a roller or a compression when negative).</summary>
    public sealed class Roller : Feature
    {
        public float Zc, Len, Amp, Width;
        public Roller(float x, float z, float len, float amp, float width)
        {
            X = x; Zc = z; Len = len; Amp = amp; Width = width;
            Z0 = z - len; Z1 = z + len; HalfW = width * 0.5f + 4f;
        }
        public override float Add(float x, float z)
        {
            float u = (z - Zc) / Len;
            if (u <= -1f || u >= 1f) return 0f;
            float k = 1f - u * u;
            float lat = 1f - M.Smooth((Mathf.Abs(x - X) - Width * 0.5f) / 4f);
            return Amp * k * k * lat;
        }
    }

    /// <summary>A flat-topped box you can slide along. A short ramp leads onto it.</summary>
    public sealed class BoxFeature : Feature
    {
        public float ZStart, Length, Width, Top;
        const float RampLen = 3.2f, Side = 0.3f;
        public BoxFeature(float x, float zStart, float length, float width, float top)
        {
            X = x; ZStart = zStart; Length = length; Width = width; Top = top;
            Z0 = zStart - RampLen; Z1 = zStart + length; HalfW = width * 0.5f + Side;
        }
        public override float Add(float x, float z)
        {
            float dz = z - ZStart;
            if (dz < -RampLen || dz > Length) return 0f;
            float lat = 1f - Mathf.Clamp01((Mathf.Abs(x - X) - Width * 0.5f) / Side);
            if (lat <= 0f) return 0f;
            float a = dz < 0f ? Top * M.Smooth((dz + RampLen) / RampLen) : Top;
            return a * lat;
        }
        public override SurfaceKind Surface(float x, float z)
        {
            return z >= ZStart && z <= ZStart + Length && Mathf.Abs(x - X) <= Width * 0.5f ? SurfaceKind.Box : SurfaceKind.Snow;
        }
        public override void Breakpoints(List<float> zs) { zs.Add(ZStart - RampLen); zs.Add(ZStart); zs.Add(ZStart + Length - 0.02f); zs.Add(ZStart + Length + 0.02f); }
    }

    /// <summary>
    /// A halfpipe running down the fall line: a flat bottom and two walls that curve up to nearly
    /// vertical at the lip, so riding up one throws you into the air above it.
    /// </summary>
    public sealed class Halfpipe : Feature
    {
        public float ZA, ZB, HalfWidth, WallHeight;
        const float Blend = 16f, FlatPart = 0.5f, Arc = 0.96f;
        public Halfpipe(float x, float zA, float zB, float halfWidth, float wallHeight)
        {
            X = x; ZA = zA; ZB = zB; HalfWidth = halfWidth; WallHeight = wallHeight;
            Z0 = zA; Z1 = zB; HalfW = halfWidth + 9f;
        }
        public float Envelope(float z) => M.Smooth((z - ZA) / Blend) * (1f - M.Smooth((z - (ZB - Blend)) / Blend));
        /// <summary>0 on the flat bottom, 1 at the lip.</summary>
        public float WallT(float x) => Mathf.Clamp01((Mathf.Abs(x - X) / HalfWidth - FlatPart) / (1f - FlatPart));
        public override float Add(float x, float z)
        {
            if (z < ZA || z > ZB) return 0f;
            float d = Mathf.Abs(x - X);
            float t = WallT(x);
            float wall;
            if (d <= HalfWidth)
            {
                // Quarter-circle transition, stopped just short of vertical.
                float sArc = Arc * t;
                wall = WallHeight * (1f - Mathf.Sqrt(1f - sArc * sArc)) / (1f - Mathf.Sqrt(1f - Arc * Arc));
            }
            else wall = WallHeight * (1f - 0.2f * M.Smooth((d - HalfWidth) / 7f)); // the deck
            float fade = 1f - M.Smooth((d - HalfWidth - 4f) / 5f);
            return wall * Envelope(z) * fade;
        }
    }

    /// <summary>
    /// A groomed run: an elevation profile along Z, a gently bending centre line, tree walls at
    /// the sides and a list of features. Ends in the village plaza.
    /// </summary>
    public class PisteCourse : Course
    {
        public float NoiseAmp = 0.035f, NoiseLen = 17f;
        public float BaseHalfWidth = 16f;

        readonly List<Feature> _features = new List<Feature>();
        public IReadOnlyList<Feature> Features => _features;

        // Centre-line bends: x = sum A sin(2 pi z / L + phase), faded out at both ends.
        readonly List<Vector3> _bends = new List<Vector3>();
        // Width variation.
        float _widthAmp, _widthLen = 210f;

        float[] _elev, _slope;
        float _tableZ0;
        const float FeatureBucket = 16f;
        List<Feature>[] _featureBuckets;

        protected void AddBend(float amplitude, float wavelength, float phase) => _bends.Add(new Vector3(amplitude, wavelength, phase));
        protected void SetWidthVariation(float amp, float len) { _widthAmp = amp; _widthLen = len; }

        public void AddFeature(Feature f) { _features.Add(f); _featureBuckets = null; }

        /// <summary>
        /// Builds the elevation table from gradient keys (z, degrees downhill). The run starts on a
        /// flat platform and ends in a flat plaza with a rise at the far end.
        /// </summary>
        protected void BuildProfile(float finishZ, params (float z, float deg)[] keys)
        {
            FinishZ = finishZ;
            MinZ = -40f;
            MaxZ = finishZ + PlazaLength + 30f;
            var k = new List<(float z, float deg)> { (MinZ - 10f, 0f), (5f, 0f) };
            k.AddRange(keys);
            float lastDeg = keys[keys.Length - 1].deg;
            k.Add((finishZ - 30f, lastDeg));
            k.Add((finishZ + 22f, 1.2f));
            k.Add((finishZ + 60f, 0f));
            k.Add((finishZ + PlazaLength - 22f, 0f));
            k.Add((finishZ + PlazaLength - 4f, -9f));
            k.Add((MaxZ + 20f, -9f));

            _tableZ0 = MinZ - 5f;
            int n = Mathf.CeilToInt(MaxZ + 15f - _tableZ0) + 1;
            _elev = new float[n];
            _slope = new float[n];
            double e = 0;
            int seg = 0;
            const int Sub = 8;
            for (int i = 0; i < n; i++)
            {
                float z = _tableZ0 + i;
                _elev[i] = (float)e;
                _slope[i] = -Mathf.Tan(DegAt(k, z, ref seg) * Mathf.Deg2Rad);
                for (int s = 0; s < Sub; s++)
                {
                    float zs = z + (s + 0.5f) / Sub;
                    e -= Mathf.Tan(DegAt(k, zs, ref seg) * Mathf.Deg2Rad) / Sub;
                }
            }
            // Put z = 0 at height 0.
            float h0 = Elev(0f);
            for (int i = 0; i < n; i++) _elev[i] -= h0;

            float maxBend = 0f;
            foreach (var b in _bends) maxBend += Mathf.Abs(b.x);
            MinX = -(maxBend + PlazaHalfWidth + 45f);
            MaxX = maxBend + PlazaHalfWidth + 45f;
            VillageX = 0f;
            BuildVillage();
        }

        static float DegAt(List<(float z, float deg)> k, float z, ref int seg)
        {
            while (seg < k.Count - 2 && z > k[seg + 1].z) seg++;
            while (seg > 0 && z < k[seg].z) seg--;
            var a = k[seg]; var b = k[seg + 1];
            float t = Mathf.Clamp01((z - a.z) / (b.z - a.z));
            return Mathf.Lerp(a.deg, b.deg, M.Smooth(t));
        }

        /// <summary>Base elevation of the centre line (cubic Hermite through the table).</summary>
        public float Elev(float z)
        {
            float f = z - _tableZ0;
            int i = Mathf.Clamp((int)f, 0, _elev.Length - 2);
            float t = Mathf.Clamp01(f - i);
            float t2 = t * t, t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * _elev[i] + (t3 - 2f * t2 + t) * _slope[i]
                 + (-2f * t3 + 3f * t2) * _elev[i + 1] + (t3 - t2) * _slope[i + 1];
        }

        public override float CenterX(float z)
        {
            if (_bends.Count == 0) return 0f;
            float env = M.Smooth(z / 90f) * (1f - M.Smooth((z - (FinishZ - 110f)) / 100f));
            if (env <= 0f) return 0f;
            float x = 0f;
            for (int i = 0; i < _bends.Count; i++)
            {
                var b = _bends[i];
                x += b.x * Mathf.Sin(2f * Mathf.PI * z / b.y + b.z);
            }
            return x * env;
        }

        public override float HalfWidth(float z)
        {
            float hw = BaseHalfWidth + _widthAmp * Mathf.Sin(2f * Mathf.PI * z / _widthLen + 1.3f);
            if (z < 20f) hw = Mathf.Lerp(9f, hw, M.Smooth(z / 20f)); // narrow start gate area
            if (z > FinishZ) hw = PlazaHalfWidthAt(z - FinishZ, hw);
            return hw;
        }

        public override float Height(float x, float z)
        {
            float h = Elev(z);
            float d = Mathf.Abs(x - CenterX(z));
            float o = d - HalfWidth(z);
            float rough = 1f;
            if (o > 0f)
            {
                h += 2.4f * M.Smooth(o / 9f) + 0.07f * o; // snow bank into the forest
                rough = 1f + 3f * M.Smooth(o / 6f);
            }
            if (z > FinishZ + 30f) rough *= 0.25f; // the plaza is flat and packed
            h += NoiseAmp * rough * Noise.Fbm(x / NoiseLen, z / NoiseLen, Seed);
            if (_features.Count > 0) h += FeatureHeight(x, z);
            return h;
        }

        bool _snowOnly;

        /// <summary>Height of the snow itself, without features that have their own model (jib boxes).</summary>
        public float SnowHeight(float x, float z)
        {
            _snowOnly = true;
            float h = Height(x, z);
            _snowOnly = false;
            return h;
        }

        void BuildFeatureBuckets()
        {
            int n = Mathf.CeilToInt((MaxZ - MinZ) / FeatureBucket) + 1;
            _featureBuckets = new List<Feature>[n];
            foreach (var f in _features)
            {
                int b0 = Mathf.Clamp(Mathf.FloorToInt((f.Z0 - MinZ) / FeatureBucket), 0, n - 1);
                int b1 = Mathf.Clamp(Mathf.FloorToInt((f.Z1 - MinZ) / FeatureBucket), 0, n - 1);
                for (int b = b0; b <= b1; b++) (_featureBuckets[b] ??= new List<Feature>()).Add(f);
            }
        }

        List<Feature> FeaturesNear(float z)
        {
            if (_featureBuckets == null) BuildFeatureBuckets();
            int b = Mathf.FloorToInt((z - MinZ) / FeatureBucket);
            if (b < 0 || b >= _featureBuckets.Length) return null;
            return _featureBuckets[b];
        }

        float FeatureHeight(float x, float z)
        {
            var list = FeaturesNear(z);
            if (list == null) return 0f;
            float a = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                var f = list[i];
                if (z < f.Z0 || z > f.Z1 || Mathf.Abs(x - f.X) > f.HalfW) continue;
                if (_snowOnly && f is BoxFeature) continue;
                a += f.Add(x, z);
            }
            return a;
        }

        public override SurfaceKind SurfaceAt(float x, float z)
        {
            var list = FeaturesNear(z);
            if (list == null) return SurfaceKind.Snow;
            for (int i = 0; i < list.Count; i++)
            {
                var f = list[i];
                if (z < f.Z0 || z > f.Z1 || Mathf.Abs(x - f.X) > f.HalfW) continue;
                var s = f.Surface(x, z);
                if (s != SurfaceKind.Snow) return s;
            }
            return SurfaceKind.Snow;
        }

        public override float AlignAssist(float x, float z)
        {
            var list = FeaturesNear(z);
            if (list == null) return 0f;
            for (int i = 0; i < list.Count; i++)
                if (list[i] is Halfpipe hp && z > hp.ZA && z < hp.ZB && Mathf.Abs(x - hp.X) < hp.HalfWidth + 2f)
                    return M.Smooth((hp.WallT(x) - 0.12f) / 0.4f) * hp.Envelope(z);
            return 0f;
        }

        public override Vector3 AirAssist(Vector3 pos, Vector3 vel)
        {
            var list = FeaturesNear(pos.z);
            if (list == null) return Vector3.zero;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Halfpipe hp && pos.z > hp.ZA && pos.z < hp.ZB)
                {
                    // Above the lip or the deck: lean back in, so you come down on the wall you left.
                    float d = pos.x - hp.X;
                    float over = Mathf.Abs(d) - hp.HalfWidth * 0.86f;
                    if (over > 0f && Mathf.Abs(d) < hp.HalfWidth + 9f)
                        return new Vector3(-Mathf.Sign(d) * Mathf.Min(11f, 3.5f + over * 3f) * hp.Envelope(pos.z), 0f, 0f);
                }
            }
            return Vector3.zero;
        }
    }
}
