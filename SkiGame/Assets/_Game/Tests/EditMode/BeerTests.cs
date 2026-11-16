using NUnit.Framework;
using Ski.Core;

namespace Ski.Tests
{
    public class BeerTests
    {
        [Test]
        public void ARoundEndsAfterAMinute()
        {
            var g = new ChugGame(5);
            g.Start();
            for (int i = 0; i < 61 * 60; i++) g.Step(1f / 60f);
            Assert.IsTrue(g.TimedOut);
            Assert.IsFalse(g.Running);
            Assert.IsFalse(g.Finished);
            Assert.AreEqual(ChugGame.TimeLimit, g.Time, 0.001f);
        }
    }
}
