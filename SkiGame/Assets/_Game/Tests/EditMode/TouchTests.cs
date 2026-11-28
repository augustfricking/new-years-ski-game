using NUnit.Framework;
using Ski.Core;

namespace Ski.Tests
{
    /// <summary>The touch steering box: nine zones, and no flicker on the lines between them.</summary>
    public class TouchTests
    {
        [Test]
        public void EveryZoneAsksForItsOwnThing()
        {
            // Across (left to right) and down (top to bottom), the middle of each third.
            float[] at = { 0.15f, 0.5f, 0.85f };
            string[,] want =
            {
                { "tuck left", "tuck", "tuck right" },
                { "left", "", "right" },
                { "brake left", "brake", "brake right" },
            };
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    int col = TouchZones.Zone(at[c], 0, 0f), row = TouchZones.Zone(at[r], 0, 0f);
                    TouchZones.Read(col, row, out float steer, out bool tuck, out bool brake);
                    string got = ((brake ? "brake " : "") + (tuck ? "tuck " : "") + (steer < 0 ? "left" : steer > 0 ? "right" : "")).Trim();
                    Assert.AreEqual(want[r, c], got, "zone at column " + c + ", row " + r);
                }
        }

        [Test]
        public void BeyondTheBoxCountsAsItsEdge()
        {
            Assert.AreEqual(-1, TouchZones.Zone(-0.4f, 0, TouchZones.Stick));
            Assert.AreEqual(1, TouchZones.Zone(1.7f, 0, TouchZones.Stick));
        }

        [Test]
        public void AThumbOnALineKeepsItsZone()
        {
            float s = TouchZones.Stick, line = 1f / 3f;
            // Just past the line from either side: the zone the thumb came from holds...
            Assert.AreEqual(-1, TouchZones.Zone(line + s * 0.5f, -1, s));
            Assert.AreEqual(0, TouchZones.Zone(line - s * 0.5f, 0, s));
            Assert.AreEqual(1, TouchZones.Zone(2f / 3f - s * 0.5f, 1, s));
            // ...until it is clearly over.
            Assert.AreEqual(0, TouchZones.Zone(line + s * 1.5f, -1, s));
            Assert.AreEqual(-1, TouchZones.Zone(line - s * 1.5f, 0, s));
            Assert.AreEqual(0, TouchZones.Zone(2f / 3f - s * 1.5f, 1, s));
            // A first touch goes by the lines themselves.
            Assert.AreEqual(-1, TouchZones.Zone(line - 0.001f, 0, 0f));
            Assert.AreEqual(0, TouchZones.Zone(line + 0.001f, 0, 0f));
        }
    }
}
