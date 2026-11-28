namespace Ski.Core
{
    /// <summary>
    /// The touch steering box (three columns by three rows): the left and right columns turn, the top row
    /// tucks and the bottom row brakes, so the corners do both, and the middle does nothing. A zone is kept
    /// until the thumb is a little past its edge, so a thumb resting on a line does not flicker.
    /// </summary>
    public static class TouchZones
    {
        /// <summary>How far past a line (a share of the box) the thumb must go to change zone.</summary>
        public const float Stick = 0.04f;

        /// <summary>-1, 0 or 1 for a place across the box (0..1, beyond it counts as its edge), given the current zone.</summary>
        public static int Zone(float t, int current, float stick)
        {
            float a = 1f / 3f + (current == -1 ? stick : -stick);
            float b = 2f / 3f + (current == 1 ? -stick : stick);
            return t < a ? -1 : t > b ? 1 : 0;
        }

        /// <summary>What a column and a row (from the top: -1 top, 1 bottom) ask for.</summary>
        public static void Read(int column, int row, out float steer, out bool tuck, out bool brake)
        {
            steer = column; tuck = row == -1; brake = row == 1;
        }
    }
}
