using System;
using System.Collections.Generic;

namespace Ski.Core
{
    public enum ItemKind { Pattern, Headgear, Suit, Gear, Colour }

    /// <summary>
    /// How a character is dressed: colour picks (-1 = their own), a pattern, headgear, a suit, and
    /// the skis or board (a design and two colours).
    /// </summary>
    [Serializable]
    public sealed class Outfit
    {
        public int jacket = -1, trim = -1, pants = -1, helmet = -1;
        public int pattern, headgear, suit;
        public int gear, gearColour = -1, gearAccent = -1;

        public Outfit Copy() => (Outfit)MemberwiseClone();

        /// <summary>A short text form, for sending to other players.</summary>
        public string Code => jacket + "," + trim + "," + pants + "," + helmet + "," + pattern + "," + headgear + "," + suit + "," + gear + "," + gearColour + "," + gearAccent;

        public static Outfit Parse(string code)
        {
            var o = new Outfit();
            if (string.IsNullOrEmpty(code)) return o;
            var p = code.Split(',');
            int Get(int i, int d) => p.Length > i && int.TryParse(p[i], out int v) ? v : d;
            o.jacket = Get(0, -1); o.trim = Get(1, -1); o.pants = Get(2, -1); o.helmet = Get(3, -1);
            o.pattern = Get(4, 0); o.headgear = Get(5, 0); o.suit = Get(6, 0);
            o.gear = Get(7, 0); o.gearColour = Get(8, -1); o.gearAccent = Get(9, -1);
            return o;
        }
    }

    /// <summary>What one character has done on this device, for unlocking their things in the shop.</summary>
    [Serializable]
    public sealed class Progress
    {
        public int kidsHitOneRun;    // most kids hit in one run on the kids' slope
        public int slopesFinished;   // bit per SlopeId
        public float bestChug;       // seconds, 0 = never
        public float bestPark;       // points on Blue
        public float bestBlack;      // seconds, 0 = never
        public int bestTrick;        // points for one trick
        public int knockouts;        // people knocked down in the tavern, all visits
        public int songsRequested;   // songs asked of the DJ
        public float[] best;         // per SlopeId: best time (s), or best score on the park; 0 = none
        public int runs;             // runs finished
        public float topSpeed;       // m/s, the fastest ever
        public int kidsTotal;        // kids hit on the kids' slope, all runs
        public int cleanRuns;        // runs with no crash and no missed gate
        public float longestAir;     // seconds in the air in one jump, the longest
        public float longestGrind;   // metres on one rail or box, the longest
        public int crashes;          // wipeouts, all runs
        public float distanceKm;     // run length skied, all finished runs
        public int liftRides, tavernVisits, flushes;
        public int greenNoKids;      // runs down Barnbacken without bumping a kid
        public int redClean;         // runs down Slalombacken through every gate with no penalty
        public int[] runsOn;         // finished runs per SlopeId

        public int RunsOn(SlopeId s) => runsOn != null && runsOn.Length > (int)s ? runsOn[(int)s] : 0;
        public void AddRunOn(SlopeId s)
        {
            if (runsOn == null || runsOn.Length < 5) { var r = new int[5]; if (runsOn != null) Array.Copy(runsOn, r, runsOn.Length); runsOn = r; }
            runsOn[(int)s]++;
        }

        public float Best(SlopeId s) => best != null && best.Length > (int)s ? best[(int)s] : 0f;
        public bool Finished(SlopeId s) => (slopesFinished & (1 << (int)s)) != 0;
        public int FinishedCount { get { int n = 0; for (int i = 0; i < 5; i++) if ((slopesFinished & (1 << i)) != 0) n++; return n; } }

        /// <summary>Keeps a run's result if it beats the best on that slope (lower time, higher score).</summary>
        public void Offer(SlopeId s, float value, bool higherIsBetter)
        {
            if (best == null || best.Length < 5) { var b = new float[5]; if (best != null) Array.Copy(best, b, best.Length); best = b; }
            float have = best[(int)s];
            if (have <= 0f || (higherIsBetter ? value > have : value < have)) best[(int)s] = value;
        }
    }

    /// <summary>One thing in the shop that one character has to earn.</summary>
    public sealed class Unlock
    {
        public string Id, Name, Goal;
        /// <summary>How to earn it, in a sentence or two (shown when the lock is hovered in the shop).</summary>
        public string How;
        public ItemKind Kind;
        public int Value;
        public CharacterId Who;
        public Func<Progress, bool> Earned;
        /// <summary>For a slope's items: which slope (-1 for the others) and how hard (0..2).</summary>
        public int Slope = -1, Tier = -1;
    }

    /// <summary>
    /// The shop's items. Most colours and the plain prints are free for everyone; everything else
    /// belongs to one of the four friends and is earned by playing as them. The harder the
    /// challenge, the sillier the reward.
    /// </summary>
    public static class Unlocks
    {
        public static readonly string[] Patterns = { "Plain", "Stripes", "Checks", "Dots", "Camo", "Tiger", "Flames", "Candy", "Graffiti", "Race stripes", "Skulls", "Pines",
                                                       "Fur", "Rainbow", "Ducks", "Hearts", "Lightning", "Leopard" };
        public static readonly string[] Headgear = { "Helmet", "Beanie", "Viking", "Goalie mask", "Boxer", "Crown", "Unicorn", "Bunny ears", "Snapback", "Aero helmet", "Devil horns", "Antlers" };
        public static readonly string[] Suits = { "Own", "Gold", "Banana", "Gingerbread", "Rainbow", "Race suit", "Midnight", "Yeti" };
        public static readonly string[] Gear = { "Own", "Wood", "Neon", "Zebra", "Lava", "Galaxy", "Candy cane", "Ice", "Diamond", "Racing", "Chrome", "Gold", "Camo", "Wolf", "Aurora" };
        /// <summary>Prints that only come with a suit (the yeti's fur, the rainbow), after the ones in the list.</summary>
        public const int PatternFur = 12, PatternRainbow = 13;
        /// <summary>The loud prints, each earned by one friend's hardest feats.</summary>
        public const int PatternDucks = 14, PatternHearts = 15, PatternLightning = 16, PatternLeopard = 17;
        /// <summary>Rare colours after the free ones (Scripts/Game/OutfitPaint.Colours keeps the same order).</summary>
        public static readonly string[] RareColours = { "Toxic green", "Neon pink", "Electric blue", "Ultraviolet" };
        public const int FreeColours = 14;
        /// <summary>Items free for everyone: the first prints, the beanie, two board and ski designs.</summary>
        public const int FreePatterns = 5, FreeHeadgear = 2, FreeGear = 3;

        /// <summary>
        /// The three results that earn a slope's items: a solid run, a good one, and one close to
        /// perfect. Set from what people actually ski (the best humans run about 0.62 x the bot's
        /// time), so the last one takes many runs. Times in seconds; the park in points.
        /// </summary>
        public static readonly float[][] SlopeGoals =
        {
            new[] { 53f, 41f, 34.6f },        // Barnbacken
            new[] { 3000f, 5500f, 8000f },    // Parken (score)
            new[] { 93f, 73f, 61f },          // Slalombacken
            new[] { 64f, 50f, 42f },          // Svarta Väggen
            new[] { 109f, 85f, 71.4f },       // Ravinen (off-piste)
        };

        static bool Reached(Progress p, SlopeId s, int tier)
        {
            float b = p.Best(s), goal = SlopeGoals[(int)s][tier];
            return s == SlopeId.Blue ? b >= goal : b > 0f && b <= goal;
        }

        static string GoalText(SlopeId s, int tier)
        {
            float goal = SlopeGoals[(int)s][tier];
            return s == SlopeId.Blue ? Courses.Names[(int)s] + " " + goal.ToString("0") + " p" : Courses.Names[(int)s] + " under " + goal.ToString("0.#") + " s";
        }

        static Unlock Slope(CharacterId who, string id, string name, ItemKind kind, int value, SlopeId s, int tier)
        {
            float goal = SlopeGoals[(int)s][tier];
            string how = s == SlopeId.Blue ? "Score " + goal.ToString("0") + " points in " + Courses.Names[(int)s] + " as " + SkierInfo.Get(who).Name + "."
                                           : "Ski " + Courses.Names[(int)s] + " in under " + goal.ToString("0.#") + " seconds as " + SkierInfo.Get(who).Name + " (penalties count).";
            return new Unlock { Who = who, Id = id, Name = name, Kind = kind, Value = value, Goal = GoalText(s, tier), How = how, Earned = p => Reached(p, s, tier), Slope = (int)s, Tier = tier };
        }

        static Unlock Runs(CharacterId who, string id, string name, int value, int runs) =>
            new Unlock { Who = who, Id = id, Name = name, Kind = ItemKind.Gear, Value = value, Goal = runs + " runs", How = "Finish " + runs + " runs as " + SkierInfo.Get(who).Name + ", on any slope.", Earned = p => p.runs >= runs };

        static Unlock Item(CharacterId who, string id, string name, ItemKind kind, int value, string goal, string how, Func<Progress, bool> earned) =>
            new Unlock { Who = who, Id = id, Name = name, Kind = kind, Value = value, Goal = goal, How = how, Earned = earned };

        const CharacterId J = CharacterId.Jonte, M = CharacterId.Maja, E = CharacterId.Erik, L = CharacterId.Linn;

        public static readonly Unlock[] All =
        {
            // Jonte, the snowboarder: the park, big air, speed and trouble.
            Slope(J, "graffiti", "Graffiti", ItemKind.Pattern, 8, SlopeId.Blue, 0),
            Slope(J, "snapback", "Snapback", ItemKind.Headgear, 8, SlopeId.Blue, 1),
            Slope(J, "rainbow", "Rainbow suit", ItemKind.Suit, 4, SlopeId.Blue, 2),
            Item(J, "tiger", "Tiger stripes", ItemKind.Pattern, 5, "3 s in the air", "Stay in the air for 3 seconds in one jump as Jonte. The big kickers in Parken can do it.", p => p.longestAir >= 3f),
            Item(J, "flames", "Flames", ItemKind.Pattern, 6, "100 km/h", "Reach 100 km/h as Jonte. A long tuck on Svarta Väggen does it.", p => p.topSpeed * 3.6f >= 100f),
            Item(J, "goalie", "Goalie mask", ItemKind.Headgear, 3, "10 kids, one run", "Bump into 10 kids in a single run down Barnbacken as Jonte.", p => p.kidsHitOneRun >= 10),
            Item(J, "boxer", "Boxer", ItemKind.Headgear, 4, "25 knockouts", "Knock 25 people over in the after-ski as Jonte (they get up again).", p => p.knockouts >= 25),
            Item(J, "ducks", "Ducks", ItemKind.Pattern, PatternDucks, "10 flushes", "Flush the after-ski toilet 10 times as Jonte.", p => p.flushes >= 10),
            Item(J, "toxic", "Toxic green", ItemKind.Colour, FreeColours, "50 wipeouts", "Wipe out 50 times as Jonte. It is the trying that counts.", p => p.crashes >= 50),
            Runs(J, "zebra", "Zebra board", 3, 10),
            Item(J, "lava", "Lava board", ItemKind.Gear, 4, "14 m grind", "Grind 14 metres on one rail or box in Parken as Jonte: the long boxes, end to end.", p => p.longestGrind >= 14f),
            Item(J, "galaxy", "Galaxy board", ItemKind.Gear, 5, "25 km skied", "Ski 25 km in total as Jonte (every finished run counts its length).", p => p.distanceKm >= 25f),
            // Maja: the kids' slope, the DJ, tricks and good manners.
            Slope(M, "candy", "Candy", ItemKind.Pattern, 7, SlopeId.Green, 0),
            Slope(M, "bunny", "Bunny ears", ItemKind.Headgear, 7, SlopeId.Green, 1),
            Slope(M, "ginger", "Gingerbread", ItemKind.Suit, 3, SlopeId.Green, 2),
            Item(M, "banana", "Banana suit", ItemKind.Suit, 2, "5 songs from the DJ", "Ask the after-ski DJ for 5 songs as Maja.", p => p.songsRequested >= 5),
            Item(M, "crown", "Crown", ItemKind.Headgear, 5, "A 1000-point trick", "Land one trick worth 1000 points as Maja: spins, flips and grabs add up.", p => p.bestTrick >= 1000),
            Item(M, "unicorn", "Unicorn", ItemKind.Headgear, 6, "All of Maja's others", "Earn every other one of Maja's things first.", p => AllOthers(M, "unicorn", p)),
            Item(M, "hearts", "Hearts", ItemKind.Pattern, PatternHearts, "A kind run", "Ski Barnbacken from top to bottom without bumping a single kid as Maja.", p => p.greenNoKids >= 1),
            Item(M, "neonpink", "Neon pink", ItemKind.Colour, FreeColours + 1, "10 after-skis", "Go to the after-ski 10 times as Maja.", p => p.tavernVisits >= 10),
            Runs(M, "candycane", "Candy cane skis", 6, 10),
            Item(M, "ice", "Ice skis", ItemKind.Gear, 7, "15 lift rides", "Ride the chairlift up 15 times as Maja.", p => p.liftRides >= 15),
            Item(M, "diamond", "Diamond skis", ItemKind.Gear, 8, "2 s in the air", "Stay in the air for 2 seconds in one jump as Maja.", p => p.longestAir >= 2f),
            // Erik: the slalom, the stein and the whole mountain.
            Slope(E, "race", "Race stripes", ItemKind.Pattern, 9, SlopeId.Red, 0),
            Slope(E, "aero", "Aero helmet", ItemKind.Headgear, 9, SlopeId.Red, 1),
            Slope(E, "racesuit", "Race suit", ItemKind.Suit, 5, SlopeId.Red, 2),
            Item(E, "viking", "Viking", ItemKind.Headgear, 2, "Stein in 8 s", "Empty the beer stein in 8 seconds or less as Erik.", p => p.bestChug > 0f && p.bestChug <= 8f),
            Item(E, "gold", "Gold suit", ItemKind.Suit, 1, "Finish all five runs", "Finish each of the five runs at least once as Erik.", p => p.FinishedCount >= 5),
            Item(E, "lightning", "Lightning", ItemKind.Pattern, PatternLightning, "110 km/h", "Reach 110 km/h as Erik.", p => p.topSpeed * 3.6f >= 110f),
            Item(E, "electric", "Electric blue", ItemKind.Colour, FreeColours + 2, "A clean slalom", "Ski Slalombacken through every gate with no penalty at all as Erik.", p => p.redClean >= 1),
            Runs(E, "racing", "Racing skis", 9, 10),
            Item(E, "chrome", "Chrome skis", ItemKind.Gear, 10, "Slalombacken x10", "Finish Slalombacken 10 times as Erik.", p => p.RunsOn(SlopeId.Red) >= 10),
            Item(E, "goldskis", "Gold skis", ItemKind.Gear, 11, "50 km skied", "Ski 50 km in total as Erik (every finished run counts its length).", p => p.distanceKm >= 50f),
            // Linn: the black run and the backcountry.
            Slope(L, "skulls", "Skulls", ItemKind.Pattern, 10, SlopeId.Black, 0),
            Slope(L, "devil", "Devil horns", ItemKind.Headgear, 10, SlopeId.Black, 1),
            Slope(L, "midnight", "Midnight suit", ItemKind.Suit, 6, SlopeId.Black, 2),
            Slope(L, "pines", "Pines", ItemKind.Pattern, 11, SlopeId.OffPiste, 0),
            Slope(L, "antlers", "Antlers", ItemKind.Headgear, 11, SlopeId.OffPiste, 1),
            Slope(L, "yeti", "Yeti suit", ItemKind.Suit, 7, SlopeId.OffPiste, 2),
            Item(L, "leopard", "Leopard", ItemKind.Pattern, PatternLeopard, "Svarta Väggen x10", "Finish Svarta Väggen 10 times as Linn.", p => p.RunsOn(SlopeId.Black) >= 10),
            Item(L, "ultraviolet", "Ultraviolet", ItemKind.Colour, FreeColours + 3, "25 clean runs", "Finish 25 runs without a crash or a missed gate as Linn.", p => p.cleanRuns >= 25),
            Runs(L, "camo", "Camo skis", 12, 10),
            Item(L, "wolf", "Wolf skis", ItemKind.Gear, 13, "Ravinen x10", "Finish Ravinen, the off-piste, 10 times as Linn.", p => p.RunsOn(SlopeId.OffPiste) >= 10),
            Item(L, "aurora", "Aurora skis", ItemKind.Gear, 14, "100 km skied", "Ski 100 km in total as Linn (every finished run counts its length).", p => p.distanceKm >= 100f),
        };

        static bool AllOthers(CharacterId who, string except, Progress p)
        {
            foreach (var u in All) if (u.Who == who && u.Id != except && !u.Earned(p)) return false;
            return true;
        }

        /// <summary>The unlock that guards an item, or null when the item is free for everyone.</summary>
        public static Unlock For(ItemKind kind, int value)
        {
            foreach (var u in All) if (u.Kind == kind && u.Value == value) return u;
            return null;
        }

        static int FreeCount(ItemKind kind) => kind == ItemKind.Pattern ? FreePatterns : kind == ItemKind.Headgear ? FreeHeadgear
                                             : kind == ItemKind.Gear ? FreeGear : kind == ItemKind.Colour ? FreeColours : 1;

        /// <summary>Whether this character may wear this item: free ones always, their own once earned, others' never.</summary>
        public static bool Available(CharacterId who, Progress p, ItemKind kind, int value)
        {
            if (value < FreeCount(kind)) return true;
            var u = For(kind, value);
            return u != null && u.Who == who && u.Earned(p);
        }

        /// <summary>Whether the shop lists this item for this character (free, or theirs to earn).</summary>
        public static bool Listed(CharacterId who, ItemKind kind, int value)
        {
            if (value < FreeCount(kind)) return true;
            var u = For(kind, value);
            return u != null && u.Who == who;
        }

        /// <summary>The unlocks newly earned between two states of one character's progress.</summary>
        public static List<Unlock> NewlyEarned(CharacterId who, Progress before, Progress after)
        {
            var list = new List<Unlock>();
            foreach (var u in All) if (u.Who == who && !u.Earned(before) && u.Earned(after)) list.Add(u);
            return list;
        }

        /// <summary>An outfit with anything not earned (or someone else's) put back to the free version.</summary>
        public static Outfit Allowed(CharacterId who, Progress p, Outfit o)
        {
            var r = o.Copy();
            if (!Available(who, p, ItemKind.Pattern, r.pattern)) r.pattern = 0;
            if (!Available(who, p, ItemKind.Headgear, r.headgear)) r.headgear = 0;
            if (!Available(who, p, ItemKind.Suit, r.suit)) r.suit = 0;
            if (!Available(who, p, ItemKind.Gear, r.gear)) r.gear = 0;
            int Colour(int c) => c < 0 || Available(who, p, ItemKind.Colour, c) ? c : -1;
            r.jacket = Colour(r.jacket); r.trim = Colour(r.trim); r.pants = Colour(r.pants); r.helmet = Colour(r.helmet);
            r.gearColour = Colour(r.gearColour); r.gearAccent = Colour(r.gearAccent);
            return r;
        }
    }
}
