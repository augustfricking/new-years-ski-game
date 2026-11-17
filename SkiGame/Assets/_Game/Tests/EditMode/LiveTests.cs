using NUnit.Framework;
using Ski.Core;
using UnityEngine;

namespace Ski.Tests
{
    public class LiveTests
    {
        static LiveSample At(float x, float z, float yaw = 0f, LiveFlags f = LiveFlags.None, float y = 0f) =>
            new LiveSample { Pos = new Vector3(x, y, z), Yaw = yaw, Flags = f };

        [Test]
        public void PacketsSurviveTheTripAndStaySmall()
        {
            var p = new LivePacket { Mode = LiveMode.Tavern, Character = 3, Seq = 513, Start = 1234.5 };
            for (int i = 0; i < 16; i++) p.Samples.Add(At(-12.35f + i, 800.4f - i * 2f, i * 20f, i == 3 ? LiveFlags.Punch | LiveFlags.Tuck : LiveFlags.None, i * 0.1f));
            string text = p.Encode();
            Assert.Less(text.Length, 170, "one update must stay small: the room service is billed by bandwidth");
            var q = LivePacket.Decode(text);
            Assert.NotNull(q);
            Assert.AreEqual(LiveMode.Tavern, q.Mode);
            Assert.AreEqual(3, q.Character);
            Assert.AreEqual(513, q.Seq);
            Assert.AreEqual(1234.5, q.Start, 0.002);
            Assert.AreEqual(16, q.Samples.Count);
            for (int i = 0; i < 16; i++)
            {
                Assert.AreEqual(p.Samples[i].Pos.x, q.Samples[i].Pos.x, 0.03f);
                Assert.AreEqual(p.Samples[i].Pos.z, q.Samples[i].Pos.z, 0.03f);
                Assert.AreEqual(p.Samples[i].Pos.y, q.Samples[i].Pos.y, 0.06f);
                Assert.AreEqual(0f, Mathf.DeltaAngle(p.Samples[i].Yaw, q.Samples[i].Yaw), 1f);
                Assert.AreEqual(p.Samples[i].Flags, q.Samples[i].Flags);
            }
            Assert.IsNull(LivePacket.Decode("not a packet"));
            Assert.IsNull(LivePacket.Decode(""));
        }

        [Test]
        public void TimelineJoinsPacketsIntoSmoothMovement()
        {
            // A skier going straight down at 10 m/s, sending a packet every two seconds that takes 0.3 s to arrive.
            var rec = new LiveRecorder { Mode = LiveMode.Skiing, Character = 1 };
            var line = new LiveTimeline();
            double senderClock = 50.0;
            const double lag = 0.3, localStart = 1000.0;
            for (int frame = 0; frame < 60 * 12; frame++)
            {
                double t = senderClock + frame / 60.0;
                string sent = rec.Tick(t, At(2f, (float)(t - senderClock) * 10f, 0f));
                if (sent != null) Assert.IsTrue(line.Add(LivePacket.Decode(sent), localStart + (t - senderClock) + lag));
            }
            Assert.AreEqual(LiveMode.Skiing, line.Mode);
            Assert.AreEqual(1, line.Character);
            // Played back Delay seconds behind the sender (plus the trip): positions step evenly.
            float prev = float.NaN;
            for (double now = localStart + 6.0; now < localStart + 9.0; now += 0.05)
            {
                Assert.IsTrue(line.Visible(now));
                Assert.IsTrue(line.At(now, out var s, out float speed, out _));
                Assert.AreEqual(2f, s.Pos.x, 0.06f);
                Assert.AreEqual(10f, speed, 0.6f);
                double senderTime = now - localStart - lag - line.Delay;
                Assert.AreEqual((float)senderTime * 10f, s.Pos.z, 0.5f, "plays the sender back at a steady delay");
                if (!float.IsNaN(prev)) Assert.AreEqual(0.5f, s.Pos.z - prev, 0.08f, "no jumps between packets");
                prev = s.Pos.z;
            }
            // Gone quiet: taken off the screen.
            Assert.IsFalse(line.Visible(localStart + 40.0));
        }

        [Test]
        public void APacketSaysWhichPartOfTheWorldItsSenderIsIn()
        {
            var p = new LivePacket { Mode = LiveMode.Finished, Character = 2, Place = LivePlace.Of(SlopeId.Black), Seq = 5, Start = 10.0 };
            p.Samples.Add(new LiveSample { Pos = new Vector3(3f, 0f, 1200f), Yaw = 10f });
            var q = LivePacket.Decode(p.Encode());
            Assert.AreEqual(LivePlace.Of(SlopeId.Black), q.Place);
            Assert.IsTrue(LivePlace.IsSlope(q.Place));
            Assert.AreEqual(SlopeId.Black, LivePlace.Slope(q.Place));
            var t = new LiveTimeline();
            t.Add(q, 11.0);
            Assert.AreEqual(LivePlace.Of(SlopeId.Black), t.Place);
            Assert.IsFalse(LivePlace.IsSlope(LivePlace.Tavern));
        }

        [Test]
        public void TheFinishTableMatchesTheRuns()
        {
            // Live players on other runs and in the shared village are placed with these.
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(Courses.Create((SlopeId)i).FinishZ, Courses.FinishZ[i], 0.01f, ((SlopeId)i).ToString());
        }

        [Test]
        public void TimelineIgnoresRepeatsAndOldPackets()
        {
            var line = new LiveTimeline();
            var a = new LivePacket { Seq = 5, Start = 10.0 };
            a.Samples.Add(At(0f, 0f)); a.Samples.Add(At(0f, 1f));
            Assert.IsTrue(line.Add(a, 100.0));
            Assert.IsFalse(line.Add(a, 100.5), "the same packet again");
            var old = new LivePacket { Seq = 4, Start = 9.0 };
            old.Samples.Add(At(0f, -5f));
            Assert.IsFalse(line.Add(old, 101.0), "an older one");
            var wrap = new LivePacket { Seq = 6, Start = 10.25 };
            wrap.Samples.Add(At(0f, 2f));
            Assert.IsTrue(line.Add(wrap, 101.0));
            Assert.AreEqual(3, line.Count);
        }
    }
}
