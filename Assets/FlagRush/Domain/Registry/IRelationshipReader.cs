using System.Collections.Generic;
using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Registry
{
    /// <summary>Query side of the relationship set.</summary>
    public interface IRelationshipReader
    {
        int Count { get; }

        bool Contains(in Relationship relationship);

        /// <summary>Edges of <paramref name="kind"/> going out of <paramref name="subject"/>.</summary>
        IEnumerable<Relationship> From(EntityId subject, RelationshipKind kind);

        /// <summary>Edges of <paramref name="kind"/> coming into <paramref name="target"/>.</summary>
        IEnumerable<Relationship> To(EntityId target, RelationshipKind kind);

        /// <summary>Every edge touching the entity in either direction, any kind.</summary>
        IEnumerable<Relationship> Touching(EntityId entity);
    }
}
