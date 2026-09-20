using FlagRush.Domain.Aspects;
using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Profile
{
    /// <summary>
    /// Everything about a player that must survive a disconnect: who they are, what they have earned, what
    /// they hold. Keyed by <see cref="AuthId"/>, never by the transient <see cref="PlayerId"/>.
    /// </summary>
    public interface IPlayerProfile : IScorable
    {
        AuthId AuthId { get; }
        string DisplayName { get; }
        IInventorySnapshot Inventory { get; }
    }
}
