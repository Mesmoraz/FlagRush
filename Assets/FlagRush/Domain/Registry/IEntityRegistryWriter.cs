using FlagRush.Domain.Aspects;
using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Registry
{
    /// <summary>
    /// Mutation side of the registry, separated so rule code can be handed a read-only view.
    /// </summary>
    public interface IEntityRegistryWriter : IEntityRegistry
    {
        /// <summary>Adds an entity; its <see cref="IEntity.Id"/> must be unique and not <see cref="EntityId.None"/>.</summary>
        void Add(IEntity entity);

        /// <summary>Removes the entity. Its relationships are the caller's job (<c>IRelationshipWriter.RemoveAll</c>).</summary>
        bool Remove(EntityId id);
    }
}
