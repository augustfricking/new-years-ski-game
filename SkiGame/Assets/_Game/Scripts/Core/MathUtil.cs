using UnityEngine;

namespace Ski.Core
{
    /// <summary>Small deterministic random generator (xorshift). Never use UnityEngine.Random in Core.</summary>
    public sealed class Rng
    {
        uint _s;
        public Rng(int seed) { _s = (uint)seed * 2654435761u + 0x9E3779B9u; if (_s == 0) _s = 1; Next(); Next(); }
        public uint Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
        /// <summary>Uniform in [0,1).</summary>
        public float Value() => (Next() & 0xFFFFFF) / 16777216f;
        public float Range(float a, float b) => a + (b - a) * Value();
        public int Range(int a, int bExclusive) => a + (int)(Next() % (uint)Mathf.Max(1, bExclusive - a));
        public bool Chance(float p) => Value() < p;
        public float Sign() => Value() < 0.5f ? -1f : 1f;
    }

    public static class Noise
    {
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)(seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        /// <summary>Smooth value noise in [0,1], continuous second derivative.</summary>
        public static float Value(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = Fade(x - xi), fy = Fade(y - yi);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
            float c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Two octaves, centred on zero, range about [-1,1].</summary>
        public static float Fbm(float x, float y, int seed)
        {
            float v = Value(x, y, seed) * 2f - 1f;
            v += (Value(x * 2.13f + 17.3f, y * 2.13f - 9.1f, seed + 7) * 2f - 1f) * 0.45f;
            return v / 1.45f;
        }
    }

    public static class M
    {
        public static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static float Smooth(float a, float b, float x) => Smooth((x - a) / (b - a));

        /// <summary>Wraps an angle to (-pi, pi].</summary>
        public static float WrapPi(float a)
        {
            a %= 2f * Mathf.PI;
            if (a > Mathf.PI) a -= 2f * Mathf.PI;
            else if (a <= -Mathf.PI) a += 2f * Mathf.PI;
            return a;
        }

        public static float MoveTo(float v, float target, float maxDelta) => Mathf.MoveTowards(v, target, maxDelta);

        /// <summary>Distance from point p to segment ab, all in the XZ plane.</summary>
        public static float DistToSegment(float px, float pz, float ax, float az, float bx, float bz, out float t)
        {
            float dx = bx - ax, dz = bz - az;
            float len2 = dx * dx + dz * dz;
            t = len2 > 1e-6f ? Mathf.Clamp01(((px - ax) * dx + (pz - az) * dz) / len2) : 0f;
            float cx = ax + dx * t - px, cz = az + dz * t - pz;
            return Mathf.Sqrt(cx * cx + cz * cz);
        }
    }
}
