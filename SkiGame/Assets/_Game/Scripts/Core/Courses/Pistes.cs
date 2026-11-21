using UnityEngine;

namespace Ski.Core
{
    /// <summary>Green: the gentle, wide children's slope.</summary>
    public sealed class GreenCourse : PisteCourse
    {
        public GreenCourse()
        {
            Id = SlopeId.Green; Name = "Barnbacken"; Seed = 11;
            BaseHalfWidth = 21f; NoiseAmp = 0.03f;
            AddBend(13f, 430f, 0.4f);
            AddBend(5f, 190f, 2.1f);
            SetWidthVariation(2.5f, 260f);
            BuildProfile(660f, (30f, 7f), (140f, 9.5f), (300f, 8f), (430f, 10.5f), (560f, 9f));
        }
    }

    /// <summary>
    /// Blue: the snow park. A warm-up line of small jumps and boxes, a big table, the halfpipe, then
    /// the big-air section with two lines of large kickers and a final booter.
    /// </summary>
    public sealed class BlueCourse : PisteCourse
    {
        public Halfpipe Pipe;
        const float LeftLine = -10f, RightLine = 10f;

        /// <summary>The forest stands well back from the pipe, so its walls have open sky behind them.</summary>
        public override float HalfWidth(float z) => base.HalfWidth(z) + (Pipe != null && z > Pipe.ZA - 30f && z < Pipe.ZB + 30f ? 9f * M.Smooth((z - (Pipe.ZA - 30f)) / 40f) * (1f - M.Smooth((z - (Pipe.ZB - 10f)) / 40f)) : 0f);

        public BlueCourse()
        {
            Id = SlopeId.Blue; Name = "Parken"; Seed = 23;
            BaseHalfWidth = 25f; NoiseAmp = 0.02f;
            SkidScale = 0.45f; // soft park snow: hard turns cost less
            ImpactScale = 1.6f; // and a clean landing from big air holds, even past the sweet spot
            BuildProfile(1190f, (30f, 12f), (110f, 15f), (470f, 14.5f), (500f, 17f), (690f, 17f), (730f, 15.5f), (1080f, 15.5f));

            // Section 1: warm-up. Jumps on the left, boxes and rollers on the right.
            AddFeature(new Kicker(LeftLine, 105f, 1.0f, 8f));
            AddFeature(new Kicker(LeftLine, 160f, 1.3f, 8f));
            AddFeature(new Kicker(LeftLine, 225f, 1.9f, 9f));
            AddFeature(new BoxFeature(RightLine, 98f, 9f, 1.6f, 0.45f));
            AddFeature(new BoxFeature(RightLine + 4f, 150f, 12f, 0.9f, 0.55f));
            AddFeature(new BoxFeature(RightLine, 205f, 14f, 1.4f, 0.5f));
            AddFeature(new Roller(RightLine, 262f, 9f, 0.9f, 10f));
            AddFeature(new Roller(RightLine, 284f, 9f, 0.9f, 10f));

            // Section 2: the big table in the middle, with side hits.
            AddFeature(new Kicker(0f, 370f, 2.7f, 11f));
            AddFeature(new BoxFeature(-16f, 335f, 16f, 1.2f, 0.5f));
            AddFeature(new BoxFeature(16f, 340f, 10f, 0.8f, 0.6f));

            // Section 3: the halfpipe.
            Pipe = new Halfpipe(0f, 500f, 700f, 9.5f, 4.8f);   // a proper pipe: walls nearly five metres, vertical at the lip
            AddFeature(Pipe);

            // Section 4: big air. Three large kickers on the left, three medium ones on the right.
            AddFeature(new Kicker(LeftLine, 770f, 2.9f, 12f));
            AddFeature(new Kicker(LeftLine, 872f, 3.2f, 12f));
            AddFeature(new Kicker(LeftLine, 978f, 3.4f, 12f));
            AddFeature(new Kicker(RightLine + 2f, 752f, 1.9f, 9f));
            AddFeature(new Kicker(RightLine + 2f, 836f, 2.2f, 9f));
            AddFeature(new Kicker(RightLine + 2f, 925f, 2.4f, 10f));
            AddFeature(new BoxFeature(RightLine + 2f, 1010f, 14f, 1.3f, 0.5f));

            // The money booter on the way out.
            AddFeature(new Kicker(0f, 1095f, 3.3f, 13f));
        }

        public override float RacingLineX(float z)
        {
            // The jump line: left kickers, centre table, down the pipe, big-air line, final booter.
            if (z < 290f) return Mathf.Lerp(0f, LeftLine, M.Smooth(z / 70f));
            if (z < 340f) return Mathf.Lerp(LeftLine, 0f, M.Smooth((z - 290f) / 50f));
            if (z < 705f) return 0f;
            if (z < 750f) return Mathf.Lerp(0f, LeftLine, M.Smooth((z - 705f) / 45f));
            if (z < 1040f) return LeftLine;
            return Mathf.Lerp(LeftLine, 0f, M.Smooth((z - 1040f) / 40f));
        }
    }

    /// <summary>Red: a winding pitch that keeps changing steepness, with a slalom course set on it.</summary>
    public sealed class RedCourse : PisteCourse
    {
        public RedCourse()
        {
            Id = SlopeId.Red; Name = "Slalombacken"; Seed = 37;
            BaseHalfWidth = 17f; NoiseAmp = 0.03f;
            AddBend(17f, 410f, 1.1f);
            AddBend(7f, 175f, 0.4f);
            SetWidthVariation(1.5f, 300f);
            BuildProfile(860f, (30f, 14f), (100f, 22f), (215f, 16f), (330f, 25f), (450f, 17.5f), (560f, 26f), (680f, 18f), (770f, 22f));
        }
    }

    /// <summary>Black: the steepest run. A winding downhill race over pits and rollers.</summary>
    public sealed class BlackCourse : PisteCourse
    {
        public BlackCourse()
        {
            Id = SlopeId.Black; Name = "Svarta Väggen"; Seed = 53;
            BaseHalfWidth = 15.5f; NoiseAmp = 0.04f; NoiseLen = 21f;
            // Crossing a pit at racing speed slams you into its far wall at 11-12 m/s whatever the speed: a heavy,
            // slowing landing here, not a certain crash (dodging them is still faster).
            ImpactScale = 1.35f;
            AddBend(24f, 520f, 0.2f);
            AddBend(10f, 235f, 1.7f);
            AddBend(4f, 125f, 0.9f);
            SetWidthVariation(2f, 240f);
            BuildProfile(1180f, (30f, 18f), (110f, 27f), (300f, 24f), (430f, 31f), (640f, 26f), (800f, 30f), (1000f, 25f));

            // Trouble: hollows and rollers. There is always a clean line somewhere.
            var rng = new Rng(Seed);
            float z = 150f;
            int lastLane = 0;
            while (z < FinishZ - 90f)
            {
                float hw = HalfWidth(z) - 3f;
                int lane;
                do { lane = rng.Range(-1, 2); } while (lane == lastLane && rng.Chance(0.7f));
                lastLane = lane;
                float x = CenterX(z) + lane * hw * 0.62f + rng.Range(-1.5f, 1.5f);
                if (rng.Chance(0.68f))
                {
                    float radius = rng.Range(3.8f, 5.4f);
                    AddFeature(new Pit(x, z, radius, radius * rng.Range(0.23f, 0.27f)));
                    // Sometimes a second hollow beside it, leaving one lane open.
                    if (rng.Chance(0.4f))
                    {
                        int lane2 = lane == 0 ? (rng.Chance(0.5f) ? -1 : 1) : 0;
                        float r2 = rng.Range(3.6f, 4.8f);
                        float z2 = z + rng.Range(4f, 10f);
                        AddFeature(new Pit(CenterX(z2) + lane2 * hw * 0.62f, z2, r2, r2 * 0.24f));
                    }
                }
                else
                {
                    AddFeature(new Roller(x, z, rng.Range(5f, 7.5f), rng.Range(0.55f, 0.85f), rng.Range(7f, 11f)));
                }
                z += rng.Range(34f, 62f);
            }
        }
    }

    public static class Courses
    {
        public static Course Create(SlopeId id)
        {
            switch (id)
            {
                case SlopeId.Green: return new GreenCourse();
                case SlopeId.Blue: return new BlueCourse();
                case SlopeId.Red: return new RedCourse();
                case SlopeId.Black: return new BlackCourse();
                default: return new OffPisteCourse();
            }
        }

        public static readonly string[] Names = { "Barnbacken", "Parken", "Slalombacken", "Svarta Väggen", "Ravinen" };
        /// <summary>Where each run's finish line is (its FinishZ), without building the run (a test keeps these true).</summary>
        public static readonly float[] FinishZ = { 660f, 1190f, 860f, 1180f, 1258f };
        public static readonly string[] Goals =
        {
            "Fastest time. Mind the kids.",
            "Most trick points.",
            "Fastest time. Every gate.",
            "Fastest time. Dodge the pits.",
            "Fastest time. Find a way down.",
        };
        public static bool ScoreBased(SlopeId id) => id == SlopeId.Blue;

        /// <summary>
        /// Whether a result could have come from a run at all (nobody skis the red in five seconds).
        /// Results outside this are kept off the world board: not anti-cheat, just a guard against garbage.
        /// </summary>
        public static bool Plausible(SlopeId id, float value)
        {
            switch (id)
            {
                case SlopeId.Green: return value > 22f && value < 900f;
                case SlopeId.Blue: return value >= 0f && value < 200000f;
                case SlopeId.Red: return value > 30f && value < 900f;
                case SlopeId.Black: return value > 28f && value < 900f;
                default: return value > 55f && value < 1800f;
            }
        }
    }
}
