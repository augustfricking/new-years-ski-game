using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>One beat of the lift cutscene: who speaks, what the camera does, how bad the weather is.</summary>
    public sealed class Beat
    {
        public CharacterId Who;
        public string Text;
        public string Voice;          // clip name under Resources/Voices
        public int Shot;              // index into LiftScene's camera shots
        public float Storm;           // weather to reach by the end of this beat, 0..1
        public float Mood;            // brows: -1 annoyed .. +1 alarmed
        public Pose? Gesture;         // pose the speaker takes
        public bool Panic;            // everyone huddles or grabs the bar
        public float Hold = 0.25f;    // pause after the line
    }

    /// <summary>
    /// The ride up: ski banter, the bet for the first round at the after-ski, a New Year's kiss that
    /// never got a text back, and a snow storm that swallows the mountain. About 50 seconds.
    /// </summary>
    public static class CutsceneScript
    {
        const CharacterId J = CharacterId.Jonte, M = CharacterId.Maja, E = CharacterId.Erik, L = CharacterId.Linn;

        public static readonly Beat[] Beats =
        {
            new Beat { Who = L, Shot = 0, Storm = 0.03f, Text = "Last lift of the year, people. Let's make this run count.", Gesture = Pose.SitPoint },
            new Beat { Who = E, Shot = 0, Storm = 0.07f, Text = "I waxed my skis this morning. I'm carving the whole way down." },
            new Beat { Who = J, Shot = 1, Storm = 0.12f, Mood = -0.5f, Text = "Carving? You ski like you're filling in a tax form." },
            new Beat { Who = M, Shot = 1, Storm = 0.17f, Text = "Says the guy who sat down four times before lunch.", Hold = 0.4f },
            new Beat { Who = L, Shot = 2, Storm = 0.24f, Text = "Deal: last one down buys the first round at the after-ski.", Gesture = Pose.SitPoint },
            new Beat { Who = M, Shot = 3, Storm = 0.3f, Mood = 0.3f, Text = "Speaking of after-ski... Erik. Who are you kissing at midnight?" },
            new Beat { Who = E, Shot = 3, Storm = 0.36f, Mood = 1f, Text = "What? Nobody! Why would you..." },
            new Beat { Who = L, Shot = 3, Storm = 0.45f, Mood = -0.8f, Text = "Because last New Year you kissed me. Then you didn't text for three weeks." },
            new Beat { Who = J, Shot = 4, Storm = 0.52f, Mood = 1f, Text = "Three weeks? Dude." },
            new Beat { Who = E, Shot = 4, Storm = 0.6f, Mood = 0.8f, Text = "I dropped my phone in the hot tub!", Gesture = Pose.SitAdjustGoggles },
            new Beat { Who = M, Shot = 4, Storm = 0.7f, Mood = -0.6f, Text = "Funny. He told me it fell off this lift.", Hold = 0.5f },
            new Beat { Who = J, Shot = 5, Storm = 0.84f, Mood = 1f, Panic = true, Text = "Uh, guys? Where did the mountain go?" },
            new Beat { Who = L, Shot = 5, Storm = 0.94f, Mood = 1f, Panic = true, Text = "I can't even see my own skis!" },
            new Beat { Who = E, Shot = 6, Storm = 0.98f, Mood = 0.6f, Panic = true, Text = "It's just a little snow. Everybody stay calm." },
            new Beat { Who = M, Shot = 6, Storm = 1f, Mood = -0.4f, Panic = true, Text = "You are crushing my arm, Erik." },
            new Beat { Who = J, Shot = 7, Storm = 1f, Mood = 1f, Panic = true, Text = "Top station! Bar up, bar up!" },
            new Beat { Who = L, Shot = 7, Storm = 0.95f, Mood = 0.2f, Text = "Okay. Maps out. One last run... then we party.", Hold = 0.6f },
        };

        static CutsceneScript()
        {
            for (int i = 0; i < Beats.Length; i++) Beats[i].Voice = "cut_" + (i + 1).ToString("00");
        }

        /// <summary>How long a line takes if there is no recorded voice for it.</summary>
        public static float EstimateSeconds(string text) => 0.7f + text.Length * 0.052f;
    }
}
