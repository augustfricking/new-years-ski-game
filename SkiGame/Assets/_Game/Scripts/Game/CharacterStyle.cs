using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>How each of the four friends looks: clothing and gear colours.</summary>
    public sealed class CharacterStyle
    {
        public Color Jacket, JacketTrim, Pants, Gloves, Helmet, Goggles, Hair, Skin, Gear, GearAccent, Boots;
        public float Height = 1f;   // relative body height
        public float SkiLength;     // metres (board length for the snowboard)
        public bool LongHair;

        static readonly CharacterStyle[] All =
        {
            new CharacterStyle // Jonte: snowboarder, baggy teal and mustard
            {
                Jacket = Palette.Hex("#18A6A6"), JacketTrim = Palette.Hex("#0E6E78"), Pants = Palette.Hex("#D9A441"), Gloves = Palette.Hex("#2C2A44"),
                Helmet = Palette.Hex("#2C2A44"), Goggles = Palette.Hex("#F2803A"), Hair = Palette.Hex("#3B2A24"), Skin = Palette.Hex("#E9B990"),
                Gear = Palette.Hex("#E8455A"), GearAccent = Palette.Hex("#F7F4EE"), Boots = Palette.Hex("#22202E"), Height = 1.03f, SkiLength = 1.52f,
            },
            new CharacterStyle // Maja: skiblades, magenta and white
            {
                Jacket = Palette.Hex("#D8437F"), JacketTrim = Palette.Hex("#F7F4EE"), Pants = Palette.Hex("#2C2A44"), Gloves = Palette.Hex("#F7F4EE"),
                Helmet = Palette.Hex("#F7F4EE"), Goggles = Palette.Hex("#6C58C9"), Hair = Palette.Hex("#E0B050"), Skin = Palette.Hex("#F1C6A7"),
                Gear = Palette.Hex("#57B86B"), GearAccent = Palette.Hex("#22202E"), Boots = Palette.Hex("#F7F4EE"), Height = 0.94f, SkiLength = 0.99f, LongHair = true,
            },
            new CharacterStyle // Erik: carving skis, race-team orange and navy
            {
                Jacket = Palette.Hex("#F2803A"), JacketTrim = Palette.Hex("#22305C"), Pants = Palette.Hex("#22305C"), Gloves = Palette.Hex("#22202E"),
                Helmet = Palette.Hex("#E8455A"), Goggles = Palette.Hex("#3A78E0"), Hair = Palette.Hex("#8A5A34"), Skin = Palette.Hex("#F1C6A7"),
                Gear = Palette.Hex("#3A78E0"), GearAccent = Palette.Hex("#FFC64A"), Boots = Palette.Hex("#E8455A"), Height = 1.05f, SkiLength = 1.72f,
            },
            new CharacterStyle // Linn: twin-tips, violet and mint
            {
                Jacket = Palette.Hex("#6C58C9"), JacketTrim = Palette.Hex("#8FE0C0"), Pants = Palette.Hex("#8FE0C0"), Gloves = Palette.Hex("#2C2A44"),
                Helmet = Palette.Hex("#8FE0C0"), Goggles = Palette.Hex("#D8437F"), Hair = Palette.Hex("#C0392B"), Skin = Palette.Hex("#EFC0A0"),
                Gear = Palette.Hex("#FFC64A"), GearAccent = Palette.Hex("#D8437F"), Boots = Palette.Hex("#2C2A44"), Height = 0.97f, SkiLength = 1.55f, LongHair = true,
            },
        };

        public static CharacterStyle Get(CharacterId id) => All[(int)id];
    }
}
