namespace FlagRush.Domain.Ports
{
    /// <summary>
    /// Seedable randomness. Rules must draw from this, never from a static RNG, so server and predicting
    /// client can agree.
    /// </summary>
    public interface IRandomSource
    {
        uint Seed { get; }

        /// <summary>Integer in [minInclusive, maxExclusive).</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>Float in [0, 1).</summary>
        float NextFloat();
    }
}
