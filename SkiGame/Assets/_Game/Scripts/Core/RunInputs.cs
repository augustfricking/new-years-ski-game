using System;
using System.Collections.Generic;

namespace Ski.Core
{
    /// <summary>
    /// A run as the player played it: the keys of every simulation step (120 a second) from the start
    /// gate to the finish, and when they pressed R to go back on course. The simulation is
    /// deterministic, so playing these into a new <see cref="RunSession"/> on the same slope with the
    /// same skier repeats the run exactly: the line, the crashes, the kids who were hit, the tricks.
    /// Stored as stretches of identical steps (the keys change a few times a second): from a few
    /// hundred bytes to a couple of KB.
    /// </summary>
    public sealed class RunInputs
    {
        /// <summary>
        /// Bump whenever the simulation or a course changes in a way that moves a run: a recording made
        /// under another version would ski its own way, so it is not played (the replay falls back on the
        /// position track).
        /// </summary>
        public const int SimVersion = 1;
        const byte Format = 2;          // GhostTrack's text starts with 1
        public const int MaxSteps = 120 * 60 * 10;

        public int Version { get; private set; } = SimVersion;
        public int Steps { get; private set; }
        readonly List<ushort> _codes = new List<ushort>();
        readonly List<int> _counts = new List<int>();
        bool _respawnNext;

        /// <summary>Whether this recording can be played into this version of the game.</summary>
        public bool Playable => Version == SimVersion && Steps > 0;

        /// <summary>The player pressed R (back on course): it happens before the next recorded step.</summary>
        public void Respawn() => _respawnNext = true;

        /// <summary>Records one step's input (as it will be played back: see <see cref="Quantize"/>).</summary>
        public void Record(in SkiInput input)
        {
            if (Steps >= MaxSteps) return;
            ushort code = Pack(input, _respawnNext);
            _respawnNext = false;
            int last = _codes.Count - 1;
            if (last >= 0 && _codes[last] == code) _counts[last]++;
            else { _codes.Add(code); _counts.Add(1); }
            Steps++;
        }

        /// <summary>The input exactly as a recording keeps it (keys are on or off; steering is left, none or right).</summary>
        public static SkiInput Quantize(in SkiInput input) => Unpack(Pack(input, false), out _);

        static ushort Pack(in SkiInput i, bool respawn)
        {
            int steer = i.Steer < -0.5f ? 1 : i.Steer > 0.5f ? 2 : 0;
            int c = steer | (i.Tuck ? 4 : 0) | (i.Brake ? 8 : 0) | (i.Jump ? 16 : 0) | (i.Pole ? 32 : 0) | (respawn ? 64 : 0);
            c |= (Math.Min(Math.Max(i.Trick, 0), 7) & 7) << 8;
            c |= (Math.Min(Math.Max(i.Variant, 0), 3) & 3) << 11;
            return (ushort)c;
        }

        static SkiInput Unpack(ushort c, out bool respawn)
        {
            respawn = (c & 64) != 0;
            int steer = c & 3;
            return new SkiInput
            {
                Steer = steer == 1 ? -1f : steer == 2 ? 1f : 0f,
                Tuck = (c & 4) != 0, Brake = (c & 8) != 0, Jump = (c & 16) != 0, Pole = (c & 32) != 0,
                Trick = (c >> 8) & 7, Variant = (c >> 11) & 3,
            };
        }

        /// <summary>Plays a recording back step by step.</summary>
        public sealed class Reader
        {
            readonly RunInputs _r;
            int _run, _left = -1;
            public int Step { get; private set; }
            public Reader(RunInputs r) { _r = r; }
            public bool Done => Step >= _r.Steps;

            /// <summary>The next step's input, and whether R was pressed just before it. False at the end.</summary>
            public bool Next(out SkiInput input, out bool respawn)
            {
                input = default; respawn = false;
                if (Done) return false;
                if (_left <= 0) { if (_left == 0) _run++; _left = _r._counts[_run]; }
                ushort code = _r._codes[_run];
                input = Unpack(code, out respawn);
                _left--;
                Step++;
                return true;
            }
        }

        /// <summary>Plays the next step of <paramref name="reader"/> into a session (R first, when it was pressed).</summary>
        public static bool StepInto(RunSession s, Reader reader)
        {
            if (!reader.Next(out var input, out bool respawn)) return false;
            if (respawn) s.Respawn();
            s.Step(input, SkiSim.FixedDt);
            return true;
        }

        // ---- storage: base64 text; a stretch is its code (one byte, or two with a trick held) and its length --

        public string Encode()
        {
            var b = new List<byte>(16 + _codes.Count * 3) { Format, (byte)Version };
            b.Add((byte)(Steps & 255)); b.Add((byte)((Steps >> 8) & 255)); b.Add((byte)((Steps >> 16) & 255));
            for (int i = 0; i < _codes.Count; i++)
            {
                ushort c = _codes[i];
                bool ext = (c >> 8) != 0;
                b.Add((byte)((c & 127) | (ext ? 128 : 0)));
                if (ext) b.Add((byte)(c >> 8));
                uint n = (uint)_counts[i];
                while (n >= 128) { b.Add((byte)(n & 127 | 128)); n >>= 7; }
                b.Add((byte)n);
            }
            return Convert.ToBase64String(b.ToArray());
        }

        /// <summary>Null if the text is not an input recording.</summary>
        public static RunInputs Decode(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 40000) return null;
            byte[] b;
            try { b = Convert.FromBase64String(text); } catch (FormatException) { return null; }
            if (b.Length < 5 || b[0] != Format) return null;
            var r = new RunInputs { Version = b[1] };
            int steps = b[2] | (b[3] << 8) | (b[4] << 16), total = 0;
            int o = 5;
            while (o < b.Length)
            {
                int c = b[o] & 127;
                bool ext = (b[o] & 128) != 0;
                o++;
                if (ext) { if (o >= b.Length) return null; c |= b[o] << 8; o++; }
                uint n = 0; int shift = 0;
                while (true)
                {
                    if (o >= b.Length || shift > 28) return null;
                    byte v = b[o++];
                    n |= (uint)(v & 127) << shift;
                    if ((v & 128) == 0) break;
                    shift += 7;
                }
                if (n == 0 || n > MaxSteps) return null;
                r._codes.Add((ushort)c); r._counts.Add((int)n);
                total += (int)n;
            }
            if (total != steps || total > MaxSteps) return null;
            r.Steps = total;
            return r;
        }
    }
}
