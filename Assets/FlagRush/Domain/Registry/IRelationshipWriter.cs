using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Registry
{
    /// <summary>Mutation side of the relationship set.</summary>
    public interface IRelationshipWriter : IRelationshipReader
    {
        /// <summary>Adds the edge. Returns false if an identical edge already exists.</summary>
        bool Add(in Relationship relationship);

        /// <summary>Removes exactly this edge. Returns false if it was not present.</summary>
        bool Remove(in Relationship relationship);

        /// <summary>Removes every edge touching the entity. Used when an entity leaves the world.</summary>
        int RemoveAll(EntityId entity);
    }
}
