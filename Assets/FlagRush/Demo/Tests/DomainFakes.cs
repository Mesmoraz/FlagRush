using System;
using System.Collections.Generic;
using System.Linq;
using FlagRush.Domain.Aspects;
using FlagRush.Domain.Ids;
using FlagRush.Domain.Ports;
using FlagRush.Domain.Primitives;
using FlagRush.Domain.Registry;
using FlagRush.Domain.Simulation;

namespace FlagRush.Demo.Tests
{
    /// <summary>Test-only aspect: an entity whose position the rule set may move (Domain aspects are read-only).</summary>
    public interface IMovable : IPositioned
    {
        void MoveTo(Point3 position);
    }

    // Minimal fakes (FlagRush.Domain.Tests is Editor-only, so its fakes cannot be referenced here).
    // Members the tests do not touch throw NotSupportedException.

    public sealed class FakeRuleContext : IRuleContext
    {
        public Tick Tick { get; set; }
        public FakeEntities EntitiesFake { get; } = new FakeEntities();
        public FakeRelationships RelationshipsFake { get; } = new FakeRelationships();
        public List<IGameEvent> Emitted { get; } = new List<IGameEvent>();

        public IEntityRegistryWriter Entities => EntitiesFake;
        public IRelationshipWriter Relationships => RelationshipsFake;
        public IRandomSource Random => throw new NotSupportedException();
        public ILogSink Log => throw new NotSupportedException();

        public void Emit(IGameEvent gameEvent) => Emitted.Add(gameEvent);
    }

    public sealed class FakeEntities : IEntityRegistryWriter
    {
        readonly Dictionary<int, IEntity> _byId = new Dictionary<int, IEntity>();

        public int Count => _byId.Count;
        public bool Contains(EntityId id) => _byId.ContainsKey(id.Value);

        public bool TryGet(EntityId id, out IEntity entity) => _byId.TryGetValue(id.Value, out entity);

        public bool TryGet<TAspect>(EntityId id, out TAspect aspect) where TAspect : class, IEntity
        {
            aspect = _byId.TryGetValue(id.Value, out var e) ? e as TAspect : null;
            return aspect != null;
        }

        public IEnumerable<TAspect> OfAspect<TAspect>() where TAspect : class, IEntity =>
            _byId.Values.OfType<TAspect>();

        public void Add(IEntity entity)
        {
            if (entity.Id.IsNone || _byId.ContainsKey(entity.Id.Value))
                throw new ArgumentException("Entity id must be unique and not None.");
            _byId.Add(entity.Id.Value, entity);
        }

        public bool Remove(EntityId id) => _byId.Remove(id.Value);
    }

    public sealed class FakeRelationships : IRelationshipWriter
    {
        readonly List<Relationship> _edges = new List<Relationship>();

        public int Count => _edges.Count;
        public bool Contains(in Relationship relationship) => _edges.Contains(relationship);

        public IEnumerable<Relationship> From(EntityId subject, RelationshipKind kind) =>
            _edges.Where(r => r.Subject == subject && r.Kind == kind).ToList();

        public IEnumerable<Relationship> To(EntityId target, RelationshipKind kind) =>
            _edges.Where(r => r.Object == target && r.Kind == kind).ToList();

        public IEnumerable<Relationship> Touching(EntityId entity) =>
            _edges.Where(r => r.Subject == entity || r.Object == entity).ToList();

        public bool Add(in Relationship relationship)
        {
            if (_edges.Contains(relationship)) return false;
            _edges.Add(relationship);
            return true;
        }

        public bool Remove(in Relationship relationship) => _edges.Remove(relationship);

        public int RemoveAll(EntityId entity) => _edges.RemoveAll(r => r.Subject == entity || r.Object == entity);
    }

    public sealed class FakeAvatar : ICarrier, IHasTeam, IMovable
    {
        public FakeAvatar(int id, TeamId team, Point3 position)
        {
            Id = new EntityId(id);
            Team = team;
            Position = position;
        }

        public EntityId Id { get; }
        public TeamId Team { get; }
        public Point3 Position { get; private set; }
        public float CarryCapacity => 1f;
        public void MoveTo(Point3 position) => Position = position;
    }

    public sealed class FakeFlag : ICarryable, IHasTeam, IMovable
    {
        public FakeFlag(int id, TeamId team, Point3 position)
        {
            Id = new EntityId(id);
            Team = team;
            Position = position;
        }

        public EntityId Id { get; }
        public TeamId Team { get; }
        public Point3 Position { get; private set; }
        public float CarryCost => 1f;
        public void MoveTo(Point3 position) => Position = position;
    }
}
