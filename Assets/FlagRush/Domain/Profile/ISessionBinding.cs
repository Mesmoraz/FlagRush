using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Profile
{
    /// <summary>
    /// Ties a durable identity to a live participant and (optionally) the world entity they control.
    /// Disconnect = flip the state; reconnect = same AuthId gets a new PlayerId and the old avatar back.
    /// </summary>
    public interface ISessionBinding
    {
        AuthId AuthId { get; }
        PlayerId PlayerId { get; }

        /// <summary>The entity this participant controls, or <see cref="EntityId.None"/> while not spawned.</summary>
        EntityId Avatar { get; }

        SessionState State { get; }
        Tick LastSeen { get; }
    }
}
