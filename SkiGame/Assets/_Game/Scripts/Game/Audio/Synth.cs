using System;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// Makes every sound effect, ambience loop and music track from maths at run time, so the game
    /// ships no audio files except the recorded voices. Deterministic. The palette is deliberately
    /// soft: snow thumps, wooden ticks and mallet notes rather than buzzers and beeps.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 22050;
        static uint _seed = 12345;

        static float Rnd() { _seed ^= _seed << 13; _seed ^= _seed >> 17; _seed ^= _seed << 5; return (_seed & 0xFFFFFF) / 8388608f - 1f; }
        static float Rnd01() => Rnd() * 0.5f + 0.5f;

        /// <summary>Sounds worth generating before they are first needed, in rough order of use.</summary>
        public static readonly string[] WarmList =
        {
            "click", "hover", "wind_a", "wind_b", "carve", "skid", "pole", "pole_perfect", "jump", "land", "land_heavy",
            "crash", "gate", "gate_miss", "trick", "trick_big", "finish", "penalty", "paper", "music_ski",
        };

        public static AudioClip Make(string id)
        {
            _seed = (uint)(StableHash(id) | 1);
            float[] d;
            switch (id)
            {
                case "wind_a": d = WindLoop(19f, 420f, 0.9f); break;
                case "wind_b": d = WindLoop(13f, 900f, 0.6f); break;
                case "carve": d = SkiLoop(3.7f, false); break;
                case "skid": d = SkiLoop(2.9f, true); break;
                case "click": d = Tock(0.07f, 520f, 0.35f); break;
                case "hover": d = Tock(0.035f, 760f, 0.16f); break;
                case "pole": d = Mix(SnowBurst(0.11f, 1700f, 0.4f), Thump(0.09f, 110f, 0.3f)); break;
                case "pole_perfect": d = Mix(Mix(SnowBurst(0.11f, 1700f, 0.4f), Thump(0.09f, 110f, 0.3f)), Mallet(new[] { 880f }, 0f, 0.3f, 0.16f)); break;
                case "jump": d = Whoosh(0.3f, 300f, 1500f, 0.3f); break;
                case "land": d = Mix(Thump(0.2f, 72f, 0.6f), SnowBurst(0.2f, 900f, 0.35f)); break;
                case "land_heavy": d = Mix(Thump(0.32f, 55f, 0.8f), SnowBurst(0.3f, 700f, 0.5f)); break;
                case "crash": d = Crash(); break;
                case "gate": d = Tock(0.09f, 690f, 0.3f); break;
                case "gate_miss": d = Mallet(new[] { 311f, 233f }, 0.11f, 0.4f, 0.3f); break;
                case "penalty": d = Mallet(new[] { 262f, 196f }, 0.13f, 0.45f, 0.32f); break;
                case "trick": d = Mallet(new[] { 659f, 784f, 988f }, 0.075f, 0.45f, 0.26f); break;
                case "trick_big": d = Mallet(new[] { 659f, 784f, 988f, 1175f, 1319f }, 0.075f, 0.6f, 0.28f); break;
                case "finish": d = Mallet(new[] { 523f, 659f, 784f, 1047f, 784f, 1047f, 1319f }, 0.13f, 0.9f, 0.3f); break;
                case "scream": d = Scream(0.85f); break;
                case "fw_launch": d = Whistle(0.9f); break;
                case "fw_boom": d = Boom(1.6f); break;
                case "fw_crackle": d = Crackle(1.2f); break;
                case "door": d = Mix(Thump(0.25f, 90f, 0.6f), SnowBurst(0.12f, 600f, 0.25f)); break;
                case "gulp": d = Gulp(); break;
                case "spill": d = Spill(); break;
                case "clink": d = Chime(new[] { 2350f, 3180f, 4700f }, 0.5f, 0.25f); break;
                case "burp": d = Burp(); break;
                case "cheer": d = Cheer(2.6f); break;
                case "paper": d = SnowBurst(0.28f, 3200f, 0.22f); break;
                case "lift": d = LiftLoop(6f); break;
                case "crowd": d = CrowdLoop(9f); break;
                case "fire": d = FireLoop(5f); break;
                case "music_menu": d = MenuMusic(); break;
                case "music_ski": d = SkiMusic(); break;
                case "music_tavern": d = TavernSong(); break;
                default: Debug.LogWarning("Synth: unknown sound " + id); return null;
            }
            var clip = AudioClip.Create(id, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        static int StableHash(string s)
        {
            unchecked { int h = 23; foreach (char c in s) h = h * 31 + c; return h; }
        }

        // ---- building blocks -----------------------------------------------------------------

        static float[] Buf(float seconds) => new float[Mathf.Max(1, (int)(seconds * Rate))];

        static float[] Mix(float[] a, float[] b)
        {
            var o = new float[Mathf.Max(a.Length, b.Length)];
            for (int i = 0; i < a.Length; i++) o[i] += a[i];
            for (int i = 0; i < b.Length; i++) o[i] += b[i];
            return o;
        }

        static void LowPass(float[] d, float hz)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * hz / Rate), y = 0f;
            for (int i = 0; i < d.Length; i++) { y += a * (d[i] - y); d[i] = y; }
        }

        static void HighPass(float[] d, float hz)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * hz / Rate), y = 0f;
            for (int i = 0; i < d.Length; i++) { y += a * (d[i] - y); d[i] -= y; }
        }

        /// <summary>Cross-fades the tail into the head so a loop has no click.</summary>
        static float[] Looped(float[] d, float fade)
        {
            int n = Mathf.Min((int)(fade * Rate), d.Length / 2);
            var o = new float[d.Length - n];
            for (int i = 0; i < o.Length; i++) o[i] = d[i];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                o[i] = d[i] * t + d[d.Length - n + i] * (1f - t);
            }
            return o;
        }

        static void Normalise(float[] d, float peak)
        {
            float m = 1e-5f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));
        static float Tri(float phase) => 2f * Mathf.Abs(Saw(phase)) - 1f;
        static float Sin(float phase) => Mathf.Sin(phase * 2f * Mathf.PI);

        /// <summary>Slow random curve in 0..1 (smooth value noise), for gusts and swells.</summary>
        static float Drift(float t, float seed)
        {
            float x = t + seed * 17.3f;
            int i = Mathf.FloorToInt(x);
            float f = x - i; f = f * f * (3f - 2f * f);
            float a = Mathf.Abs(Mathf.Sin(i * 127.1f + seed * 311.7f) * 43758.5453f) % 1f;
            float b = Mathf.Abs(Mathf.Sin((i + 1) * 127.1f + seed * 311.7f) * 43758.5453f) % 1f;
            return Mathf.Lerp(a, b, f);
        }

        // ---- effects -------------------------------------------------------------------------

        /// <summary>A soft wooden tick.</summary>
        static float[] Tock(float sec, float hz, float vol)
        {
            var d = Buf(sec); float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                ph += hz * (1.15f - 0.3f * t) / Rate;
                d[i] = (Sin(ph) + 0.25f * Sin(ph * 2.7f)) * Mathf.Exp(-t * 7f) * vol * Mathf.Min(1f, i / 25f);
            }
            LowPass(d, 2600f);
            return d;
        }

        /// <summary>Marimba-like notes, one after another.</summary>
        static float[] Mallet(float[] notes, float step, float tail, float vol)
        {
            var d = Buf(step * (notes.Length - 1) + tail);
            for (int n = 0; n < notes.Length; n++)
            {
                int at = (int)(n * step * Rate), len = (int)(tail * Rate);
                for (int i = 0; i < len && at + i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float s = Sin(notes[n] * t) + 0.3f * Sin(notes[n] * 4f * t) * Mathf.Exp(-t * 30f);
                    d[at + i] += s * Mathf.Exp(-t * 8f) * vol * Mathf.Min(1f, i / 20f);
                }
            }
            LowPass(d, 4200f);
            return d;
        }

        /// <summary>Snow being pushed: a short soft hiss.</summary>
        static float[] SnowBurst(float sec, float lp, float vol)
        {
            var d = Buf(sec);
            for (int i = 0; i < d.Length; i++) { float t = i / (float)d.Length; d[i] = Rnd() * Mathf.Sin(Mathf.Min(1f, t * 6f) * Mathf.PI * 0.5f) * (1f - t) * (1f - t); }
            LowPass(d, lp); LowPass(d, lp * 1.6f);
            HighPass(d, 180f);
            Normalise(d, vol);
            return d;
        }

        static float[] Thump(float sec, float hz, float vol)
        {
            var d = Buf(sec); float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                ph += hz * (1.5f - t) / Rate;
                d[i] = Sin(ph) * Mathf.Exp(-5f * t) * vol * Mathf.Min(1f, i / 30f);
            }
            return d;
        }

        static float[] Whoosh(float sec, float f0, float f1, float vol)
        {
            var d = Buf(sec); float y = 0f, y2 = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Lerp(f0, f1, t) / Rate);
                y += a * (Rnd() - y); y2 += a * (y - y2);
                d[i] = y2 * Mathf.Sin(t * Mathf.PI);
            }
            HighPass(d, 200f);
            Normalise(d, vol);
            return d;
        }

        static float[] Crash()
        {
            var o = new float[(int)(1.0f * Rate)];
            var first = Mix(SnowBurst(0.45f, 1100f, 0.6f), Thump(0.3f, 58f, 0.8f));
            Array.Copy(first, o, Mathf.Min(first.Length, o.Length));
            for (int k = 0; k < 3; k++)
            {
                var t = Mix(Thump(0.16f, 64f + k * 9f, 0.5f - k * 0.12f), SnowBurst(0.14f, 800f, 0.25f));
                int at = (int)((0.26f + k * 0.2f) * Rate);
                for (int i = 0; i < t.Length && at + i < o.Length; i++) o[at + i] += t[i];
            }
            Normalise(o, 0.75f);
            return o;
        }

        static float[] Chime(float[] freqs, float sec, float vol)
        {
            var d = Buf(sec);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate, s = 0f;
                for (int f = 0; f < freqs.Length; f++) s += Mathf.Sin(2f * Mathf.PI * freqs[f] * t) * Mathf.Exp(-t * (7f + f * 3f)) / (f + 1);
                d[i] = s * vol * Mathf.Min(1f, i / 20f);
            }
            return d;
        }

        static float[] Scream(float sec)
        {
            // A child going "aaaah" some way off: softened, with breath.
            var d = Buf(sec); float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                float f = Mathf.Lerp(700f, 900f, Mathf.Sin(t * Mathf.PI)) * (1f + 0.03f * Mathf.Sin(i * 2f * Mathf.PI * 8f / Rate));
                ph += f / Rate;
                float env = Mathf.Min(1f, t * 9f) * Mathf.Min(1f, (1f - t) * 4f);
                d[i] = (Tri(ph) * 0.6f + Sin(ph * 2f) * 0.2f + Rnd() * 0.06f) * env;
            }
            LowPass(d, 2400f); HighPass(d, 450f);
            Normalise(d, 0.4f);
            return d;
        }

        static float[] Whistle(float sec)
        {
            var d = Buf(sec); float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                ph += Mathf.Lerp(500f, 2200f, t * t) / Rate;
                d[i] = (Sin(ph) * 0.4f + Rnd() * 0.2f * t) * Mathf.Min(1f, t * 10f) * (1f - t * 0.4f);
            }
            HighPass(d, 400f); LowPass(d, 3500f);
            Normalise(d, 0.3f);
            return d;
        }

        static float[] Boom(float sec)
        {
            var d = Buf(sec);
            for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; d[i] = Rnd() * Mathf.Exp(-t * 3.2f); }
            LowPass(d, 380f);
            var body = Thump(0.5f, 48f, 1f);
            for (int i = 0; i < body.Length; i++) d[i] += body[i] * 0.9f;
            Normalise(d, 0.85f);
            return d;
        }

        static float[] Crackle(float sec)
        {
            var d = Buf(sec);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                if (Rnd01() < 0.012f * (1f - t)) { int n = Mathf.Min(40, d.Length - i); for (int k = 0; k < n; k++) d[i + k] += Rnd() * (1f - k / (float)n); }
            }
            HighPass(d, 1500f); LowPass(d, 5000f);
            Normalise(d, 0.3f);
            return d;
        }

        static float[] Gulp()
        {
            var d = Buf(0.18f); float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                ph += Mathf.Lerp(420f, 170f, t) / Rate;
                d[i] = Sin(ph) * Mathf.Sin(t * Mathf.PI) * 0.6f;
            }
            return d;
        }

        static float[] Spill()
        {
            // Liquid slopping on wood: a few quick pitched drips over a splash.
            var d = SnowBurst(0.3f, 2400f, 0.3f);
            for (int k = 0; k < 4; k++)
            {
                int at = (int)((0.03f + k * 0.055f) * Rate); float ph = 0f; float f0 = 900f + Rnd01() * 700f;
                for (int i = 0; i < 0.05f * Rate && at + i < d.Length; i++)
                {
                    float t = i / (0.05f * Rate);
                    ph += Mathf.Lerp(f0, f0 * 1.6f, t) / Rate;
                    d[at + i] += Sin(ph) * (1f - t) * 0.25f;
                }
            }
            return d;
        }

        static float[] Burp()
        {
            var d = Buf(0.5f); float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                ph += (85f + 30f * Mathf.Sin(t * 14f) + Rnd() * 10f) / Rate;
                d[i] = Saw(ph) * Mathf.Sin(t * Mathf.PI) * (0.7f + 0.3f * Mathf.Sin(t * 60f));
            }
            LowPass(d, 800f);
            Normalise(d, 0.55f);
            return d;
        }

        /// <summary>A room full of people going "wooo!" with clapping.</summary>
        static float[] Cheer(float sec)
        {
            var d = Buf(sec);
            for (int v = 0; v < 18; v++)
            {
                float f = 260f + Rnd01() * 420f, start = Rnd01() * 0.35f, ph = 0f, vib = 4f + Rnd01() * 5f, y = 0f;
                float len = sec - start - Rnd01() * 0.6f;
                for (int i = (int)(start * Rate); i < d.Length; i++)
                {
                    float t = (i / (float)Rate - start) / len;
                    if (t > 1f) break;
                    // "Wooo": the pitch swoops up and settles, the vowel opens.
                    ph += f * (0.75f + 0.45f * Mathf.Sin(Mathf.Clamp01(t * 2.2f) * Mathf.PI * 0.5f) + 0.03f * Mathf.Sin(i * vib * 6.28f / Rate)) / Rate;
                    float formant = 500f + 500f * Mathf.Clamp01(t * 3f);
                    float a = 1f - Mathf.Exp(-2f * Mathf.PI * formant / Rate);
                    y += a * (Saw(ph) - y);
                    d[i] += y * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.22f;
                }
            }
            // Clapping: lots of little bursts.
            for (int cl = 0; cl < 70; cl++)
            {
                int at = (int)(Rnd01() * (sec - 0.1f) * Rate);
                float vol = 0.15f + Rnd01() * 0.2f;
                for (int i = 0; i < 260 && at + i < d.Length; i++) d[at + i] += Rnd() * (1f - i / 260f) * vol * Mathf.Sin(Mathf.Clamp01(at / (sec * Rate)) * Mathf.PI);
            }
            LowPass(d, 3600f); HighPass(d, 280f);
            Normalise(d, 0.6f);
            return d;
        }

        // ---- loops ---------------------------------------------------------------------------

        /// <summary>
        /// Wind: filtered noise whose brightness and loudness wander at random, so no gust repeats
        /// in an obvious rhythm. Two of these with different lengths are layered and modulated again
        /// at run time.
        /// </summary>
        static float[] WindLoop(float sec, float centre, float gustiness)
        {
            var d = Buf(sec + 1.5f);
            float y1 = 0f, y2 = 0f, seed = Rnd01() * 50f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float gust = Drift(t * 0.55f, seed) * 0.6f + Drift(t * 1.9f, seed + 3f) * 0.4f;
                float cutoff = centre * (0.45f + 1.3f * gust);
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
                y1 += a * (Rnd() - y1); y2 += a * (y1 - y2);
                d[i] = y2 * (1f - gustiness + gustiness * (0.25f + 0.75f * Drift(t * 0.8f, seed + 9f)));
            }
            HighPass(d, 110f);
            d = Looped(d, 1.5f);
            Normalise(d, 0.7f);
            return d;
        }

        /// <summary>Skis on snow. The clean edge is a smooth hiss; the skid is a rough, chattering scrape.</summary>
        static float[] SkiLoop(float sec, bool harsh)
        {
            var d = Buf(sec + 0.5f);
            float seed = Rnd01() * 30f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float grain = harsh
                    ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(t * 47f + 3f * Drift(t * 9f, seed))) * (0.6f + 0.4f * Drift(t * 14f, seed + 2f))
                    : 0.8f + 0.2f * Drift(t * 3f, seed);
                d[i] = Rnd() * grain;
            }
            if (harsh) { LowPass(d, 3300f); HighPass(d, 650f); }
            else { LowPass(d, 2400f); LowPass(d, 3600f); HighPass(d, 900f); }
            d = Looped(d, 0.5f);
            Normalise(d, harsh ? 0.7f : 0.55f);
            return d;
        }

        /// <summary>The lift: a low electric hum that swells a little. No clatter.</summary>
        static float[] LiftLoop(float sec)
        {
            var d = Buf(sec);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float hum = Sin(50f * t) * 0.5f + Sin(100f * t) * 0.2f + Sin(150f * t) * 0.07f;
                d[i] = hum * (0.75f + 0.25f * Mathf.Sin(t * 2f * Mathf.PI / sec * 2f)) * 0.35f;
            }
            return d;
        }

        static float[] CrowdLoop(float sec)
        {
            // Babble: many voices, each a buzzy tone through a moving formant, chopped into syllables.
            var d = Buf(sec + 0.8f);
            for (int v = 0; v < 26; v++)
            {
                float f0 = 95f + Rnd01() * 150f, rate = 2.5f + Rnd01() * 3f, phase = Rnd01() * 10f, ph = 0f, y = 0f, gain = 0.4f + Rnd01() * 0.6f;
                float talk = Rnd01() * 6f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float syll = Mathf.Max(0f, Mathf.Sin(t * rate * 6.28f + phase)) * Mathf.Clamp01(Mathf.Sin(t * 0.9f + talk) * 2f + 0.6f);
                    ph += f0 * (1f + 0.08f * Mathf.Sin(t * 1.7f + phase)) / Rate;
                    float formant = 500f + 900f * (0.5f + 0.5f * Mathf.Sin(t * rate * 2.1f + phase * 3f));
                    float a = 1f - Mathf.Exp(-2f * Mathf.PI * formant / Rate);
                    y += a * (Saw(ph) - y);
                    d[i] += y * syll * gain;
                }
            }
            HighPass(d, 180f); LowPass(d, 3800f);
            d = Looped(d, 0.8f);
            Normalise(d, 0.5f);
            return d;
        }

        static float[] FireLoop(float sec)
        {
            var d = Buf(sec + 0.6f);
            float y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * (180f + 220f * Drift(t * 2f, 4f)) / Rate);
                y += a * (Rnd() - y);
                d[i] = y * 1.6f;
                if (Rnd01() < 0.0009f) { int n = Mathf.Min(30 + (int)(Rnd01() * 60f), d.Length - i); for (int k = 0; k < n; k++) d[i + k] += Rnd() * (1f - k / (float)n) * 0.8f; }
            }
            d = Looped(d, 0.6f);
            Normalise(d, 0.5f);
            return d;
        }

        // ---- music ---------------------------------------------------------------------------

        static float Note(int semitonesFromA4) => 440f * Mathf.Pow(2f, semitonesFromA4 / 12f);

        /// <summary>wave: 0 sine, 1 detuned saws (pad/accordion), 2 soft square (lead), 3 warm bass, 4 pluck.</summary>
        static void AddTone(float[] d, float start, float len, float hz, float vol, int wave, float decay)
        {
            int at = (int)(start * Rate), n = (int)(len * Rate);
            for (int i = 0; i < n && at + i < d.Length; i++)
            {
                float t = i / (float)Rate, ph = hz * t;
                float s;
                switch (wave)
                {
                    case 0: s = Sin(ph); break;
                    case 1: s = Saw(ph) * 0.5f + Saw(ph * 1.006f) * 0.5f; break;
                    case 2: s = Tri(ph) * 0.8f + Sin(ph * 2f) * 0.2f; break;
                    case 3: s = Sin(ph) + 0.4f * Sin(ph * 2f) + 0.2f * Sin(ph * 3f); break;
                    default: s = Tri(ph) * Mathf.Exp(-t * 9f) + 0.5f * Sin(ph) * Mathf.Exp(-t * 4f); break;
                }
                float env = Mathf.Min(1f, i / (0.006f * Rate)) * Mathf.Exp(-t * decay) * Mathf.Min(1f, (n - i) / (0.02f * Rate));
                d[at + i] += s * env * vol;
            }
        }

        static void AddDrum(float[] d, float start, int kind, float vol)
        {
            float[] s = kind == 0 ? Thump(0.22f, 58f, 1f) : kind == 1 ? SnowBurst(0.13f, 4500f, 0.8f) : SnowBurst(0.035f, 9000f, 0.5f);
            if (kind == 2) HighPass(s, 4000f);
            int at = (int)(start * Rate);
            for (int i = 0; i < s.Length && at + i < d.Length; i++) d[at + i] += s[i] * vol;
        }

        /// <summary>
        /// A sung "la" (or "na", "hey") by one voice: a buzzing source through three vowel formants,
        /// with a quick consonant dip at the start so the syllables separate.
        /// </summary>
        static void AddVoice(float[] d, float start, float len, float hz, float vol, float detune, float seed)
        {
            int at = (int)(start * Rate), n = (int)(len * Rate);
            float f1 = 760f, f2 = 1180f, f3 = 2550f;
            // Three two-pole resonators.
            float[] y1 = new float[3], y2 = new float[3];
            float[] fr = { f1, f2, f3 }; float[] gain = { 1f, 0.55f, 0.22f };
            float[] c1 = new float[3], c2 = new float[3];
            for (int k = 0; k < 3; k++)
            {
                float r = Mathf.Exp(-Mathf.PI * (k == 0 ? 90f : 120f) / Rate);
                c1[k] = 2f * r * Mathf.Cos(2f * Mathf.PI * fr[k] / Rate); c2[k] = -r * r;
            }
            float ph = seed;
            for (int i = 0; i < n && at + i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float f = hz * detune * (1f + 0.012f * Mathf.Sin(t * 2f * Mathf.PI * 5.3f + seed * 9f) * Mathf.Clamp01(t * 4f));
                ph += f / Rate;
                float src = Saw(ph) * 0.6f + (Rnd() * 0.04f);
                float o = 0f;
                for (int k = 0; k < 3; k++)
                {
                    float y = src * 0.06f + c1[k] * y1[k] + c2[k] * y2[k];
                    y2[k] = y1[k]; y1[k] = y;
                    o += y * gain[k];
                }
                // "L": a soft dip and a short closed colour at the very start.
                float onset = Mathf.Clamp01(t / 0.045f);
                float env = onset * onset * Mathf.Min(1f, (n - i) / (0.05f * Rate)) * (0.85f + 0.15f * Mathf.Exp(-t * 3f));
                d[at + i] += o * env * vol;
            }
        }

        /// <summary>
        /// The après-ski anthem: an original disco-schlager tune with a crowd "la la la" chorus.
        /// 126 bpm, 32 bars: riff, chorus, riff with claps, chorus.
        /// </summary>
        static float[] TavernSong()
        {
            const float bpm = 126f; float beat = 60f / bpm, bar = 4f * beat;
            int bars = 32;
            var d = Buf(bars * bar);
            // A minor feel resolving to C: Am F C G / Am F G G.   (semitones from A4)
            int[][] chords = { new[] { 0, 3, 7 }, new[] { -4, 0, 3 }, new[] { -9, -5, -2 }, new[] { -2, 2, 5 }, new[] { 0, 3, 7 }, new[] { -4, 0, 3 }, new[] { -2, 2, 5 }, new[] { -2, 2, 5 } };
            // Instrumental riff (eighth notes; -99 = rest).
            int[] riff = { 12, -99, 12, 10, 12, -99, 15, -99, 8, -99, 8, 7, 8, -99, 12, -99, 3, -99, 3, 5, 7, -99, 10, -99, 5, -99, 5, 7, 5, 3, 2, -99 };
            // The chorus everyone bawls along to: "la la la la, la la laa".
            int[] hook = { 12, 12, 10, 12, 15, -99, 12, -99, 8, 8, 7, 8, 12, -99, 8, -99, 7, 7, 5, 7, 10, -99, 7, -99, 5, 5, 7, 5, 3, -99, 2, -99 };
            int[] hook2 = { 12, 12, 10, 12, 15, -99, 17, -99, 15, 15, 12, 15, 17, -99, 20, -99, 19, 17, 15, 17, 15, 12, 10, -99, 12, -99, 12, -99, 12, -99, -99, -99 };
            for (int b = 0; b < bars; b++)
            {
                int section = b / 8;                 // 0 riff, 1 chorus, 2 riff, 3 chorus
                bool chorus = section % 2 == 1;
                var ch = chords[b % 8];
                float t0 = b * bar;
                for (int q = 0; q < 4; q++)
                {
                    float t = t0 + q * beat;
                    AddDrum(d, t, 0, 0.85f);                                             // four on the floor
                    if (q % 2 == 1) AddDrum(d, t, 1, section >= 2 || chorus ? 0.55f : 0.4f); // clap on 2 and 4
                    AddDrum(d, t + beat * 0.5f, 2, 0.3f);                                 // off-beat hat
                    // Octave-bouncing disco bass.
                    AddTone(d, t, beat * 0.42f, Note(ch[0] - 24), 0.42f, 3, 5f);
                    AddTone(d, t + beat * 0.5f, beat * 0.4f, Note(ch[0] - 12), 0.3f, 3, 6f);
                    // Off-beat chord stabs.
                    foreach (int n in ch) AddTone(d, t + beat * 0.5f, beat * 0.28f, Note(n), 0.075f, 1, 7f);
                }
                int[] line = chorus ? (b % 16 >= 12 ? hook2 : hook) : riff;
                for (int s = 0; s < 8; s++)
                {
                    int m = line[(b % 4) * 8 + s];
                    if (m == -99) continue;
                    float t = t0 + s * beat * 0.5f;
                    // Hold a note across following rests.
                    int hold = 1;
                    while (s + hold < 8 && line[(b % 4) * 8 + s + hold] == -99) hold++;
                    float len = beat * 0.5f * hold * 0.92f;
                    if (chorus)
                    {
                        // The room sings: several voices, some an octave down, slightly out of tune and time.
                        for (int v = 0; v < 5; v++)
                        {
                            float det = 1f + (v - 2) * 0.006f;
                            int oct = v >= 3 ? -24 : -12;
                            AddVoice(d, t + v * 0.007f, len, Note(m + oct), 0.2f, det, v * 1.37f + b);
                        }
                        AddTone(d, t, len, Note(m - 12), 0.05f, 2, 5f);
                    }
                    else AddTone(d, t, len, Note(m - 12), 0.15f, 2, 5.5f);
                }
                // A brassy answer at the end of every fourth bar.
                if (b % 4 == 3) foreach (int n in ch) { AddTone(d, t0 + beat * 3f, beat * 0.4f, Note(n + 12), 0.06f, 1, 5f); AddTone(d, t0 + beat * 3.5f, beat * 0.45f, Note(n + 12), 0.07f, 1, 4f); }
            }
            LowPass(d, 7000f);
            Normalise(d, 0.8f);
            return d;
        }

        /// <summary>Calm lift-ride loop: slow pads and a music-box arpeggio.</summary>
        static float[] MenuMusic()
        {
            const float beat = 0.6f;
            int bars = 8;
            var d = Buf(bars * 4 * beat + 0.4f);
            int[][] chords = { new[] { 0, 3, 7 }, new[] { -4, 0, 3 }, new[] { -9, -5, -2 }, new[] { -2, 2, 5 } }; // Am F C G
            for (int bar = 0; bar < bars; bar++)
            {
                var ch = chords[bar % 4];
                float t0 = bar * 4 * beat;
                foreach (int n in ch)
                {
                    AddTone(d, t0, 4 * beat, Note(n - 12), 0.09f, 0, 0.5f);
                    AddTone(d, t0, 4 * beat, Note(n - 12) * 1.004f, 0.07f, 3, 0.6f);
                }
                AddTone(d, t0, 4 * beat, Note(ch[0] - 24), 0.16f, 0, 0.4f);
                int[] order = { 0, 1, 2, 1, 0, 2, 1, 2 };
                for (int s = 0; s < 8; s++)
                {
                    if (bar % 2 == 1 && s == 7) continue;
                    AddTone(d, t0 + s * beat * 0.5f, beat * 1.2f, Note(ch[order[s]] + 12), 0.08f, 0, 4.5f);
                }
            }
            d = Looped(d, 0.4f);
            Normalise(d, 0.6f);
            return d;
        }

        /// <summary>
        /// Music for the runs: an easy, rolling groove that stays out of the way. Soft kick, round
        /// bass, plucked arpeggio and a pad; 102 bpm, 16 bars.
        /// </summary>
        static float[] SkiMusic()
        {
            const float bpm = 102f; float beat = 60f / bpm, bar = 4f * beat;
            int bars = 16;
            var d = Buf(bars * bar + 0.5f);
            // D major colour: D A Bm G / D A G G.  (from A4: D=-7, A=0, B=2, G=-2)
            int[][] chords = { new[] { -7, -3, 0 }, new[] { -12, -8, -5 }, new[] { -10, -7, -3 }, new[] { -14, -10, -7 }, new[] { -7, -3, 0 }, new[] { -12, -8, -5 }, new[] { -14, -10, -7 }, new[] { -14, -10, -7 } };
            int[] lead = { 5, -99, 4, 2, 0, -99, -3, -99, 0, -99, 2, 4, 0, -99, -99, -99, 2, -99, 4, 5, 9, -99, 7, -99, 5, 4, 2, -99, 0, -99, -99, -99 };
            for (int b = 0; b < bars; b++)
            {
                var ch = chords[b % 8];
                float t0 = b * bar;
                // Pad.
                foreach (int n in ch) { AddTone(d, t0, bar, Note(n), 0.035f, 1, 0.6f); AddTone(d, t0, bar, Note(n) * 1.003f, 0.03f, 0, 0.5f); }
                for (int q = 0; q < 4; q++)
                {
                    float t = t0 + q * beat;
                    AddDrum(d, t, 0, q % 2 == 0 ? 0.55f : 0.3f);
                    if (q % 2 == 1) AddDrum(d, t, 1, 0.16f);
                    AddDrum(d, t + beat * 0.5f, 2, 0.12f);
                    AddTone(d, t, beat * 0.8f, Note(ch[0] - 12), 0.26f, 3, 3.2f);
                    // Rolling plucked arpeggio.
                    for (int s = 0; s < 2; s++)
                    {
                        int idx = (q * 2 + s) % 3;
                        AddTone(d, t + s * beat * 0.5f, beat * 0.6f, Note(ch[idx] + 12), 0.07f, 4, 5f);
                    }
                }
                // A sparse melody every other four bars.
                if (b % 8 >= 4)
                    for (int s = 0; s < 8; s++)
                    {
                        int m = lead[(b % 4) * 8 + s];
                        if (m == -99) continue;
                        AddTone(d, t0 + s * beat * 0.5f, beat * 0.9f, Note(m), 0.07f, 2, 4f);
                    }
            }
            LowPass(d, 5200f);
            d = Looped(d, 0.5f);
            Normalise(d, 0.6f);
            return d;
        }
    }
}
