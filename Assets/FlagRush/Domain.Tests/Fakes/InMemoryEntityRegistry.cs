using System.Collections.Generic;
using System.Linq;
using FlagRush.Domain.Aspects;
using FlagRush.Domain.Ids;
using FlagRush.Domain.Registry;

namespace FlagRush.Domain.Tests.Fakes
{
    public sealed class InMemoryEntityRegistry : IEntityRegistryWriter
    {
        readonly Dictionary<EntityId, IEntity> _entities = new Dictionary<EntityId, IEntity>();

        public int Count => _entities.Count;

        public bool Contains(EntityId id) => _entities.ContainsKey(id);

        public bool TryGet(EntityId id, out IEntity entity) => _entities.TryGetValue(id, out entity);

        public bool TryGet<TAspect>(EntityId id, out TAspect aspect) where TAspect : class, IEntity
        {
            aspect = _entities.TryGetValue(id, out var entity) ? entity as TAspect : null;
            return aspect != null;
        }

        public IEnumerable<TAspect> OfAspect<TAspect>() where TAspect : class, IEntity =>
            _entities.Values.OfType<TAspect>();

        public void Add(IEntity entity)
        {
            if (entity.Id.IsNone) throw new System.ArgumentException("Entity id cannot be None.");
            _entities.Add(entity.Id, entity);
        }

        public bool Remove(EntityId id) => _entities.Remove(id);
    }
}
