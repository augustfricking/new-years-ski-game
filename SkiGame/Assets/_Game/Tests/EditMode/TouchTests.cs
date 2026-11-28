using NUnit.Framework;
using Ski.Core;
using UnityEngine;

namespace Ski.Tests
{
    /// <summary>The touch steering stick: eight ways the same distance out, a still middle, no flicker on the lines.</summary>
    public class TouchTests
    {
        static string Ask(int way)
        {
            TouchZones.Read(way, out float steer, out bool tuck, out bool brake);
            return ((brake ? "brake " : "") + (tuck ? "tuck " : "") + (steer < 0 ? "left" : steer > 0 ? "right" : "")).Trim();
        }

        static Vector2 At(float degrees, float r) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * r;

        [Test]
        public void EveryWayAsksForItsOwnThing()
        {
            string[] want = { "right", "tuck right", "tuck", "tuck left", "left", "brake left", "brake", "brake right" };
            for (int i = 0; i < 8; i++)
            {
                int way = TouchZones.Stick(At(i * 45f, 0.8f), -1, false);
                Assert.AreEqual(i, way, "way at " + (i * 45) + " degrees");
                Assert.AreEqual(want[i], Ask(way), "way " + i);
            }
            Assert.AreEqual("", Ask(-1), "the middle");
        }

        [Test]
        public void EveryWayIsTheSameDistanceOut()
        {
            // Just inside the middle nothing, just outside it each way, whichever way the thumb goes.
            for (float a = 0f; a < 360f; a += 7.5f)
            {
                Assert.AreEqual(-1, TouchZones.Stick(At(a, TouchZones.Dead - 0.01f), -1, false), "inside at " + a);
                Assert.AreNotEqual(-1, TouchZones.Stick(At(a, TouchZones.Dead + 0.01f), -1, false), "outside at " + a);
            }
            // Further out than the knob goes still counts by its direction.
            Assert.AreEqual(4, TouchZones.Stick(new Vector2(-3f, 0.1f), -1, false));
        }

        [Test]
        public void EveryWayIsTheSameWidth()
        {
            int[] count = new int[8];
            for (float a = 0.25f; a < 360f; a += 0.5f) count[TouchZones.Stick(At(a, 0.8f), -1, false)]++;
            for (int i = 0; i < 8; i++) Assert.AreEqual(90, count[i], "half-degrees in way " + i);
        }

        [Test]
        public void AThumbOnALineKeepsItsWay()
        {
            float line = 22.5f, s = TouchZones.AngleStick;
            // Just past the line between right (0) and up-right (1): the way the thumb came from holds...
            Assert.AreEqual(0, TouchZones.Stick(At(line + s * 0.5f, 0.8f), 0, true));
            Assert.AreEqual(1, TouchZones.Stick(At(line - s * 0.5f, 0.8f), 1, true));
            // ...until it is clearly over.
            Assert.AreEqual(1, TouchZones.Stick(At(line + s * 1.5f, 0.8f), 0, true));
            Assert.AreEqual(0, TouchZones.Stick(At(line - s * 1.5f, 0.8f), 1, true));
            // The same across the line round from left (4) to down-left (5), past the half turn.
            Assert.AreEqual(4, TouchZones.Stick(At(180f + line + s * 0.5f, 0.8f), 4, true));
            // The middle's edge holds too: in and out a little does not switch.
            Assert.AreEqual(2, TouchZones.Stick(At(90f, TouchZones.Dead - TouchZones.DeadStick * 0.5f), 2, true));
            Assert.AreEqual(-1, TouchZones.Stick(At(90f, TouchZones.Dead + TouchZones.DeadStick * 0.5f), -1, true));
            Assert.AreEqual(-1, TouchZones.Stick(At(90f, TouchZones.Dead - TouchZones.DeadStick * 1.5f), 2, true));
        }
    }
}
