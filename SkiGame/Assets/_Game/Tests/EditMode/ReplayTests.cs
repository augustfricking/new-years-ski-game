using NUnit.Framework;
using Ski.Core;
using UnityEngine;

namespace Ski.Tests
{
    public class ReplayTests
    {
        /// <summary>Plays a slope with the bot, recording every step's input as the game does.</summary>
        static (RunSession, RunInputs) Record(SlopeId slope, CharacterId who, int respawnAtStep = -1)
        {
            var s = new RunSession(slope, who);
            var bot = new Bot(s);
            var rec = new RunInputs();
            for (int i = 0; i < 120 * 400 && s.Phase != RunPhase.Finished; i++)
            {
                if (i == respawnAtStep) { s.Respawn(); rec.Respawn(); }
                var input = RunInputs.Quantize(bot.Decide());
                rec.Record(input);
                s.Step(input, SkiSim.FixedDt);
                s.Events.Clear();
            }
            return (s, rec);
        }

        [TestCase(SlopeId.Green), TestCase(SlopeId.Blue), TestCase(SlopeId.Red), TestCase(SlopeId.Black), TestCase(SlopeId.OffPiste)]
        public void PlayingTheInputsBackRepeatsTheRunExactly(SlopeId slope)
        {
            var (orig, rec) = Record(slope, CharacterId.Linn, respawnAtStep: slope == SlopeId.Black ? 2400 : -1);
            Assert.AreEqual(RunPhase.Finished, orig.Phase, "the bot finished");
            string text = rec.Encode();
            Debug.Log(slope + ": " + rec.Steps + " steps, " + text.Length + " characters recorded");
            Assert.That(text.Length, Is.LessThan(6000), "small enough to go with the score");

            var back = RunInputs.Decode(text);
            Assert.IsNotNull(back);
            Assert.IsTrue(back.Playable);
            Assert.AreEqual(rec.Steps, back.Steps);
            var again = new RunSession(slope, CharacterId.Linn);
            var reader = new RunInputs.Reader(back);
            while (RunInputs.StepInto(again, reader)) again.Events.Clear();

            Assert.AreEqual(RunPhase.Finished, again.Phase);
            Assert.AreEqual(orig.Time, again.Time, "the same time to the step");
            Assert.AreEqual(orig.Penalty, again.Penalty);
            Assert.AreEqual(orig.Score, again.Score);
            Assert.AreEqual(orig.GatesMissed, again.GatesMissed);
            Assert.AreEqual(orig.PeopleHit, again.PeopleHit);
            Assert.AreEqual(orig.Sim.CrashCount, again.Sim.CrashCount);
            Assert.AreEqual(orig.Sim.Pos, again.Sim.Pos, "the same place, exactly");
            if (orig.Crowd != null)
                for (int k = 0; k < orig.Crowd.Kids.Count; k++)
                {
                    Assert.AreEqual(orig.Crowd.Kids[k].X, again.Crowd.Kids[k].X, "kid " + k);
                    Assert.AreEqual(orig.Crowd.Kids[k].Z, again.Crowd.Kids[k].Z, "kid " + k);
                    Assert.AreEqual(orig.Crowd.Kids[k].Fallen, again.Crowd.Kids[k].Fallen, "kid " + k);
                }
        }

        /// <summary>
        /// The simulation as recordings know it: the bot's exact result on every slope. When a change to the
        /// skiing, the rules or a course moves these, recordings made before it no longer play true: bump
        /// <see cref="RunInputs.SimVersion"/> (they then fall back on their path) and write the new numbers here.
        /// </summary>
        static readonly (SlopeId slope, string result)[] Pinned =
        {
            (SlopeId.Green, "71.48794 5 0 13.3830109 660.022339"),
            (SlopeId.Blue, "76.8616257 0 5380 0.456260771 1190.10669"),
            (SlopeId.Red, "78.7861557 0 0 -0.4509494 860.043335"),
            (SlopeId.Black, "67.21398 0 0 12.414093 1180.0155"),
            (SlopeId.OffPiste, "124.083427 0 0 0.28011626 1258.01831"),
        };

        [Test, Timeout(600000)]
        public void TheSimulationIsTheOneRecordingsWereMadeWith()
        {
            var log = new System.Text.StringBuilder("Pinned results (SimVersion " + RunInputs.SimVersion + "):\n");
            bool changed = false;
            foreach (var (slope, pinned) in Pinned)
            {
                var s = new RunSession(slope, CharacterId.Erik);
                Bot.Play(s, new Bot(s));
                string now = s.Time.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " " + s.Penalty.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " " + s.Score
                    + " " + s.Sim.Pos.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " " + s.Sim.Pos.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                log.AppendLine("            (SlopeId." + slope + ", \"" + now + "\"),");
                if (now != pinned) changed = true;
            }
            Debug.Log(log.ToString());
            Assert.IsFalse(changed, "The simulation changed: recordings made before would ski another way. Bump RunInputs.SimVersion and pin the new results (in the log).");
        }

        [Test]
        public void OtherTextIsNotARecording()
        {
            Assert.IsNull(RunInputs.Decode(null));
            Assert.IsNull(RunInputs.Decode("not base64!"));
            var ghost = new GhostTrack();
            for (int i = 0; i < 10; i++) ghost.Record(i * 0.5f, 1f, i, 0f);
            Assert.IsNull(RunInputs.Decode(ghost.Encode()), "a position track is not an input recording");
            var rec = new RunInputs();
            rec.Record(new SkiInput { Steer = 1f, Trick = 3, Variant = 2 });
            string t = rec.Encode();
            Assert.IsNull(RunInputs.Decode(t.Substring(0, t.Length - 4)), "cut short");
            var r = new RunInputs.Reader(RunInputs.Decode(t));
            Assert.IsTrue(r.Next(out var input, out bool respawn));
            Assert.AreEqual(1f, input.Steer); Assert.AreEqual(3, input.Trick); Assert.AreEqual(2, input.Variant); Assert.IsFalse(respawn);
            Assert.IsFalse(r.Next(out _, out _));
        }
    }
}
