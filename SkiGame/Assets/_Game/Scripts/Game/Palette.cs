using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// The art bible's palette. Every colour in the game comes from here.
    /// Style: ink-and-wash graphic-novel alpine dusk; hard cel bands, dark-plum ink lines,
    /// lamp-warm accents against cold blue snow.
    /// </summary>
    public static class Palette
    {
        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        // Snow and sky.
        public static readonly Color Snow = Hex("#EEF3FA");
        public static readonly Color SnowOffPiste = Hex("#E3EAF6");
        public static readonly Color SnowShadow = Hex("#8C9CCB");   // multiplied into shadowed areas
        public static readonly Color SkyTop = Hex("#2B2F5E");
        public static readonly Color SkyMid = Hex("#8C74A3");
        public static readonly Color SkyHorizon = Hex("#F29F7A");
        public static readonly Color RidgeFar = Hex("#7480B8");
        public static readonly Color RidgeNear = Hex("#474D80");
        public static readonly Color Storm = Hex("#B9C0D2");
        public static readonly Color Fog = Hex("#C9B7C9");

        // Light.
        public static readonly Color Sun = Hex("#FFF0DC");
        public static readonly Color AmbientSky = Hex("#8E9AD0");
        public static readonly Color AmbientEquator = Hex("#B9A9C4");
        public static readonly Color AmbientGround = Hex("#6E5A68");
        public static readonly Color Lamp = Hex("#FFC64A");          // accent
        public static readonly Color Window = Hex("#FFD67A");

        // Ink.
        public static readonly Color Ink = Hex("#1B1430");
        public static readonly Color InkSoft = Hex("#3A3157");

        // Nature.
        public static readonly Color Pine = Hex("#1F4A4A");
        public static readonly Color PineLight = Hex("#2E6B5E");
        public static readonly Color PineDark = Hex("#163838");
        public static readonly Color Bark = Hex("#5A3B33");
        public static readonly Color DeadWood = Hex("#8A7A78");
        public static readonly Color Rock = Hex("#6E6A85");
        public static readonly Color RockDark = Hex("#4D4A66");
        public static readonly Color Bush = Hex("#7A5A4A");

        // Built things.
        public static readonly Color Timber = Hex("#7A4A32");
        public static readonly Color TimberDark = Hex("#55301F");
        public static readonly Color TimberLight = Hex("#A26A45");
        public static readonly Color Roof = Hex("#3B3552");
        public static readonly Color Steel = Hex("#5C6480");
        public static readonly Color SteelDark = Hex("#3B4160");
        public static readonly Color SignalRed = Hex("#E8455A");
        public static readonly Color SignalBlue = Hex("#3A78E0");
        public static readonly Color SignalYellow = Hex("#FFC64A");
        public static readonly Color SafetyOrange = Hex("#F2803A");
        public static readonly Color PisteGreen = Hex("#3FAE6A");
        public static readonly Color PisteBlack = Hex("#22202E");
        public static readonly Color White = Hex("#F7F4EE");
        public static readonly Color Black = Hex("#1B1430");

        // People.
        public static readonly Color Skin = Hex("#F1C6A7");
        public static readonly Color SkinDark = Hex("#C98F6B");
        public static readonly Color[] Jackets =
        {
            Hex("#18A6A6"), Hex("#D8437F"), Hex("#E8A93B"), Hex("#6C58C9"),
            Hex("#E8455A"), Hex("#3A78E0"), Hex("#57B86B"), Hex("#F2803A"),
        };
        public static readonly Color[] Pants = { Hex("#2C2A44"), Hex("#3B4160"), Hex("#4A3550"), Hex("#22202E") };

        // UI.
        public static readonly Color UiPaper = Hex("#F5EBD8");
        public static readonly Color UiInk = Hex("#1B1430");
        public static readonly Color UiAccent = Hex("#FFC64A");
        public static readonly Color UiGood = Hex("#57D18A");
        public static readonly Color UiBad = Hex("#E8455A");

        public static Color Slope(Ski.Core.SlopeId id)
        {
            switch (id)
            {
                case Ski.Core.SlopeId.Green: return PisteGreen;
                case Ski.Core.SlopeId.Blue: return SignalBlue;
                case Ski.Core.SlopeId.Red: return SignalRed;
                case Ski.Core.SlopeId.Black: return PisteBlack;
                default: return SafetyOrange;
            }
        }

        public static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
        public static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, t);
    }
}
