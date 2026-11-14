using System;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// Makes every sound effect, ambience loop and music loop from maths at run time, so the game
    /// ships no audio files except the recorded voices. Deterministic.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 22050;
        static uint _seed = 12345;

        static float Rnd() { _seed ^= _seed << 13; _seed ^= _seed >> 17; _seed ^= _seed << 5; return (_seed & 0xFFFFFF) / 8388608f - 1f; }
        static float Rnd01() => Rnd() * 0.5f + 0.5f;

        public static AudioClip Make(string id)
        {
            _seed = (uint)(id.GetHashCode() | 1);
            float[] d;
            switch (id)
            {
                case "wind": d = WindLoop(4f); break;
                case "ski": d = SkiLoop(3f, false); break;
                case "skid": d = SkiLoop(2.5f, true); break;
                case "click": d = Blip(0.06f, 880f, 620f, 0.6f); break;
                case "hover": d = Blip(0.025f, 1500f, 1300f, 0.4f); break;
                case "pole": d = Mix(NoiseBurst(0.09f, 2500f, 0.5f), Thump(0.09f, 120f, 0.5f)); break;
                case "pole_perfect": d = Mix(Mix(NoiseBurst(0.09f, 2500f, 0.5f), Thump(0.09f, 120f, 0.5f)), Blip(0.12f, 1320f, 1760f, 0.3f)); break;
                case "jump": d = Whoosh(0.28f, 500f, 2600f, 0.5f); break;
                case "land": d = Mix(Thump(0.22f, 75f, 0.9f), NoiseBurst(0.18f, 1400f, 0.4f)); break;
                case "land_heavy": d = Mix(Thump(0.35f, 55f, 1f), NoiseBurst(0.3f, 900f, 0.7f)); break;
                case "crash": d = Crash(); break;
                case "gate": d = Chime(new[] { 1320f, 1980f }, 0.22f, 0.4f); break;
                case "gate_miss": d = Buzz(0.32f, 140f, 0.5f); break;
                case "trick": d = Arp(new[] { 660f, 880f, 1100f }, 0.07f, 0.35f); break;
                case "trick_big": d = Arp(new[] { 660f, 880f, 1100f, 1320f, 1760f }, 0.075f, 0.4f); break;
                case "finish": d = Arp(new[] { 523f, 659f, 784f, 1047f, 784f, 1047f, 1319f }, 0.13f, 0.4f); break;
                case "penalty": d = Buzz(0.4f, 110f, 0.55f); break;
                case "scream": d = Scream(1.3f); break;
                case "fw_launch": d = Whistle(0.9f); break;
                case "fw_boom": d = Boom(1.6f); break;
                case "fw_crackle": d = Crackle(1.2f); break;
                case "door": d = Mix(Thump(0.25f, 90f, 0.8f), NoiseBurst(0.12f, 700f, 0.4f)); break;
                case "gulp": d = Gulp(); break;
                case "clink": d = Chime(new[] { 2350f, 3180f, 4700f }, 0.5f, 0.35f); break;
                case "burp": d = Burp(); break;
                case "cheer": d = Cheer(2.2f); break;
                case "paper": d = NoiseBurst(0.3f, 3500f, 0.35f); break;
                case "lift": d = LiftLoop(4f); break;
                case "crowd": d = CrowdLoop(7f); break;
                case "music_menu": d = MenuMusic(); break;
                case "music_tavern": d = TavernMusic(); break;
                default: Debug.LogWarning("Synth: unknown sound " + id); return null;
            }
            var clip = AudioClip.Create(id, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
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
        static float Square(float phase) => phase - Mathf.Floor(phase) < 0.5f ? 1f : -1f;
        static float Sin(float phase) => Mathf.Sin(phase * 2f * Mathf.PI);

        // ---- effects -------------------------------------------------------------------------

        static float[] Blip(float sec, float f0, float f1, float vol)
        {
            var d = Buf(sec); float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                ph += Mathf.Lerp(f0, f1, t) / Rate;
                d[i] = Sin(ph) * (1f - t) * (1f - t) * vol * Mathf.Min(1f, i / 30f);
            }
            return d;
        }

        static float[] NoiseBurst(float sec, float lp, float vol)
        {
            var d = Buf(sec);
            for (int i = 0; i < d.Length; i++) { float t = i / (float)d.Length; d[i] = Rnd() * (1f - t) * (1f - t); }
            LowPass(d, lp);
            Normalise(d, vol);
            return d;
        }

        static float[] Thump(float sec, float hz, float vol)
        {
            var d = Buf(sec); float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                ph += hz * (1.6f - t) / Rate;
                d[i] = Sin(ph) * Mathf.Exp(-5f * t) * vol;
            }
            return d;
        }

        static float[] Whoosh(float sec, float f0, float f1, float vol)
        {
            var d = Buf(sec); float y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Lerp(f0, f1, t) / Rate);
                y += a * (Rnd() - y);
                d[i] = y * Mathf.Sin(t * Mathf.PI);
            }
            HighPass(d, 300f);
            Normalise(d, vol);
            return d;
        }

        static float[] Crash()
        {
            var d = Mix(NoiseBurst(0.5f, 1800f, 0.9f), Thump(0.3f, 60f, 1f));
            var o = new float[(int)(1.0f * Rate)];
            Array.Copy(d, o, Mathf.Min(d.Length, o.Length));
            // Tumbling thumps.
            for (int k = 0; k < 4; k++)
            {
                var t = Thump(0.14f, 70f + k * 12f, 0.6f - k * 0.1f);
                int at = (int)((0.22f + k * 0.17f) * Rate);
                for (int i = 0; i < t.Length && at + i < o.Length; i++) o[at + i] += t[i];
            }
            Normalise(o, 0.9f);
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

        static float[] Buzz(float sec, float hz, float vol)
        {
            var d = Buf(sec);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                d[i] = Square(i * hz / Rate) * 0.5f * (1f - t * 0.6f) * vol * Mathf.Min(1f, (1f - t) * 12f);
            }
            LowPass(d, 1600f);
            return d;
        }

        static float[] Arp(float[] notes, float step, float vol)
        {
            var d = Buf(step * notes.Length + 0.25f);
            for (int n = 0; n < notes.Length; n++)
            {
                int at = (int)(n * step * Rate), len = (int)(0.3f * Rate);
                for (int i = 0; i < len && at + i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    d[at + i] += (Square(notes[n] * t) * 0.35f + Sin(notes[n] * t) * 0.65f) * Mathf.Exp(-t * 11f) * vol;
                }
            }
            LowPass(d, 5000f);
            return d;
        }

        static float[] Scream(float sec)
        {
            // A child going "aaaaaah": a bright tone with vibrato, rising then falling, plus breath.
            var d = Buf(sec); float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                float f = Mathf.Lerp(780f, 1050f, Mathf.Sin(t * Mathf.PI)) * (1f + 0.035f * Mathf.Sin(i * 2f * Mathf.PI * 9f / Rate));
                ph += f / Rate;
                float env = Mathf.Min(1f, t * 14f) * Mathf.Min(1f, (1f - t) * 5f);
                d[i] = (Saw(ph) * 0.5f + Sin(ph * 2f) * 0.3f + Rnd() * 0.08f) * env;
            }
            LowPass(d, 3600f); HighPass(d, 500f);
            Normalise(d, 0.6f);
            return d;
        }

        static float[] Whistle(float sec)
        {
            var d = Buf(sec); float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                ph += Mathf.Lerp(500f, 2600f, t * t) / Rate;
                d[i] = (Sin(ph) * 0.5f + Rnd() * 0.25f * t) * Mathf.Min(1f, t * 10f) * (1f - t * 0.4f);
            }
            HighPass(d, 400f);
            Normalise(d, 0.4f);
            return d;
        }

        static float[] Boom(float sec)
        {
            var d = Buf(sec);
            for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; d[i] = Rnd() * Mathf.Exp(-t * 3.2f); }
            LowPass(d, 420f);
            var body = Thump(0.5f, 48f, 1f);
            for (int i = 0; i < body.Length; i++) d[i] += body[i] * 0.9f;
            Normalise(d, 0.95f);
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
            HighPass(d, 1500f);
            Normalise(d, 0.45f);
            return d;
        }

        static float[] Gulp()
        {
            var d = Buf(0.18f); float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                ph += Mathf.Lerp(420f, 170f, t) / Rate;
                d[i] = Sin(ph) * Mathf.Sin(t * Mathf.PI) * 0.7f;
            }
            return d;
        }

        static float[] Burp()
        {
            var d = Buf(0.55f); float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                ph += (85f + 30f * Mathf.Sin(t * 14f) + Rnd() * 10f) / Rate;
                d[i] = Saw(ph) * Mathf.Sin(t * Mathf.PI) * (0.7f + 0.3f * Mathf.Sin(t * 60f));
            }
            LowPass(d, 900f);
            Normalise(d, 0.7f);
            return d;
        }

        static float[] Cheer(float sec)
        {
            var d = Buf(sec);
            for (int v = 0; v < 14; v++)
            {
                float f = 300f + Rnd01() * 500f, start = Rnd01() * 0.4f, ph = 0f, vib = 4f + Rnd01() * 5f;
                for (int i = (int)(start * Rate); i < d.Length; i++)
                {
                    float t = (i / (float)Rate - start) / (sec - start);
                    ph += f * (1f + 0.25f * Mathf.Sin(t * 3f) + 0.03f * Mathf.Sin(i * vib * 6.28f / Rate)) / Rate;
                    d[i] += Saw(ph) * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.2f;
                }
            }
            for (int i = 0; i < d.Length; i++) d[i] += Rnd() * 0.2f * Mathf.Sin(i / (float)d.Length * Mathf.PI);
            LowPass(d, 3000f); HighPass(d, 350f);
            Normalise(d, 0.55f);
            return d;
        }

        // ---- loops ---------------------------------------------------------------------------

        static float[] WindLoop(float sec)
        {
            var d = Buf(sec + 0.6f);
            float y1 = 0f, y2 = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float cutoff = 500f + 350f * Mathf.Sin(t * 1.57f) + 180f * Mathf.Sin(t * 4.7f + 1f);
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
                y1 += a * (Rnd() - y1); y2 += a * (y1 - y2);
                d[i] = y2;
            }
            HighPass(d, 90f);
            d = Looped(d, 0.6f);
            Normalise(d, 0.8f);
            return d;
        }

        static float[] SkiLoop(float sec, bool harsh)
        {
            var d = Buf(sec + 0.4f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float grain = 0.75f + 0.25f * Mathf.Sin(t * (harsh ? 61f : 23f)) * Mathf.Sin(t * 7.3f);
                d[i] = Rnd() * grain;
            }
            LowPass(d, harsh ? 5200f : 3200f);
            HighPass(d, harsh ? 1500f : 800f);
            d = Looped(d, 0.4f);
            Normalise(d, 0.7f);
            return d;
        }

        static float[] LiftLoop(float sec)
        {
            var d = Buf(sec);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float hum = Sin(50f * t) * 0.5f + Sin(100f * t) * 0.25f + Sin(150f * t) * 0.12f;
                hum *= 0.8f + 0.2f * Mathf.Sin(t * 2f * Mathf.PI * 0.5f);
                d[i] = hum * 0.5f;
            }
            // Wheel clatter twice per loop.
            for (int k = 0; k < 2; k++)
                for (int c = 0; c < 5; c++)
                {
                    var click = NoiseBurst(0.04f, 2200f, 0.35f);
                    int at = (int)((k * sec * 0.5f + 0.3f + c * 0.11f) * Rate);
                    for (int i = 0; i < click.Length && at + i < d.Length; i++) d[at + i] += click[i];
                }
            return d;
        }

        static float[] CrowdLoop(float sec)
        {
            // Babble: many voices, each a buzzy tone through a moving formant, chopped into syllables.
            var d = Buf(sec + 0.8f);
            for (int v = 0; v < 22; v++)
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
            HighPass(d, 180f);
            d = Looped(d, 0.8f);
            Normalise(d, 0.6f);
            return d;
        }

        // ---- music ---------------------------------------------------------------------------

        static float Note(int semitonesFromA4) => 440f * Mathf.Pow(2f, semitonesFromA4 / 12f);

        static void AddTone(float[] d, float start, float len, float hz, float vol, int wave, float decay)
        {
            int at = (int)(start * Rate), n = (int)(len * Rate);
            for (int i = 0; i < n && at + i < d.Length; i++)
            {
                float t = i / (float)Rate, ph = hz * t;
                float s = wave == 0 ? Sin(ph) : wave == 1 ? Saw(ph) * 0.6f + Saw(ph * 1.006f) * 0.6f : wave == 2 ? Square(ph) * 0.6f : Sin(ph) + 0.4f * Sin(ph * 2f) + 0.2f * Sin(ph * 3f);
                float env = Mathf.Min(1f, i / (0.006f * Rate)) * Mathf.Exp(-t * decay) * Mathf.Min(1f, (n - i) / (0.02f * Rate));
                d[at + i] += s * env * vol;
            }
        }

        static void AddDrum(float[] d, float start, int kind, float vol)
        {
            float[] s = kind == 0 ? Thump(0.22f, 58f, 1f) : kind == 1 ? NoiseBurst(0.13f, 4500f, 0.8f) : NoiseBurst(0.035f, 9000f, 0.5f);
            if (kind == 2) HighPass(s, 4000f);
            int at = (int)(start * Rate);
            for (int i = 0; i < s.Length && at + i < d.Length; i++) d[at + i] += s[i] * vol;
        }

        /// <summary>Après-ski party loop: four-on-the-floor, oompah bass and accordion stabs, 16 bars.</summary>
        static float[] TavernMusic()
        {
            const float bpm = 132f; float beat = 60f / bpm;
            int bars = 16;
            var d = Buf(bars * 4 * beat);
            // Chords as semitones from A4: C, F, G, C / Am, F, G, G
            int[][] chords = { new[] { -9, -5, -2 }, new[] { -4, 0, 3 }, new[] { -2, 2, 5 }, new[] { -9, -5, -2 }, new[] { 0, 3, 7 }, new[] { -4, 0, 3 }, new[] { -2, 2, 5 }, new[] { -2, 2, 5 } };
            int[] melody = { 3, 3, 5, 7, 7, 5, 3, 2, 0, 0, 2, 3, 3, 2, 2, -99, 3, 3, 5, 7, 7, 5, 3, 2, 0, 0, 2, 3, 2, 0, 0, -99 };
            for (int bar = 0; bar < bars; bar++)
            {
                var ch = chords[bar % 8];
                float t0 = bar * 4 * beat;
                for (int b = 0; b < 4; b++)
                {
                    float t = t0 + b * beat;
                    AddDrum(d, t, 0, 0.9f);                                   // kick
                    if (b % 2 == 1) AddDrum(d, t, 1, 0.5f);                    // clap
                    AddDrum(d, t + beat * 0.5f, 2, 0.35f);                     // off-beat hat
                    // Oompah: bass on the beat (root, fifth), chord on the off-beat.
                    AddTone(d, t, beat * 0.45f, Note(ch[b % 2 == 0 ? 0 : 2] - 24), 0.5f, 3, 5f);
                    foreach (int n in ch) AddTone(d, t + beat * 0.5f, beat * 0.3f, Note(n), 0.11f, 1, 7f);
                }
                // Melody: two notes per beat on the second half of the phrase.
                for (int s = 0; s < 8; s++)
                {
                    int m = melody[(bar % 4) * 8 + s];
                    if (m == -99) continue;
                    AddTone(d, t0 + s * beat * 0.5f, beat * 0.45f, Note(m + (bar % 8 >= 4 ? 0 : 0)), 0.16f, 2, 6f);
                }
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
            var d = Buf(bars * 4 * beat);
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
            // Blend the tail into the head so the pads loop smoothly.
            d = Looped(Mix(d, new float[d.Length + (int)(0.3f * Rate)]), 0.3f);
            Normalise(d, 0.6f);
            return d;
        }
    }
}
