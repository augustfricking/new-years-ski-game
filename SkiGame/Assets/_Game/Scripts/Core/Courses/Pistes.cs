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

    /// <summary>Blue: the snow park. Two lines of features, a halfpipe and a big final jump.</summary>
    public sealed class BlueCourse : PisteCourse
    {
        public Halfpipe Pipe;

        public BlueCourse()
        {
            Id = SlopeId.Blue; Name = "Parken"; Seed = 23;
            BaseHalfWidth = 24f; NoiseAmp = 0.02f;
            BuildProfile(840f, (30f, 12f), (110f, 15f), (400f, 14f), (470f, 12.5f), (640f, 12.5f), (700f, 15.5f));

            // Section 1: jump line on the left, jib line on the right.
            const float jumpX = -9f, jibX = 10f;
            AddFeature(new Kicker(jumpX, 105f, 1.0f, 8f));
            AddFeature(new Kicker(jumpX, 165f, 1.5f, 8f));
            AddFeature(new Kicker(jumpX, 240f, 2.0f, 9f));
            AddFeature(new BoxFeature(jibX, 98f, 9f, 1.6f, 0.45f));
            AddFeature(new BoxFeature(jibX + 4f, 150f, 12f, 0.9f, 0.55f));
            AddFeature(new BoxFeature(jibX, 205f, 14f, 1.4f, 0.5f));
            AddFeature(new Roller(jibX, 262f, 9f, 0.9f, 10f));
            AddFeature(new Roller(jibX, 284f, 9f, 0.9f, 10f));

            // Section 2: the big table in the middle, with boxes beside it.
            AddFeature(new Kicker(0f, 350f, 2.6f, 11f));
            AddFeature(new BoxFeature(-15f, 335f, 16f, 1.2f, 0.5f));
            AddFeature(new BoxFeature(15f, 340f, 10f, 0.8f, 0.6f));

            // Section 3: the halfpipe.
            Pipe = new Halfpipe(0f, 455f, 650f, 9.5f, 4.2f);
            AddFeature(Pipe);

            // Section 4: the money booter and a last box on the way out.
            AddFeature(new Kicker(-4f, 728f, 2.9f, 12f));
            AddFeature(new BoxFeature(13f, 715f, 12f, 1.3f, 0.5f));
        }

        public override float RacingLineX(float z)
        {
            // The jump line: left kickers, centre table, down the pipe, final booter.
            if (z < 290f) return Mathf.Lerp(0f, -9f, M.Smooth(z / 70f));
            if (z < 330f) return Mathf.Lerp(-9f, 0f, M.Smooth((z - 290f) / 40f));
            if (z < 680f) return 0f;
            return Mathf.Lerp(0f, -4f, M.Smooth((z - 680f) / 35f));
        }
    }

    /// <summary>Red: a steady, steep pitch with a slalom course set on it.</summary>
    public sealed class RedCourse : PisteCourse
    {
        public RedCourse()
        {
            Id = SlopeId.Red; Name = "Slalombacken"; Seed = 37;
            BaseHalfWidth = 17f; NoiseAmp = 0.03f;
            AddBend(9f, 520f, 1.1f);
            SetWidthVariation(1.5f, 300f);
            BuildProfile(820f, (30f, 15f), (120f, 21f), (350f, 19f), (520f, 23f), (700f, 20f));
        }
    }

    /// <summary>Black: the steepest run. A downhill race over pits and rollers.</summary>
    public sealed class BlackCourse : PisteCourse
    {
        public BlackCourse()
        {
            Id = SlopeId.Black; Name = "Svarta Väggen"; Seed = 53;
            BaseHalfWidth = 15.5f; NoiseAmp = 0.04f; NoiseLen = 21f;
            AddBend(14f, 610f, 0.2f);
            AddBend(6f, 270f, 1.7f);
            SetWidthVariation(2f, 240f);
            BuildProfile(1180f, (30f, 18f), (110f, 27f), (300f, 24f), (430f, 31f), (640f, 26f), (800f, 30f), (1000f, 25f));

            // Subtle trouble: hollows and rollers. There is always a clean line somewhere.
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
        public static readonly string[] Goals =
        {
            "Fastest time. Don't hit the kids!",
            "Most points: big air, tricks, clean landings.",
            "Fastest time through every gate.",
            "Fastest time. Tuck hard, dodge the pits.",
            "Fastest time. Find a way down.",
        };
        public static bool ScoreBased(SlopeId id) => id == SlopeId.Blue;
    }
}
