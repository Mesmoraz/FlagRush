using System;
using System.Linq;
using FlagRush.Domain.Aspects;
using FlagRush.Domain.Ids;
using FlagRush.Domain.Registry;
using FlagRush.Domain.Tests.Fakes;
using NUnit.Framework;

namespace FlagRush.Domain.Tests
{
    public class RelationshipTests
    {
        static readonly EntityId AvatarId = new EntityId(1);
        static readonly EntityId ObjectiveId = new EntityId(2);
        static readonly EntityId ItemId = new EntityId(3);

        [Test]
        public void CarryingIsAnEdgeBetweenIds_NotAFieldOnEitherEntity()
        {
            var entities = new InMemoryEntityRegistry();
            var edges = new InMemoryRelationshipSet();
            entities.Add(new TestAvatar(AvatarId, new TeamId(1)));
            entities.Add(new TestTeamObjective(ObjectiveId, new TeamId(2)));

            Assert.That(edges.Add(new Relationship(RelationshipKind.Carries, AvatarId, ObjectiveId)), Is.True);

            var carried = edges.From(AvatarId, RelationshipKind.Carries).Single();
            Assert.That(carried.Object, Is.EqualTo(ObjectiveId));
            Assert.That(edges.To(ObjectiveId, RelationshipKind.Carries).Single().Subject, Is.EqualTo(AvatarId));

            // The registry can resolve the edge back to aspects without either type knowing about the other.
            Assert.That(entities.TryGet<ICarrier>(carried.Subject, out var carrier), Is.True);
            Assert.That(entities.TryGet<ICarryable>(carried.Object, out var carryable), Is.True);
            Assert.That(carryable.CarryCost, Is.LessThanOrEqualTo(carrier.CarryCapacity));
        }

        [Test]
        public void DuplicateEdgesAreRejected_AndRemoveIsExact()
        {
            var edges = new InMemoryRelationshipSet();
            var edge = new Relationship(RelationshipKind.Owns, AvatarId, ItemId);

            Assert.That(edges.Add(edge), Is.True);
            Assert.That(edges.Add(edge), Is.False);
            Assert.That(edges.Remove(new Relationship(RelationshipKind.Carries, AvatarId, ItemId)), Is.False, "different kind is a different edge");
            Assert.That(edges.Remove(edge), Is.True);
            Assert.That(edges.Count, Is.Zero);
        }

        [Test]
        public void ContainsUsesOrdinalAsSlot_SoOneContainerCanHoldManyThings()
        {
            var edges = new InMemoryRelationshipSet();
            edges.Add(new Relationship(RelationshipKind.Contains, AvatarId, ItemId, ordinal: 0));
            edges.Add(new Relationship(RelationshipKind.Contains, AvatarId, ObjectiveId, ordinal: 1));

            var slots = edges.From(AvatarId, RelationshipKind.Contains).OrderBy(e => e.Ordinal).ToList();
            Assert.That(slots.Select(e => e.Ordinal), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void RemovingAnEntityFromTheWorldDropsEveryEdgeTouchingIt()
        {
            var edges = new InMemoryRelationshipSet();
            edges.Add(new Relationship(RelationshipKind.Carries, AvatarId, ObjectiveId));
            edges.Add(new Relationship(RelationshipKind.Owns, AvatarId, ItemId));
            edges.Add(new Relationship(RelationshipKind.MemberOf, ItemId, ObjectiveId));

            Assert.That(edges.RemoveAll(AvatarId), Is.EqualTo(2));
            Assert.That(edges.Touching(AvatarId), Is.Empty);
            Assert.That(edges.Count, Is.EqualTo(1), "edges not touching the removed entity survive");
        }

        [Test]
        public void EdgesToNoneAreInvalid()
        {
            Assert.Throws<ArgumentException>(() => new Relationship(RelationshipKind.Owns, EntityId.None, ItemId));
            Assert.Throws<ArgumentException>(() => new Relationship(RelationshipKind.Owns, AvatarId, EntityId.None));
        }

        [Test]
        public void RuleSetsCanDefineTheirOwnKinds()
        {
            var fuels = new RelationshipKind("Fuels");
            Assert.That(fuels, Is.Not.EqualTo(RelationshipKind.Contains));
            Assert.That(new RelationshipKind("Fuels"), Is.EqualTo(fuels), "kinds are value-equal by name");
        }
    }
}
