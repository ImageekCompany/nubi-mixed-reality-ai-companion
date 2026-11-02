namespace Jolly.Character
{
    /// <summary>
    /// Which of three shell-count levels a furry renderer draws at a viewer distance: 0 near, 1 from
    /// <c>midFrom</c>, 2 from <c>farFrom</c>. A level changes only once the distance is <c>band</c> past a threshold, so
    /// a viewer standing on one does not swap the renderer's materials every frame.
    /// </summary>
    public static class ShellFurLod
    {
        public const int Levels = 3;

        /// <param name="current">The level drawn now, or -1 for none yet (no hysteresis).</param>
        public static int Select(float distance, int current, float midFrom, float farFrom, float band)
        {
            if (current < 0)
                return Level(distance, midFrom, farFrom);
            var outward = Level(distance - band, midFrom, farFrom);
            if (outward > current)
                return outward;
            var inward = Level(distance + band, midFrom, farFrom);
            return inward < current ? inward : current;
        }

        /// <summary>Height of shell <paramref name="index"/> (0 innermost) of <paramref name="count"/>, in (0, 1]: the
        /// outermost shell is the strand tips, the skin itself (0) is the base layer.</summary>
        public static float Height(int index, int count) => (index + 1f) / count;

        static int Level(float distance, float midFrom, float farFrom) =>
            distance >= farFrom ? 2 : distance >= midFrom ? 1 : 0;
    }
}
