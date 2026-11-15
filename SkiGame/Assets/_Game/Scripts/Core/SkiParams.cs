namespace Ski.Core
{
    /// <summary>The four friends. Order matches the seats on the chair lift, left to right.</summary>
    public enum CharacterId
    {
        Jonte = 0, // boy, snowboard
        Maja = 1,  // girl, skiblades
        Erik = 2,  // boy, carving skis
        Linn = 3,  // girl, twin-tips
    }

    public enum Gear { Snowboard, Blades, Carvers, TwinTips }

    public enum Trait { TuckSpeed, TightTurns, Carver, PoleRhythm }

    /// <summary>Static facts about a playable character.</summary>
    public sealed class SkierInfo
    {
        public CharacterId Id;
        public string Name;
        public Gear Gear;
        public Trait Trait;
        public string TraitTitle;
        public string TraitText;
        public bool IsGirl;

        public static readonly SkierInfo[] All =
        {
            new SkierInfo { Id = CharacterId.Jonte, Name = "Jonte", Gear = Gear.Snowboard, Trait = Trait.TuckSpeed,
                TraitTitle = "Bomber", TraitText = "Slightly higher top speed in a tuck." },
            new SkierInfo { Id = CharacterId.Maja, Name = "Maja", Gear = Gear.Blades, Trait = Trait.TightTurns, IsGirl = true,
                TraitTitle = "Quick feet", TraitText = "Tighter turns that lose less speed." },
            new SkierInfo { Id = CharacterId.Erik, Name = "Erik", Gear = Gear.Carvers, Trait = Trait.Carver,
                TraitTitle = "Edge master", TraitText = "Gains more speed from a clean carve." },
            new SkierInfo { Id = CharacterId.Linn, Name = "Linn", Gear = Gear.TwinTips, Trait = Trait.PoleRhythm, IsGirl = true,
                TraitTitle = "Fast starter", TraitText = "Easier push timing and stronger pushes." },
        };

        public static SkierInfo Get(CharacterId id) => All[(int)id];
    }

    /// <summary>
    /// Tuning for the ski simulation. One instance per run; traits are applied on top of the
    /// defaults by <see cref="For"/>. All units are SI (metres, seconds, radians).
    /// </summary>
    public sealed class SkiParams
    {
        public float Gravity = 9.81f;

        // Resistance. Terminal speed on a slope is sqrt(g (sin a - mu cos a) / drag).
        public float Mu = 0.035f;          // snow friction coefficient
        public float DragUpright = 0.0052f; // air drag factor standing
        public float DragTuck = 0.0029f;    // air drag factor in a full tuck
        public float TuckIn = 3.5f, TuckOut = 4.5f; // tuck ramp rates (1/s)

        // Turning. Lean ramps toward the steer key; yaw rate is lean * max rate.
        public float LeanIn = 1.45f;        // 1/s while the key is held
        public float LeanOut = 3.6f;        // 1/s when released
        public float TurnRateLow = 1.9f;    // rad/s cap at low speed
        public float LatAccelMax = 13.5f;   // m/s^2, limits yaw rate at speed
        public float TuckTurnFactor = 0.7f; // turn-rate multiplier in a full tuck
        public float Grip = 11f;            // 1/s, how fast sideways slip is killed
        public float GripPlough = 5f;

        // Carving: lean inside [CarveMin, CarveMax] gains speed, above CarveMax skids and brakes.
        public float CarveMin = 0.18f, CarveMax = 0.72f;
        public float CarveBoost = 1.15f;    // m/s^2 at the middle of the band
        public float SkidDecel = 5.2f;      // m/s^2 at full lean
        public float SkidLossFactor = 1f;   // scales energy lost to sideways slip

        // Snowplough.
        public float PloughIn = 4f, PloughOut = 6f;
        public float PloughDecel = 6.2f;

        // Pole pushes (or skate pushes on the board).
        public float PoleMaxSpeed = 9f;     // no pushing above this speed
        public float PolePeriodSlow = 1.05f, PolePeriodFast = 0.5f;
        public float PoleSweetSpot = 0.75f; // phase of the perfect moment
        public float PoleWindow = 0.11f;    // half-width of the perfect window (in phase)
        public float PoleOkWindow = 0.24f;
        public float PolePerfect = 1.55f, PoleOk = 0.85f, PoleMiss = 0.22f; // m/s per push

        // Jumping and air.
        public float JumpSpeed = 3.7f;
        public float AirSpinRate = 7.2f;    // rad/s
        public float AirSpinAccel = 30f;    // rad/s^2
        public float FlipRate = 6.9f;       // rad/s
        public float CorkRate = 5.6f;
        public float AirDrag = 0.0025f;

        // Landings and crashes.
        public float HeavyImpact = 7.5f;    // m/s into the snow: lose speed
        public float CrashImpact = 11f;   // m/s into the snow: crash
        public float CrashTime = 1.7f;
        public float LandYawTolerance = 0.95f; // rad
        public float LandFlipTolerance = 1.0f; // rad

        public float BodyRadius = 0.35f;
        public float EyeHeight = 1.62f;

        public static SkiParams For(CharacterId id)
        {
            var p = new SkiParams();
            switch (SkierInfo.Get(id).Trait)
            {
                case Trait.TuckSpeed:
                    p.DragTuck *= 0.915f; // about +4.5 % tucked top speed
                    break;
                case Trait.TightTurns:
                    p.LatAccelMax *= 1.09f;
                    p.TurnRateLow *= 1.08f;
                    p.SkidDecel *= 0.82f;
                    p.SkidLossFactor = 0.8f;
                    p.CarveMax = 0.75f;
                    break;
                case Trait.Carver:
                    p.CarveBoost *= 1.3f;
                    break;
                case Trait.PoleRhythm:
                    p.PoleWindow *= 1.45f;
                    p.PoleOkWindow *= 1.2f;
                    p.PolePerfect *= 1.06f;
                    p.PoleMaxSpeed += 0.5f;
                    break;
            }
            return p;
        }
    }
}
