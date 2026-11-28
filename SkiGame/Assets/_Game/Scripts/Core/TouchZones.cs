namespace Ski.Core
{
    /// <summary>
    /// The touch steering stick: two halves. The knob left of the middle turns left, right of it turns
    /// right; only letting go of it goes straight. The side is kept until the thumb is a little past the
    /// middle, so a thumb resting on the line does not flicker between the two.
    /// </summary>
    public static class TouchZones
    {
        /// <summary>How far past the middle (a share of the knob's reach) the thumb must go to change side.</summary>
        public const float SideStick = 0.08f;
        /// <summary>Walking: nothing for a twitch of the thumb.</summary>
        public const float WalkDead = 0.12f;

        /// <summary>
        /// -1 (left) or 1 (right) for a knob at <paramref name="x"/> across the stick (0 the middle, 1 full reach
        /// to the right). <paramref name="sticky"/> keeps <paramref name="current"/> (-1, 1, or 0 for none yet)
        /// near the middle.
        /// </summary>
        public static int Side(float x, int current, bool sticky)
        {
            if (sticky && current < 0) return x > SideStick ? 1 : -1;
            if (sticky && current > 0) return x < -SideStick ? -1 : 1;
            return x < 0f ? -1 : 1;
        }
    }
}
