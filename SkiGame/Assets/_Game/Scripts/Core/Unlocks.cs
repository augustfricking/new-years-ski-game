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
            new[] { 2000f, 3500f, 5000f },    // Parken (score)
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

        static Unlock Slope(CharacterId who, string id, string name, ItemKind kind, int value, SlopeId s, int tier) =>
            new Unlock { Who = who, Id = id, Name = name, Kind = kind, Value = value, Goal = GoalText(s, tier), Earned = p => Reached(p, s, tier), Slope = (int)s, Tier = tier };

        static Unlock Runs(CharacterId who, string id, string name, int value, int runs) =>
            new Unlock { Who = who, Id = id, Name = name, Kind = ItemKind.Gear, Value = value, Goal = runs + " runs", Earned = p => p.runs >= runs };

        const CharacterId J = CharacterId.Jonte, M = CharacterId.Maja, E = CharacterId.Erik, L = CharacterId.Linn;

        public static readonly Unlock[] All =
        {
            // Jonte, the snowboarder: the park, speed and trouble.
            Slope(J, "graffiti", "Graffiti", ItemKind.Pattern, 8, SlopeId.Blue, 0),
            Slope(J, "snapback", "Snapback", ItemKind.Headgear, 8, SlopeId.Blue, 1),
            Slope(J, "rainbow", "Rainbow suit", ItemKind.Suit, 4, SlopeId.Blue, 2),
            new Unlock { Who = J, Id = "tiger", Name = "Tiger stripes", Kind = ItemKind.Pattern, Value = 5, Goal = "3000 in the park", Earned = p => p.bestPark >= 3000f },
            new Unlock { Who = J, Id = "flames", Name = "Flames", Kind = ItemKind.Pattern, Value = 6, Goal = "100 km/h", Earned = p => p.topSpeed * 3.6f >= 100f },
            new Unlock { Who = J, Id = "goalie", Name = "Goalie mask", Kind = ItemKind.Headgear, Value = 3, Goal = "10 kids, one run", Earned = p => p.kidsHitOneRun >= 10 },
            new Unlock { Who = J, Id = "boxer", Name = "Boxer", Kind = ItemKind.Headgear, Value = 4, Goal = "25 knockouts", Earned = p => p.knockouts >= 25 },
            new Unlock { Who = J, Id = "ducks", Name = "Ducks", Kind = ItemKind.Pattern, Value = PatternDucks, Goal = "100 kids in all", Earned = p => p.kidsTotal >= 100 },
            new Unlock { Who = J, Id = "toxic", Name = "Toxic green", Kind = ItemKind.Colour, Value = FreeColours, Goal = "50 knockouts", Earned = p => p.knockouts >= 50 },
            Runs(J, "zebra", "Zebra board", 3, 10), Runs(J, "lava", "Lava board", 4, 25), Runs(J, "galaxy", "Galaxy board", 5, 50),
            // Maja: the kids' slope, the DJ and tricks.
            Slope(M, "candy", "Candy", ItemKind.Pattern, 7, SlopeId.Green, 0),
            Slope(M, "bunny", "Bunny ears", ItemKind.Headgear, 7, SlopeId.Green, 1),
            Slope(M, "ginger", "Gingerbread", ItemKind.Suit, 3, SlopeId.Green, 2),
            new Unlock { Who = M, Id = "banana", Name = "Banana suit", Kind = ItemKind.Suit, Value = 2, Goal = "5 songs from the DJ", Earned = p => p.songsRequested >= 5 },
            new Unlock { Who = M, Id = "crown", Name = "Crown", Kind = ItemKind.Headgear, Value = 5, Goal = "A 1000-point trick", Earned = p => p.bestTrick >= 1000 },
            new Unlock { Who = M, Id = "unicorn", Name = "Unicorn", Kind = ItemKind.Headgear, Value = 6, Goal = "All of Maja's others", Earned = p => AllOthers(M, "unicorn", p) },
            new Unlock { Who = M, Id = "hearts", Name = "Hearts", Kind = ItemKind.Pattern, Value = PatternHearts, Goal = "15 DJ songs", Earned = p => p.songsRequested >= 15 },
            new Unlock { Who = M, Id = "neonpink", Name = "Neon pink", Kind = ItemKind.Colour, Value = FreeColours + 1, Goal = "A 2000-point trick", Earned = p => p.bestTrick >= 2000 },
            Runs(M, "candycane", "Candy cane skis", 6, 10), Runs(M, "ice", "Ice skis", 7, 25), Runs(M, "diamond", "Diamond skis", 8, 50),
            // Erik: the slalom, the stein and the whole mountain.
            Slope(E, "race", "Race stripes", ItemKind.Pattern, 9, SlopeId.Red, 0),
            Slope(E, "aero", "Aero helmet", ItemKind.Headgear, 9, SlopeId.Red, 1),
            Slope(E, "racesuit", "Race suit", ItemKind.Suit, 5, SlopeId.Red, 2),
            new Unlock { Who = E, Id = "viking", Name = "Viking", Kind = ItemKind.Headgear, Value = 2, Goal = "Stein in 8 s", Earned = p => p.bestChug > 0f && p.bestChug <= 8f },
            new Unlock { Who = E, Id = "gold", Name = "Gold suit", Kind = ItemKind.Suit, Value = 1, Goal = "Finish all five runs", Earned = p => p.FinishedCount >= 5 },
            new Unlock { Who = E, Id = "lightning", Name = "Lightning", Kind = ItemKind.Pattern, Value = PatternLightning, Goal = "110 km/h", Earned = p => p.topSpeed * 3.6f >= 110f },
            new Unlock { Who = E, Id = "electric", Name = "Electric blue", Kind = ItemKind.Colour, Value = FreeColours + 2, Goal = "Stein in 6 s", Earned = p => p.bestChug > 0f && p.bestChug <= 6f },
            Runs(E, "racing", "Racing skis", 9, 10), Runs(E, "chrome", "Chrome skis", 10, 25), Runs(E, "goldskis", "Gold skis", 11, 50),
            // Linn: the black run and the backcountry.
            Slope(L, "skulls", "Skulls", ItemKind.Pattern, 10, SlopeId.Black, 0),
            Slope(L, "devil", "Devil horns", ItemKind.Headgear, 10, SlopeId.Black, 1),
            Slope(L, "midnight", "Midnight suit", ItemKind.Suit, 6, SlopeId.Black, 2),
            Slope(L, "pines", "Pines", ItemKind.Pattern, 11, SlopeId.OffPiste, 0),
            Slope(L, "antlers", "Antlers", ItemKind.Headgear, 11, SlopeId.OffPiste, 1),
            Slope(L, "yeti", "Yeti suit", ItemKind.Suit, 7, SlopeId.OffPiste, 2),
            new Unlock { Who = L, Id = "leopard", Name = "Leopard", Kind = ItemKind.Pattern, Value = PatternLeopard, Goal = "100 runs", Earned = p => p.runs >= 100 },
            new Unlock { Who = L, Id = "ultraviolet", Name = "Ultraviolet", Kind = ItemKind.Colour, Value = FreeColours + 3, Goal = "25 clean runs", Earned = p => p.cleanRuns >= 25 },
            Runs(L, "camo", "Camo skis", 12, 10), Runs(L, "wolf", "Wolf skis", 13, 25), Runs(L, "aurora", "Aurora skis", 14, 50),
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
