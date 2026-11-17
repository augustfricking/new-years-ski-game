using NUnit.Framework;
using Ski.Core;

namespace Ski.Tests
{
    public class ShopTests
    {
        [Test]
        public void ColoursAndPlainPatternsAreFreeTheRestIsEarned()
        {
            var p = new Progress();
            Assert.IsTrue(Unlocks.Available(p, ItemKind.Pattern, 1), "stripes are free");
            Assert.IsTrue(Unlocks.Available(p, ItemKind.Headgear, 1), "the beanie is free");
            Assert.IsFalse(Unlocks.Available(p, ItemKind.Headgear, 3), "the goalie mask is earned");
            Assert.IsFalse(Unlocks.Available(p, ItemKind.Headgear, 6), "the unicorn is earned last");

            p.kidsHitOneRun = 10;
            Assert.IsTrue(Unlocks.Available(p, ItemKind.Headgear, 3), "ten kids in one run: goalie mask");
        }

        [Test]
        public void TheUnicornNeedsEverythingElse()
        {
            var p = new Progress { kidsHitOneRun = 12, slopesFinished = 31, bestChug = 7f, bestPark = 3500f, bestBlack = 65f, bestTrick = 1200, knockouts = 30, songsRequested = 5 };
            Assert.IsTrue(Unlocks.Available(p, ItemKind.Headgear, 6));
            p.songsRequested = 4;
            Assert.IsFalse(Unlocks.Available(p, ItemKind.Headgear, 6));
            Assert.IsFalse(Unlocks.Available(p, ItemKind.Suit, 2), "and the banana suit waits for the fifth song");
        }

        [Test]
        public void NewlyEarnedIsAnnouncedOnce()
        {
            var before = new Progress { knockouts = 24 };
            var after = new Progress { knockouts = 25 };
            var got = Unlocks.NewlyEarned(before, after);
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual("boxer", got[0].Id);
            Assert.AreEqual(0, Unlocks.NewlyEarned(after, after).Count);
        }

        [Test]
        public void EachSlopeHasThreeItemsThatGetHarder()
        {
            for (int slope = 0; slope < 5; slope++)
            {
                int n = 0;
                foreach (var u in Unlocks.All) if (u.Slope == slope) n++;
                Assert.AreEqual(3, n, "three items on slope " + slope);
                var g = Unlocks.SlopeGoals[slope];
                if ((SlopeId)slope == SlopeId.Blue) Assert.IsTrue(g[0] < g[1] && g[1] < g[2], "park: more and more points");
                else Assert.IsTrue(g[0] > g[1] && g[1] > g[2], "times: faster and faster");
            }
        }

        [Test]
        public void SlopeItemsFollowTheBestRun()
        {
            var p = new Progress();
            Assert.IsFalse(Unlocks.Available(p, ItemKind.Pattern, 7), "nothing yet");
            p.Offer(SlopeId.Green, 50f, false);
            Assert.IsTrue(Unlocks.Available(p, ItemKind.Pattern, 7), "a solid run: candy");
            Assert.IsFalse(Unlocks.Available(p, ItemKind.Headgear, 7));
            p.Offer(SlopeId.Green, 60f, false);
            Assert.AreEqual(50f, p.Best(SlopeId.Green), "a slower run keeps the best");
            p.Offer(SlopeId.Green, 34f, false);
            Assert.IsTrue(Unlocks.Available(p, ItemKind.Suit, 3), "near perfect: gingerbread");
            p.Offer(SlopeId.Blue, 3600f, true);
            Assert.IsTrue(Unlocks.Available(p, ItemKind.Headgear, 8), "3500 in the park: snapback");
            Assert.IsFalse(Unlocks.Available(p, ItemKind.Suit, 4));
            // The unicorn is about the other challenges, not the slope times.
            var q = new Progress { kidsHitOneRun = 12, slopesFinished = 31, bestChug = 7f, bestPark = 3500f, bestBlack = 65f, bestTrick = 1200, knockouts = 30, songsRequested = 5 };
            Assert.IsTrue(Unlocks.Available(q, ItemKind.Headgear, 6));
        }

        [Test]
        public void OutfitsTravelAsShortCodesAndLockedItemsFallBack()
        {
            var o = new Outfit { jacket = 4, trim = 0, pants = -1, helmet = 11, pattern = 5, headgear = 3, suit = 1 };
            var back = Outfit.Parse(o.Code);
            Assert.AreEqual(o.Code, back.Code);
            Assert.AreEqual(0, Outfit.Parse("rubbish").pattern);
            var allowed = Unlocks.Allowed(new Progress(), o);
            Assert.AreEqual(0, allowed.pattern);
            Assert.AreEqual(0, allowed.headgear);
            Assert.AreEqual(0, allowed.suit);
            Assert.AreEqual(4, allowed.jacket, "colours are always yours");
        }
    }
}
