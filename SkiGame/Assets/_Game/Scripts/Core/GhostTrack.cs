using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ski.Core
{
    /// <summary>
    /// A recording of one run, small enough to store with a leaderboard score: where the skier was
    /// twice a second and how high off the snow. Played back as a ghost on the slope.
    /// </summary>
    public sealed class GhostTrack
    {
        public const float Interval = 0.5f;
        public struct Sample { public float X, Z, Air; }

        readonly List<Sample> _samples = new List<Sample>();
        public int Count => _samples.Count;
        public float Duration => Mathf.Max(0, _samples.Count - 1) * Interval;
        public Sample this[int i] => _samples[Mathf.Clamp(i, 0, _samples.Count - 1)];

        /// <summary>Call every step with the run clock; a sample is kept whenever the next half second passes.</summary>
        public void Record(float time, float x, float z, float air)
        {
            while (time >= _samples.Count * Interval && _samples.Count < 2400)
                _samples.Add(new Sample { X = x, Z = z, Air = Mathf.Max(air, 0f) });
        }

        /// <summary>Where the ghost is at a time, smoothly between samples. False once the recording has ended.</summary>
        public bool At(float time, out Vector3 flat, out float air, out float heading, out float speed)
        {
            flat = default; air = 0f; heading = 0f; speed = 0f;
            if (_samples.Count < 2) return false;
            float f = Mathf.Clamp(time / Interval, 0f, _samples.Count - 1.001f);
            int i = (int)f;
            float t = f - i;
            Sample p0 = this[i - 1], p1 = this[i], p2 = this[i + 1], p3 = this[i + 2];
            float x = Spline(p0.X, p1.X, p2.X, p3.X, t), z = Spline(p0.Z, p1.Z, p2.Z, p3.Z, t);
            float dx = SplineSlope(p0.X, p1.X, p2.X, p3.X, t) / Interval, dz = SplineSlope(p0.Z, p1.Z, p2.Z, p3.Z, t) / Interval;
            flat = new Vector3(x, 0f, z);
            air = Mathf.Lerp(p1.Air, p2.Air, t);
            speed = Mathf.Sqrt(dx * dx + dz * dz);
            heading = speed > 0.3f ? Mathf.Atan2(dx, dz) : 0f;
            return time <= Duration + 0.01f;
        }

        static float Spline(float a, float b, float c, float d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * b + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (-a + 3f * b - 3f * c + d) * t3);
        }

        static float SplineSlope(float a, float b, float c, float d, float t)
        {
            return 0.5f * ((-a + c) + 2f * (2f * a - 5f * b + 4f * c - d) * t + 3f * (-a + 3f * b - 3f * c + d) * t * t);
        }

        // ---- storage: 5 bytes per sample, as text ----------------------------------------------

        public string Encode()
        {
            var bytes = new byte[3 + _samples.Count * 5];
            bytes[0] = 1;
            bytes[1] = (byte)(_samples.Count & 255); bytes[2] = (byte)(_samples.Count >> 8);
            for (int i = 0; i < _samples.Count; i++)
            {
                int x = Mathf.Clamp(Mathf.RoundToInt(_samples[i].X * 20f), short.MinValue, short.MaxValue);
                int z = Mathf.Clamp(Mathf.RoundToInt((_samples[i].Z + 100f) * 20f), 0, ushort.MaxValue);
                int o = 3 + i * 5;
                bytes[o] = (byte)(x & 255); bytes[o + 1] = (byte)((x >> 8) & 255);
                bytes[o + 2] = (byte)(z & 255); bytes[o + 3] = (byte)(z >> 8);
                bytes[o + 4] = (byte)Mathf.Clamp(Mathf.RoundToInt(_samples[i].Air * 10f), 0, 255);
            }
            return Convert.ToBase64String(bytes);
        }

        /// <summary>Null if the text is not a recording.</summary>
        public static GhostTrack Decode(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(text); } catch (FormatException) { return null; }
            if (bytes.Length < 3 || bytes[0] != 1) return null;
            int n = bytes[1] | (bytes[2] << 8);
            if (n < 2 || bytes.Length < 3 + n * 5) return null;
            var g = new GhostTrack();
            for (int i = 0; i < n; i++)
            {
                int o = 3 + i * 5;
                short x = (short)(bytes[o] | (bytes[o + 1] << 8));
                int z = bytes[o + 2] | (bytes[o + 3] << 8);
                g._samples.Add(new Sample { X = x / 20f, Z = z / 20f - 100f, Air = bytes[o + 4] / 10f });
            }
            return g;
        }
    }
}
