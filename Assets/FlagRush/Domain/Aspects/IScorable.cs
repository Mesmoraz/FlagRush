namespace FlagRush.Domain.Aspects
{
    /// <summary>
    /// Accumulates progression. Implemented by live avatars (per match) and by persisted profiles
    /// (across matches) so the same rules can credit either.
    /// </summary>
    public interface IScorable
    {
        int Xp { get; }
        int Score { get; }
        long Money { get; }
    }
}
