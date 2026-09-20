namespace FlagRush.Domain.Config
{
    /// <summary>
    /// Read-only match parameters. Deliberately tiny: anything mode-specific belongs to that rule set's own
    /// config type, which can extend this.
    /// </summary>
    public interface IGameConfig
    {
        int TeamCount { get; }
        int TeamSize { get; }
        int TickRate { get; }
        int MatchLengthTicks { get; }

        int MaxPlayers => TeamCount * TeamSize;
    }
}
