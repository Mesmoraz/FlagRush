namespace FlagRush.Demo
{
    /// <summary>Team assignment. No [BurstCompile]; inlined into Burst systems.</summary>
    public static class MatchTeams
    {
        /// <summary>0 (Red) on a tie or when Red is smaller, else 1 (Blue).</summary>
        public static byte Pick(int redCount, int blueCount) => (byte)(redCount <= blueCount ? 0 : 1);
    }
}
