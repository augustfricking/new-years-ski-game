using NUnit.Framework;
using Ski.Core;

namespace Ski.Tests
{
    /// <summary>The touch steering stick: two halves, always a turn while held, no flicker on the middle line.</summary>
    public class TouchTests
    {
        [Test]
        public void EachHalfTurnsItsWay()
        {
            Assert.AreEqual(-1, TouchZones.Side(-0.6f, 0, false));
            Assert.AreEqual(1, TouchZones.Side(0.6f, 0, false));
            // Out past the knob's reach still counts.
            Assert.AreEqual(-1, TouchZones.Side(-3f, 1, true));
            Assert.AreEqual(1, TouchZones.Side(3f, -1, true));
        }

        [Test]
        public void HeldItAlwaysTurns()
        {
            // Even right on the middle, a held knob turns one way or the other: only letting go goes straight.
            for (float x = -1f; x <= 1f; x += 0.01f)
                foreach (int current in new[] { 0, -1, 1 })
                    Assert.AreNotEqual(0, TouchZones.Side(x, current, current != 0), "at " + x);
            Assert.AreNotEqual(0, TouchZones.Side(0f, 0, false));
        }

        [Test]
        public void AThumbOnTheMiddleKeepsItsSide()
        {
            float s = TouchZones.SideStick;
            // Just over the middle from either side: the side the thumb came from holds...
            Assert.AreEqual(-1, TouchZones.Side(s * 0.5f, -1, true));
            Assert.AreEqual(1, TouchZones.Side(-s * 0.5f, 1, true));
            // ...until it is clearly over.
            Assert.AreEqual(1, TouchZones.Side(s * 1.5f, -1, true));
            Assert.AreEqual(-1, TouchZones.Side(-s * 1.5f, 1, true));
            // A first touch goes by the middle itself.
            Assert.AreEqual(-1, TouchZones.Side(-0.001f, 0, false));
            Assert.AreEqual(1, TouchZones.Side(0.001f, 0, false));
        }
    }
}
