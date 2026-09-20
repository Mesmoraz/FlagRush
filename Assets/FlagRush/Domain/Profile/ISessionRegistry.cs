using System.Collections.Generic;
using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Profile
{
    /// <summary>
    /// Live sessions on the authoritative side. This is the piece that makes reconnection a data operation
    /// instead of a special case in gameplay code.
    /// </summary>
    public interface ISessionRegistry
    {
        IReadOnlyCollection<ISessionBinding> All { get; }

        bool TryGetByAuth(AuthId authId, out ISessionBinding binding);
        bool TryGetByPlayer(PlayerId playerId, out ISessionBinding binding);

        /// <summary>
        /// Binds (or re-binds) an AuthId to a PlayerId at a tick. If the AuthId already has a binding, it is
        /// resumed: the avatar is kept and the state becomes Connected.
        /// </summary>
        ISessionBinding Connect(AuthId authId, PlayerId playerId, Tick tick);

        /// <summary>Marks the session disconnected but keeps it so it can be resumed.</summary>
        bool Disconnect(PlayerId playerId, Tick tick);

        /// <summary>Attaches the avatar the participant controls.</summary>
        bool AssignAvatar(PlayerId playerId, EntityId avatar);

        /// <summary>Drops a binding for good (timeout, ban). The profile itself lives on in the repository.</summary>
        bool Forget(AuthId authId);
    }
}
