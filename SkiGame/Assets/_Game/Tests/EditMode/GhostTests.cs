using NUnit.Framework;
using Ski.Core;
using UnityEngine;

namespace Ski.Tests
{
    public class GhostTests
    {
        static GhostTrack Record(SlopeId slope, out RunSession s)
        {
            s = new RunSession(slope, CharacterId.Erik);
            var bot = new Bot(s);
            var track = new GhostTrack();
            for (int i = 0; i < 120 * 240 && s.Phase != RunPhase.Finished; i++)
            {
                s.Step(bot.Decide(), SkiSim.FixedDt);
                s.Events.Clear();
                if (s.Phase == RunPhase.Running) track.Record(s.Time, s.Sim.Pos.x, s.Sim.Pos.z, s.Sim.Contact ? 0f : s.Sim.AirHeight);
            }
            return track;
        }

        [Test]
        public void ARecordedRunSurvivesStorageAndIsSmall()
        {
            var track = Record(SlopeId.Red, out var s);
            Assert.AreEqual(RunPhase.Finished, s.Phase);
            Assert.Greater(track.Count, 40);
            string text = track.Encode();
            Assert.Less(text.Length, 4000, "must fit in a leaderboard entry");
            var back = GhostTrack.Decode(text);
            Assert.NotNull(back);
            Assert.AreEqual(track.Count, back.Count);
            for (int i = 0; i < track.Count; i += 7)
            {
                Assert.AreEqual(track[i].X, back[i].X, 0.03f);
                Assert.AreEqual(track[i].Z, back[i].Z, 0.03f);
            }
        }

        [Test]
        public void AGhostMovesDownTheSlopeSmoothly()
        {
            var track = Record(SlopeId.Green, out _);
            float lastZ = -1000f;
            for (float t = 0f; t < track.Duration; t += 0.1f)
            {
                Assert.IsTrue(track.At(t, out Vector3 p, out _, out _, out float speed));
                Assert.Greater(p.z, lastZ - 0.6f, "the ghost should not jump back up the hill");
                Assert.Less(speed, 60f);
                lastZ = p.z;
            }
            Assert.IsFalse(track.At(track.Duration + 5f, out _, out _, out _, out _), "the recording ends");
        }

        [Test]
        public void GarbageIsNotARecording()
        {
            Assert.IsNull(GhostTrack.Decode(null));
            Assert.IsNull(GhostTrack.Decode(""));
            Assert.IsNull(GhostTrack.Decode("not base64 !!"));
            Assert.IsNull(GhostTrack.Decode("AAAA"));
        }

        [Test]
        public void RivalsCanShareTheCourseOfTheRun()
        {
            // The ghosts that ski live use the player's course object; they must still finish.
            var player = new RunSession(SlopeId.Green, CharacterId.Erik);
            var rival = new RunSession(SlopeId.Green, CharacterId.Linn, player.Course, false);
            Assert.IsNull(rival.Crowd);
            var bot = new Bot(rival);
            for (int i = 0; i < 120 * 200 && rival.Phase != RunPhase.Finished; i++) { rival.Step(bot.Decide(), SkiSim.FixedDt); rival.Events.Clear(); }
            Assert.AreEqual(RunPhase.Finished, rival.Phase);
        }
    }
}
