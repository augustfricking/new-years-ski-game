using NUnit.Framework;
using Ski.Core;

namespace Ski.Tests
{
    public class ShopTests
    {
        const CharacterId J = CharacterId.Jonte, M = CharacterId.Maja, E = CharacterId.Erik, L = CharacterId.Linn;

        [Test]
        public void FreeThingsForEveryoneTheRestBelongsToOneFriend()
        {
            var p = new Progress();
            foreach (CharacterId who in System.Enum.GetValues(typeof(CharacterId)))
            {
                Assert.IsTrue(Unlocks.Available(who, p, ItemKind.Pattern, 1), "stripes are free");
                Assert.IsTrue(Unlocks.Available(who, p, ItemKind.Headgear, 1), "the beanie is free");
                Assert.IsTrue(Unlocks.Available(who, p, ItemKind.Gear, 2), "neon is free");
            }
            foreach (var u in Unlocks.All)
            {
                int owners = 0;
                foreach (CharacterId who in System.Enum.GetValues(typeof(CharacterId))) if (Unlocks.Listed(who, u.Kind, u.Value)) owners++;
                Assert.AreEqual(1, owners, u.Id + " belongs to exactly one friend");
            }
        }

        [Test]
        public void TheGoalieMaskIsJontesAndOnlyHis()
        {
            var p = new Progress { kidsHitOneRun = 10 };
            Assert.IsTrue(Unlocks.Available(J, p, ItemKind.Headgear, 3));
            Assert.IsFalse(Unlocks.Available(E, p, ItemKind.Headgear, 3), "not Erik's, however many kids he hits");
            Assert.IsFalse(Unlocks.Listed(M, ItemKind.Headgear, 3));
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
            Assert.IsFalse(Unlocks.Available(M, p, ItemKind.Pattern, 7), "nothing yet");
            p.Offer(SlopeId.Green, 50f, false);
            Assert.IsTrue(Unlocks.Available(M, p, ItemKind.Pattern, 7), "a solid run: candy");
            Assert.IsFalse(Unlocks.Available(M, p, ItemKind.Headgear, 7));
            p.Offer(SlopeId.Green, 60f, false);
            Assert.AreEqual(50f, p.Best(SlopeId.Green), "a slower run keeps the best");
            p.Offer(SlopeId.Green, 34f, false);
            Assert.IsTrue(Unlocks.Available(M, p, ItemKind.Suit, 3), "near perfect: gingerbread");
            var q = new Progress();
            q.Offer(SlopeId.Blue, 3600f, true);
            Assert.IsTrue(Unlocks.Available(J, q, ItemKind.Headgear, 8), "3500 in the park: Jonte's snapback");
            Assert.IsFalse(Unlocks.Available(J, q, ItemKind.Suit, 4));
        }

        [Test]
        public void SkisAndBoardsComeWithRuns()
        {
            var p = new Progress { runs = 24 };
            Assert.IsTrue(Unlocks.Available(L, p, ItemKind.Gear, 12), "camo after 10 runs");
            Assert.IsFalse(Unlocks.Available(L, p, ItemKind.Gear, 13), "wolf after 25");
            p.runs = 25;
            Assert.IsTrue(Unlocks.Available(L, p, ItemKind.Gear, 13));
            Assert.IsFalse(Unlocks.Available(E, p, ItemKind.Gear, 13), "Linn's, not Erik's");
        }

        [Test]
        public void NewlyEarnedIsAnnouncedOnce()
        {
            var before = new Progress { knockouts = 24 };
            var after = new Progress { knockouts = 25 };
            var got = Unlocks.NewlyEarned(J, before, after);
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual("boxer", got[0].Id);
            Assert.AreEqual(0, Unlocks.NewlyEarned(J, after, after).Count);
            Assert.AreEqual(0, Unlocks.NewlyEarned(M, before, after).Count, "Maja earns nothing from Jonte's punches");
        }

        [Test]
        public void RareColoursAndLoudPrintsAreEarned()
        {
            var p = new Progress();
            Assert.IsTrue(Unlocks.Available(J, p, ItemKind.Colour, Unlocks.FreeColours - 1), "the ordinary colours are free");
            for (int i = 0; i < Unlocks.RareColours.Length; i++)
            {
                var u = Unlocks.For(ItemKind.Colour, Unlocks.FreeColours + i);
                Assert.IsNotNull(u, Unlocks.RareColours[i] + " has a goal");
                Assert.AreEqual(Unlocks.RareColours[i], u.Name);
                Assert.IsFalse(Unlocks.Available(u.Who, p, ItemKind.Colour, u.Value), u.Name + " is locked at first");
            }
            foreach (int pattern in new[] { Unlocks.PatternDucks, Unlocks.PatternHearts, Unlocks.PatternLightning, Unlocks.PatternLeopard })
            {
                Assert.IsNotNull(Unlocks.For(ItemKind.Pattern, pattern), Unlocks.Patterns[pattern] + " has a goal");
                Assert.IsFalse(Unlocks.Available(Unlocks.For(ItemKind.Pattern, pattern).Who, p, ItemKind.Pattern, pattern));
            }
            Assert.IsFalse(Unlocks.Listed(J, ItemKind.Pattern, Unlocks.PatternFur), "the fur only comes with the yeti suit");
            p.kidsTotal = 100;
            Assert.IsTrue(Unlocks.Available(J, p, ItemKind.Pattern, Unlocks.PatternDucks), "100 kids: Jonte's ducks");
            Assert.IsFalse(Unlocks.Available(M, p, ItemKind.Pattern, Unlocks.PatternDucks));
            p.cleanRuns = 25;
            Assert.IsTrue(Unlocks.Available(L, p, ItemKind.Colour, Unlocks.FreeColours + 3), "25 clean runs: Linn's ultraviolet");
            var o = new Outfit { jacket = Unlocks.FreeColours + 1, trim = Unlocks.FreeColours + 3, pants = 2, gearAccent = Unlocks.FreeColours };
            var allowed = Unlocks.Allowed(L, p, o);
            Assert.AreEqual(-1, allowed.jacket, "Maja's neon pink is not Linn's");
            Assert.AreEqual(Unlocks.FreeColours + 3, allowed.trim, "her own, earned");
            Assert.AreEqual(2, allowed.pants);
            Assert.AreEqual(-1, allowed.gearAccent);
        }

        [Test]
        public void OutfitsTravelAsShortCodesAndLockedItemsFallBack()
        {
            var o = new Outfit { jacket = 4, trim = 0, pants = -1, helmet = 11, pattern = 5, headgear = 3, suit = 1, gear = 4, gearColour = 2, gearAccent = 7 };
            var back = Outfit.Parse(o.Code);
            Assert.AreEqual(o.Code, back.Code);
            Assert.AreEqual(0, Outfit.Parse("rubbish").pattern);
            Assert.AreEqual(0, Outfit.Parse("1,2,3,4,0,0,0").gear, "codes from before the skis existed still read");
            var allowed = Unlocks.Allowed(J, new Progress(), o);
            Assert.AreEqual(0, allowed.pattern);
            Assert.AreEqual(0, allowed.headgear);
            Assert.AreEqual(0, allowed.suit);
            Assert.AreEqual(0, allowed.gear);
            Assert.AreEqual(4, allowed.jacket, "colours are always yours");
            Assert.AreEqual(2, allowed.gearColour);
        }
    }
}
