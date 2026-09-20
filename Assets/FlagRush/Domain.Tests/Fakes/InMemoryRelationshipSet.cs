using System.Collections.Generic;
using System.Linq;
using FlagRush.Domain.Ids;
using FlagRush.Domain.Registry;

namespace FlagRush.Domain.Tests.Fakes
{
    public sealed class InMemoryRelationshipSet : IRelationshipWriter
    {
        readonly HashSet<Relationship> _edges = new HashSet<Relationship>();

        public int Count => _edges.Count;

        public bool Contains(in Relationship relationship) => _edges.Contains(relationship);

        public IEnumerable<Relationship> From(EntityId subject, RelationshipKind kind) =>
            _edges.Where(e => e.Subject == subject && e.Kind == kind).ToList();

        public IEnumerable<Relationship> To(EntityId target, RelationshipKind kind) =>
            _edges.Where(e => e.Object == target && e.Kind == kind).ToList();

        public IEnumerable<Relationship> Touching(EntityId entity) =>
            _edges.Where(e => e.Subject == entity || e.Object == entity).ToList();

        public bool Add(in Relationship relationship) => _edges.Add(relationship);

        public bool Remove(in Relationship relationship) => _edges.Remove(relationship);

        public int RemoveAll(EntityId entity) => _edges.RemoveWhere(e => e.Subject == entity || e.Object == entity);
    }
}
