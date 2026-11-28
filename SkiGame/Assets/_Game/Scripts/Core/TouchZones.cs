using UnityEngine;

namespace Ski.Core
{
    /// <summary>
    /// The touch steering stick: the knob dragged out from the middle of a ring reads one of eight ways,
    /// each the same distance out and the same width (as the keys around S: sideways turns, up tucks, down
    /// brakes, the diagonals do both); near the middle it reads nothing. A way is kept until the thumb is
    /// a little past its edge, so a thumb resting on a line does not flicker between two.
    /// </summary>
    public static class TouchZones
    {
        /// <summary>No way at all this near the middle (a share of the knob's reach).</summary>
        public const float Dead = 0.35f;
        /// <summary>How much further in or out (a share of the reach) the thumb must go to cross the middle's edge.</summary>
        public const float DeadStick = 0.06f;
        /// <summary>How many degrees past the line between two ways the thumb must go to change way.</summary>
        public const float AngleStick = 6f;
        /// <summary>Walking: nothing for a twitch of the thumb.</summary>
        public const float WalkDead = 0.12f;

        /// <summary>
        /// The way for a knob at <paramref name="d"/> (x right, y up, 1 = full reach): -1 for the middle,
        /// else 0 right, then anticlockwise: 1 up-right, 2 up, 3 up-left, 4 left, 5 down-left, 6 down,
        /// 7 down-right. <paramref name="sticky"/> keeps <paramref name="current"/> near its edges.
        /// </summary>
        public static int Stick(Vector2 d, int current, bool sticky)
        {
            float r = d.magnitude;
            float dead = Dead + (sticky ? (current < 0 ? DeadStick : -DeadStick) : 0f);
            if (r < dead) return -1;
            float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            int z = ((Mathf.RoundToInt(a / 45f) % 8) + 8) % 8;
            if (sticky && current >= 0 && z != current && Mathf.Abs(Mathf.DeltaAngle(current * 45f, a)) < 22.5f + AngleStick) z = current;
            return z;
        }

        /// <summary>What a way asks for.</summary>
        public static void Read(int way, out float steer, out bool tuck, out bool brake)
        {
            steer = 0f; tuck = brake = false;
            if (way < 0) return;
            steer = way == 0 || way == 1 || way == 7 ? 1f : way >= 3 && way <= 5 ? -1f : 0f;
            tuck = way >= 1 && way <= 3;
            brake = way >= 5 && way <= 7;
        }
    }
}
