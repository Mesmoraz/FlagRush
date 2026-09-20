using System.Collections.Generic;
using FlagRush.Domain.Aspects;
using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Registry
{
    /// <summary>
    /// Read access to every entity in a simulation, by id or by aspect. This is the only way rules find
    /// entities; they never hold references between ticks.
    /// </summary>
    public interface IEntityRegistry
    {
        int Count { get; }

        bool Contains(EntityId id);

        bool TryGet(EntityId id, out IEntity entity);

        /// <summary>Try to view an entity through one aspect. False if the entity is missing or lacks it.</summary>
        bool TryGet<TAspect>(EntityId id, out TAspect aspect) where TAspect : class, IEntity;

        /// <summary>Every entity that implements <typeparamref name="TAspect"/>.</summary>
        IEnumerable<TAspect> OfAspect<TAspect>() where TAspect : class, IEntity;
    }
}
