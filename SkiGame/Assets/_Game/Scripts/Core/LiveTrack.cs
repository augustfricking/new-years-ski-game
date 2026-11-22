using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ski.Core
{
    /// <summary>Where a live player is.</summary>
    public enum LiveMode : byte { Skiing = 0, Finished = 1, Tavern = 2 }

    /// <summary>
    /// Which part of the one shared world a live player is in: a slope (1..5, by <see cref="SlopeId"/>),
    /// the tavern, or the lift and the map. Past the finish of any slope they are in the village,
    /// which all the slopes share.
    /// </summary>
    public static class LivePlace
    {
        public const byte Unknown = 0, Tavern = 6, Lift = 7;
        public static byte Of(SlopeId s) => (byte)(1 + (int)s);
        public static bool IsSlope(byte p) => p >= 1 && p <= 5;
        public static SlopeId Slope(byte p) => (SlopeId)(p - 1);
    }

    [Flags]
    public enum LiveFlags : byte { None = 0, Tuck = 1, Plough = 2, Crashed = 4, Down = 8, Punch = 16, Sit = 32 }

    /// <summary>One moment of a live player: position (y is height off the snow, or off the floor), facing, state.</summary>
    public struct LiveSample
    {
        public Vector3 Pos;
        public float Yaw;          // degrees
        public LiveFlags Flags;
    }

    /// <summary>
    /// A short stretch of a live player's movement, as sent to the others in the room: eight
    /// samples a second, a couple of seconds at a time, in a few dozen bytes of text.
    /// </summary>
    public sealed class LivePacket
    {
        public const float Interval = 0.125f;
        public const int MaxSamples = 40;
        const byte Version = 1;

        public LiveMode Mode;
        public int Character;
        public byte Place;
        public int Seq;
        /// <summary>The sender's clock (seconds) at the first sample.</summary>
        public double Start;
        public readonly List<LiveSample> Samples = new List<LiveSample>();

        public double End => Start + Math.Max(0, Samples.Count - 1) * Interval;

        public string Encode()
        {
            int n = Math.Min(Samples.Count, MaxSamples);
            var b = new byte[11 + n * 7];
            b[0] = Version; b[1] = (byte)Mode; b[2] = (byte)Character;
            b[3] = (byte)(Seq & 255); b[4] = (byte)((Seq >> 8) & 255);
            uint ms = (uint)(Math.Max(0.0, Start) * 1000.0);
            b[5] = (byte)(ms & 255); b[6] = (byte)((ms >> 8) & 255); b[7] = (byte)((ms >> 16) & 255); b[8] = (byte)(ms >> 24);
            b[9] = (byte)n; b[10] = Place;
            for (int i = 0; i < n; i++)
            {
                var s = Samples[i];
                int x = Mathf.Clamp(Mathf.RoundToInt(s.Pos.x * 20f), short.MinValue, short.MaxValue);
                int z = Mathf.Clamp(Mathf.RoundToInt((s.Pos.z + 100f) * 20f), 0, ushort.MaxValue);
                int o = 11 + i * 7;
                b[o] = (byte)(x & 255); b[o + 1] = (byte)((x >> 8) & 255);
                b[o + 2] = (byte)(z & 255); b[o + 3] = (byte)(z >> 8);
                b[o + 4] = (byte)Mathf.Clamp(Mathf.RoundToInt(s.Pos.y * 10f), 0, 255);
                b[o + 5] = (byte)(Mathf.RoundToInt(Mathf.Repeat(s.Yaw, 360f) / 360f * 256f) & 255);
                b[o + 6] = (byte)s.Flags;
            }
            return Convert.ToBase64String(b);
        }

        /// <summary>Null if the text is not a packet this version understands.</summary>
        public static LivePacket Decode(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 2000) return null;
            byte[] b;
            try { b = Convert.FromBase64String(text); } catch (FormatException) { return null; }
            if (b.Length < 11 || b[0] != Version) return null;
            int n = b[9];
            if (n > MaxSamples || b.Length < 11 + n * 7 || b[1] > (byte)LiveMode.Tavern) return null;
            var p = new LivePacket
            {
                Mode = (LiveMode)b[1], Character = b[2], Place = b[10], Seq = b[3] | (b[4] << 8),
                Start = (b[5] | (b[6] << 8) | (b[7] << 16) | ((uint)b[8] << 24)) / 1000.0,
            };
            for (int i = 0; i < n; i++)
            {
                int o = 11 + i * 7;
                short x = (short)(b[o] | (b[o + 1] << 8));
                int z = b[o + 2] | (b[o + 3] << 8);
                p.Samples.Add(new LiveSample
                {
                    Pos = new Vector3(x / 20f, b[o + 4] / 10f, z / 20f - 100f),
                    Yaw = b[o + 5] / 256f * 360f,
                    Flags = (LiveFlags)b[o + 6],
                });
            }
            return p;
        }
    }

    /// <summary>
    /// Plays a live player back a little behind real time. Their movement comes two ways: straight
    /// from their browser (twenty samples a second, a tenth of a second old) when a direct line is
    /// open, and through the room every couple of seconds otherwise. Played back just behind the
    /// newest sample, the movement stays smooth: about 0.12 s behind with a direct line, 3.2 s
    /// without, sliding between the two (catching up, or easing back) rather than jumping. The
    /// sender's clock is mapped onto ours by the quickest delivery seen so far.
    /// </summary>
    public sealed class LiveTimeline
    {
        /// <summary>How far behind the sender we play: with a direct line, and through the room (one packet plus room for a late one).</summary>
        public const float DirectDelay = 0.12f, RoomDelay = 3.2f;
        /// <summary>How long a direct line may stay quiet before playback eases back to the room's pace.</summary>
        public const double DirectQuiet = 0.8;
        /// <summary>After this long without news a player is taken off the screen.</summary>
        public float Stale = 9f;

        struct Timed { public double T; public LiveSample S; public LiveMode Mode; }
        readonly List<Timed> _samples = new List<Timed>();
        // Our clock minus theirs, as each way delivers (the quickest delivery seen, nudged up by slower ones).
        double _offsetRoom = double.NaN, _offsetDirect = double.NaN;
        double _lastArrival = double.NegativeInfinity, _lastDirect = double.NegativeInfinity;
        double _lagAt = double.NaN, _lag;
        int _lastSeq = -1;
        double Offset => double.IsNaN(_offsetDirect) ? _offsetRoom : double.IsNaN(_offsetRoom) ? _offsetDirect : Math.Min(_offsetRoom, _offsetDirect);

        public int Count => _samples.Count;
        public LiveMode Mode { get; private set; }
        public int Character { get; private set; }
        /// <summary>Where they are (<see cref="LivePlace"/>), from their latest packet.</summary>
        public byte Place { get; private set; }
        /// <summary>Whether their movement is coming straight from their browser right now.</summary>
        public bool Direct(double now) => now - _lastDirect < DirectQuiet;

        /// <summary>Adds a packet from the room that arrived at local time <paramref name="now"/>. Returns false for an old or repeated one.</summary>
        public bool Add(LivePacket p, double now)
        {
            if (p == null || p.Samples.Count == 0) return false;
            int ahead = (p.Seq - _lastSeq) & 0xFFFF;
            if (_lastSeq >= 0 && (ahead == 0 || ahead > 0x8000)) return false;
            _lastSeq = p.Seq;
            Take(p, now, ref _offsetRoom);
            return true;
        }

        /// <summary>Adds samples that came straight from the sender's browser (numbered on their own, apart from the room's packets).</summary>
        public bool AddDirect(LivePacket p, double now)
        {
            if (p == null || p.Samples.Count == 0) return false;
            if (_samples.Count > 0 && p.End <= _samples[_samples.Count - 1].T + 1e-4) return false;   // late: something newer is here already
            _lastDirect = now;
            Take(p, now, ref _offsetDirect);
            return true;
        }

        void Take(LivePacket p, double now, ref double offset)
        {
            _lastArrival = now;
            Mode = p.Mode;
            Character = p.Character;
            Place = p.Place;
            double guess = now - p.End;
            // The quickest delivery is the truest; a slow one only nudges the estimate.
            if (double.IsNaN(offset) || guess < offset) offset = guess;
            else offset += (guess - offset) * 0.05;
            double newest = _samples.Count > 0 ? _samples[_samples.Count - 1].T : double.NegativeInfinity;
            for (int i = 0; i < p.Samples.Count; i++)
            {
                double t = p.Start + i * LivePacket.Interval;
                if (t <= newest + 1e-4) continue;
                _samples.Add(new Timed { T = t, S = p.Samples[i], Mode = p.Mode });
            }
            // Keep about half a minute.
            int drop = 0;
            double keep = _samples[_samples.Count - 1].T - 30.0;
            while (drop < _samples.Count - 2 && _samples[drop].T < keep) drop++;
            if (drop > 0) _samples.RemoveRange(0, drop);
        }

        /// <summary>
        /// How far our clock is ahead of the moment of theirs being shown at local time <paramref name="now"/>:
        /// the trip plus the direct line's short wait while it carries news, else the room's trip and wait.
        /// A change is spread out: catching up plays a little fast, easing back plays slower (never backwards).
        /// </summary>
        public double LagAt(double now)
        {
            double target = Direct(now) || double.IsNaN(_offsetRoom) ? _offsetDirect + DirectDelay : _offsetRoom + RoomDelay;
            if (!Direct(now) && double.IsNaN(_offsetRoom)) target = _offsetDirect + RoomDelay;   // only ever direct, now quiet: wait as the room would
            if (double.IsNaN(_lagAt)) { _lagAt = now; _lag = target; return _lag; }
            double dt = Math.Max(0.0, now - _lagAt);
            _lagAt = Math.Max(_lagAt, now);
            if (target < _lag) _lag = Math.Max(target, _lag - dt * 1.5);
            else if (target > _lag) _lag = Math.Min(target, _lag + dt * 0.7);
            // Never more than a moment past the newest sample: rather wait there for the next news than leap to it.
            if (_samples.Count > 0) _lag = Math.Max(_lag, now - (_samples[_samples.Count - 1].T + Coast));
            return _lag;
        }

        /// <summary>The sender's clock at the moment being shown at local time <paramref name="now"/> (NaN before any packet).</summary>
        public double PlaybackTime(double now) => double.IsNaN(Offset) ? double.NaN : now - LagAt(now);

        /// <summary>The sender's clock right now, as far as we can tell (NaN before any packet).</summary>
        public double SenderClock(double now) => double.IsNaN(Offset) ? double.NaN : now - Offset;

        /// <summary>True when there is something to show at local time <paramref name="now"/>.</summary>
        public bool Visible(double now) => _samples.Count > 0 && now - _lastArrival < Stale;

        /// <summary>How long past the newest sample movement is carried on along its last direction (a late sample on a direct line).</summary>
        const double Coast = 0.25;

        /// <summary>The player at local time <paramref name="now"/>; speed in metres a second along the ground.</summary>
        public bool At(double now, out LiveSample s, out float speed, out LiveMode mode)
        {
            s = default; speed = 0f; mode = Mode;
            if (_samples.Count == 0 || double.IsNaN(Offset)) return false;
            double t = now - LagAt(now);
            if (t <= _samples[0].T) { s = _samples[0].S; mode = _samples[0].Mode; return true; }
            int last = _samples.Count - 1;
            if (t >= _samples[last].T)
            {
                s = _samples[last].S; mode = _samples[last].Mode;
                // Just past the newest: carry on a moment along the last stretch, then hold.
                if (last > 0)
                {
                    var p = _samples[last - 1];
                    double span = Math.Max(_samples[last].T - p.T, 1e-4);
                    if (span < 0.3)
                    {
                        Vector3 v = (_samples[last].S.Pos - p.S.Pos) / (float)span;
                        v.y = 0f;
                        s.Pos += v * (float)Math.Min(t - _samples[last].T, Coast);
                        speed = v.magnitude;
                    }
                }
                return true;
            }
            int i = last - 1;
            while (i > 0 && _samples[i].T > t) i--;
            var a = _samples[i]; var b = _samples[i + 1];
            double h = Math.Max(b.T - a.T, 1e-4);
            float f = (float)((t - a.T) / h);
            // A curve through the samples with each one's velocity taken over its neighbours' times, so it stays
            // true where the spacing changes (the room's samples beside the direct line's, a gap between them).
            var pa = _samples[Math.Max(i - 1, 0)]; var pd = _samples[Math.Min(i + 2, last)];
            Vector3 va = (b.S.Pos - pa.S.Pos) / (float)Math.Max(b.T - pa.T, 1e-4), vb = (pd.S.Pos - a.S.Pos) / (float)Math.Max(pd.T - a.T, 1e-4);
            float f2 = f * f, f3 = f2 * f;
            Vector3 curve = (2f * f3 - 3f * f2 + 1f) * a.S.Pos + (f3 - 2f * f2 + f) * (float)h * va + (-2f * f3 + 3f * f2) * b.S.Pos + (f3 - f2) * (float)h * vb;
            s.Pos = new Vector3(curve.x, Mathf.Lerp(a.S.Pos.y, b.S.Pos.y, f), curve.z);
            s.Yaw = Mathf.LerpAngle(a.S.Yaw, b.S.Yaw, f);
            s.Flags = f < 0.5f ? a.S.Flags : b.S.Flags;
            mode = f < 0.5f ? a.Mode : b.Mode;
            Vector3 d = b.S.Pos - a.S.Pos; d.y = 0f;
            speed = d.magnitude / (float)Math.Max(b.T - a.T, 1e-4);
            return true;
        }

    }

    /// <summary>Collects the local player's samples and cuts them into packets.</summary>
    public sealed class LiveRecorder
    {
        /// <summary>How often a packet goes out (the room service allows one update a second; this leaves room for punches).</summary>
        public float PacketSeconds = 2f;

        readonly LivePacket _open = new LivePacket();
        double _next = double.NaN, _lastT;
        LiveSample _last;
        /// <summary>Packet numbers run on across scenes, so somebody met again is not mistaken for an old packet.</summary>
        static int _seq;

        public LiveMode Mode;
        public int Character;
        public byte Place;

        /// <summary>
        /// Call every frame with the local clock. Returns an encoded packet when one is due (or
        /// when <paramref name="flush"/> asks for it now), else null.
        /// </summary>
        public string Tick(double now, LiveSample sample, bool flush = false)
        {
            if (double.IsNaN(_next)) { _next = now; _open.Start = now; _last = sample; _lastT = now; }
            while (now >= _next)
            {
                // Exactly on the mark, between the last frame and this one.
                float f = now > _lastT ? (float)((_next - _lastT) / (now - _lastT)) : 1f;
                f = Mathf.Clamp01(f);
                var at = sample;
                at.Pos = Vector3.Lerp(_last.Pos, sample.Pos, f);
                at.Yaw = Mathf.LerpAngle(_last.Yaw, sample.Yaw, f);
                at.Flags = _last.Flags | sample.Flags;
                if (_open.Samples.Count == 0) _open.Start = _next;
                _open.Samples.Add(at);
                _next += LivePacket.Interval;
                if (_open.Samples.Count >= LivePacket.MaxSamples) { _next = Math.Max(_next, now); break; }
            }
            _last = sample; _lastT = now;
            float span = _open.Samples.Count * LivePacket.Interval;
            if (_open.Samples.Count == 0 || (!flush && span < PacketSeconds)) return null;
            _open.Mode = Mode; _open.Character = Character; _open.Place = Place; _open.Seq = _seq = (_seq + 1) & 0xFFFF;
            string text = _open.Encode();
            _open.Samples.Clear();
            return text;
        }
    }
}
