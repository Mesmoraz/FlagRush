using System.Linq;
using FlagRush.Domain.Aspects;
using FlagRush.Domain.Ids;
using FlagRush.Domain.Tests.Fakes;
using NUnit.Framework;

namespace FlagRush.Domain.Tests
{
    public class RegistryTests
    {
        [Test]
        public void EntitiesAreFoundByAspect_NotByConcreteType()
        {
            var entities = new InMemoryEntityRegistry();
            entities.Add(new TestAvatar(new EntityId(1), new TeamId(1)));
            entities.Add(new TestTeamObjective(new EntityId(2), new TeamId(1)));
            entities.Add(new TestWorldItem(new EntityId(3)));

            Assert.That(entities.OfAspect<IHasTeam>().Count(), Is.EqualTo(2));
            Assert.That(entities.OfAspect<ICarryable>().Count(), Is.EqualTo(2));
            Assert.That(entities.OfAspect<ICarrier>().Count(), Is.EqualTo(1));
            Assert.That(entities.OfAspect<IOwnable>().Single().Id, Is.EqualTo(new EntityId(3)));
        }

        [Test]
        public void AspectLookupFailsCleanlyWhenTheEntityLacksIt()
        {
            var entities = new InMemoryEntityRegistry();
            entities.Add(new TestWorldItem(new EntityId(3)));

            Assert.That(entities.TryGet<IHasHealth>(new EntityId(3), out var health), Is.False);
            Assert.That(health, Is.Null);
            Assert.That(entities.TryGet<IHasHealth>(new EntityId(99), out _), Is.False);
        }

        [Test]
        public void HealthAspectDerivesAliveness()
        {
            var avatar = new TestAvatar(new EntityId(1), new TeamId(1));
            IHasHealth health = avatar;
            Assert.That(health.IsAlive, Is.True);
            avatar.Health = 0f;
            Assert.That(health.IsAlive, Is.False);
        }

        [Test]
        public void NoneIdsAreRejected()
        {
            var entities = new InMemoryEntityRegistry();
            Assert.Throws<System.ArgumentException>(() => entities.Add(new TestWorldItem(EntityId.None)));
        }
    }
}
