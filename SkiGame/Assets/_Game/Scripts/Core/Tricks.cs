using System.Collections.Generic;
using UnityEngine;

namespace Ski.Core
{
    /// <summary>What a landed trick (or a grind) was worth.</summary>
    public sealed class TrickResult
    {
        public string Name;
        public int Points;
        public float AirTime, Height;
        public bool Clean;     // well-aligned, soft landing
        public bool Sketchy;   // barely held it
        public readonly List<string> Parts = new List<string>();
    }

    /// <summary>Turns the air state of a <see cref="SkiSim"/> into a name and a score.</summary>
    public static class TrickScorer
    {
        static readonly string[,] GrabNames =
        {
            { "", "", "" },
            { "Safety", "Nose grab", "Tail grab" },        // key 1 (+W, +S)
            { "Mute", "Japan", "Truck driver" },           // key 2
            { "", "", "" },
            { "Spread eagle", "Daffy", "Backscratcher" },  // key 4
        };
        static readonly float[] GrabRate = { 0f, 160f, 220f, 0f, 190f };

        public static string GrabName(int trick, int variant) =>
            trick >= 0 && trick < 5 ? GrabNames[trick, Mathf.Clamp(variant, 0, 2)] : "";

        public static TrickResult Score(SkiSim s, float yawError, float impact)
        {
            var r = new TrickResult { AirTime = s.AirTime, Height = s.MaxAirHeight };
            float pts = 0f;

            const float Full = 2f * Mathf.PI;
            int corks = Mathf.RoundToInt(Mathf.Abs(s.Cork) / Full);
            if (corks > 0)
            {
                r.Parts.Add(corks == 1 ? "Cork" : corks == 2 ? "Double cork" : "Triple cork");
                pts += 650f * corks * (1f + 0.3f * (corks - 1));
            }
            int flips = Mathf.RoundToInt(Mathf.Abs(s.Flip) / Full);
            if (flips > 0)
            {
                string kind = s.Flip > 0f ? "frontflip" : "backflip";
                r.Parts.Add(flips == 1 ? Cap(kind) : flips == 2 ? "Double " + kind : "Triple " + kind);
                pts += 450f * flips * (1f + 0.3f * (flips - 1));
            }
            int halves = Mathf.RoundToInt(Mathf.Abs(s.AirYaw) / Mathf.PI);
            if (halves > 0)
            {
                r.Parts.Add((halves * 180).ToString());
                pts += 120f * halves * (1f + 0.15f * (halves - 1));
            }
            for (int t = 1; t <= 4; t++)
            {
                if (GrabRate[t] <= 0f) continue;
                for (int v = 0; v < 3; v++)
                {
                    float time = s.GrabTime[t * 3 + v];
                    if (time < 0.22f) continue;
                    r.Parts.Add(GrabNames[t, v]);
                    pts += GrabRate[t] * (v == 0 ? 1f : 1.35f) * Mathf.Min(time, 2.5f);
                }
            }

            float air = 50f * s.AirTime + 35f * s.MaxAirHeight;
            if (r.Parts.Count == 0) { r.Name = s.TakeoffSwitch ? "Switch air" : "Air"; pts = air * (s.TakeoffSwitch ? 1.3f : 1f); }
            else
            {
                pts = (pts + air) * (1f + 0.3f * (r.Parts.Count - 1));
                r.Name = string.Join(" + ", r.Parts);
                // Riding in backwards is harder: the same trick is worth more. So is landing backwards.
                if (s.TakeoffSwitch) { pts *= 1.3f; r.Name = "Switch " + r.Name; }
                if (s.Switch) { pts *= 1.15f; r.Name += s.TakeoffSwitch ? " (still switch)" : " to switch"; }
            }

            r.Clean = yawError < 0.22f && impact < s.P.HeavyImpact * 0.8f;
            r.Sketchy = yawError > 0.6f || impact > s.P.HeavyImpact;
            if (r.Clean) pts *= 1.25f;
            else if (r.Sketchy) pts *= 0.6f;
            r.Points = Mathf.RoundToInt(pts / 5f) * 5;
            return r;
        }

        public static TrickResult ScoreGrind(float metres, float sideways)
        {
            bool slide = sideways > 0.7f;
            var r = new TrickResult { Name = slide ? "Boardslide" : "50-50 grind", Clean = true };
            r.Parts.Add(r.Name);
            r.Points = Mathf.RoundToInt(metres * (slide ? 95f : 60f) / 5f) * 5;
            return r;
        }

        static string Cap(string s) => char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
