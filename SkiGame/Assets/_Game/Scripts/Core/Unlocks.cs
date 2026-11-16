using System;
using System.Collections.Generic;

namespace Ski.Core
{
    public enum ItemKind { Pattern, Headgear, Suit }

    /// <summary>How a character is dressed: colour picks (-1 = their own), a pattern, headgear and a suit.</summary>
    [Serializable]
    public sealed class Outfit
    {
        public int jacket = -1, trim = -1, pants = -1, helmet = -1;
        public int pattern, headgear, suit;

        public Outfit Copy() => (Outfit)MemberwiseClone();

        /// <summary>A short text form, for sending to other players: "j,t,p,h,pattern,headgear,suit".</summary>
        public string Code => jacket + "," + trim + "," + pants + "," + helmet + "," + pattern + "," + headgear + "," + suit;

        public static Outfit Parse(string code)
        {
            var o = new Outfit();
            if (string.IsNullOrEmpty(code)) return o;
            var p = code.Split(',');
            int Get(int i, int d) => p.Length > i && int.TryParse(p[i], out int v) ? v : d;
            o.jacket = Get(0, -1); o.trim = Get(1, -1); o.pants = Get(2, -1); o.helmet = Get(3, -1);
            o.pattern = Get(4, 0); o.headgear = Get(5, 0); o.suit = Get(6, 0);
            return o;
        }
    }

    /// <summary>What this device has done so far, for unlocking things in the shop.</summary>
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

        public bool Finished(SlopeId s) => (slopesFinished & (1 << (int)s)) != 0;
        public int FinishedCount { get { int n = 0; for (int i = 0; i < 5; i++) if ((slopesFinished & (1 << i)) != 0) n++; return n; } }
    }

    /// <summary>One thing in the shop that has to be earned.</summary>
    public sealed class Unlock
    {
        public string Id, Name, Goal;
        public ItemKind Kind;
        public int Value;
        public Func<Progress, bool> Earned;
    }

    /// <summary>
    /// The shop's items. Colours and the plain patterns are free; the rest are earned, and the
    /// harder the challenge the sillier the reward.
    /// </summary>
    public static class Unlocks
    {
        public static readonly string[] Patterns = { "Plain", "Stripes", "Checks", "Dots", "Camo", "Tiger", "Flames" };
        public static readonly string[] Headgear = { "Helmet", "Beanie", "Viking", "Goalie mask", "Boxer", "Crown", "Unicorn" };
        public static readonly string[] Suits = { "Own", "Gold", "Banana" };

        public static readonly Unlock[] All =
        {
            new Unlock { Id = "tiger", Name = "Tiger stripes", Kind = ItemKind.Pattern, Value = 5, Goal = "3000 in the park", Earned = p => p.bestPark >= 3000f },
            new Unlock { Id = "flames", Name = "Flames", Kind = ItemKind.Pattern, Value = 6, Goal = "Black under 70 s", Earned = p => p.bestBlack > 0f && p.bestBlack <= 70f },
            new Unlock { Id = "viking", Name = "Viking", Kind = ItemKind.Headgear, Value = 2, Goal = "Stein in 8 s", Earned = p => p.bestChug > 0f && p.bestChug <= 8f },
            new Unlock { Id = "goalie", Name = "Goalie mask", Kind = ItemKind.Headgear, Value = 3, Goal = "10 kids, one run", Earned = p => p.kidsHitOneRun >= 10 },
            new Unlock { Id = "boxer", Name = "Boxer", Kind = ItemKind.Headgear, Value = 4, Goal = "25 knockouts", Earned = p => p.knockouts >= 25 },
            new Unlock { Id = "gold", Name = "Gold suit", Kind = ItemKind.Suit, Value = 1, Goal = "Finish all five runs", Earned = p => p.FinishedCount >= 5 },
            new Unlock { Id = "crown", Name = "Crown", Kind = ItemKind.Headgear, Value = 5, Goal = "A 1000-point trick", Earned = p => p.bestTrick >= 1000 },
            new Unlock { Id = "banana", Name = "Banana suit", Kind = ItemKind.Suit, Value = 2, Goal = "5 songs from the DJ", Earned = p => p.songsRequested >= 5 },
            new Unlock { Id = "unicorn", Name = "Unicorn", Kind = ItemKind.Headgear, Value = 6, Goal = "Everything else", Earned = p => EverythingElse(p) },
        };

        static bool EverythingElse(Progress p)
        {
            foreach (var u in All) if (u.Id != "unicorn" && !u.Earned(p)) return false;
            return true;
        }

        /// <summary>The unlock that guards an item, or null when the item is free.</summary>
        public static Unlock For(ItemKind kind, int value)
        {
            foreach (var u in All) if (u.Kind == kind && u.Value == value) return u;
            return null;
        }

        public static bool Available(Progress p, ItemKind kind, int value)
        {
            var u = For(kind, value);
            return u == null || u.Earned(p);
        }

        /// <summary>The unlocks newly earned between two states of progress (for a "you unlocked" note).</summary>
        public static List<Unlock> NewlyEarned(Progress before, Progress after)
        {
            var list = new List<Unlock>();
            foreach (var u in All) if (!u.Earned(before) && u.Earned(after)) list.Add(u);
            return list;
        }

        /// <summary>An outfit with anything not earned put back to the free version.</summary>
        public static Outfit Allowed(Progress p, Outfit o)
        {
            var r = o.Copy();
            if (!Available(p, ItemKind.Pattern, r.pattern)) r.pattern = 0;
            if (!Available(p, ItemKind.Headgear, r.headgear)) r.headgear = 0;
            if (!Available(p, ItemKind.Suit, r.suit)) r.suit = 0;
            return r;
        }
    }
}
