using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Aspects
{
    /// <summary>
    /// Belongs to a team. Kept as an aspect (not only a relationship) because almost every rule needs
    /// a cheap "same team?" check; the authoritative membership record is still a relationship.
    /// </summary>
    public interface IHasTeam : IEntity
    {
        TeamId Team { get; }
    }
}
