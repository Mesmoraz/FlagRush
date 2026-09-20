using FlagRush.Domain.Aspects;
using FlagRush.Domain.Ids;
using FlagRush.Domain.Primitives;

namespace FlagRush.Domain.Tests.Fakes
{
    // Test-only entity shapes. They exist to prove aspects compose; they are not the game's types.

    /// <summary>Something that can carry, has a team, can die and has pockets.</summary>
    public sealed class TestAvatar : ICarrier, IHasTeam, IHasHealth, IContainer
    {
        public TestAvatar(EntityId id, TeamId team, float carryCapacity = 1f, int slots = 4)
        {
            Id = id;
            Team = team;
            CarryCapacity = carryCapacity;
            SlotCount = slots;
            Health = MaxHealth = 100f;
        }

        public EntityId Id { get; }
        public TeamId Team { get; }
        public Point3 Position { get; set; }
        public float CarryCapacity { get; }
        public float Health { get; set; }
        public float MaxHealth { get; }
        public int SlotCount { get; }
    }

    /// <summary>Something carryable that belongs to a team (a flag, but the test does not need to know).</summary>
    public sealed class TestTeamObjective : ICarryable, IHasTeam
    {
        public TestTeamObjective(EntityId id, TeamId team, float carryCost = 1f)
        {
            Id = id;
            Team = team;
            CarryCost = carryCost;
        }

        public EntityId Id { get; }
        public TeamId Team { get; }
        public Point3 Position { get; set; }
        public float CarryCost { get; }
    }

    /// <summary>Something carryable and ownable (a dropped item, a crate).</summary>
    public sealed class TestWorldItem : ICarryable, IOwnable
    {
        public TestWorldItem(EntityId id, float carryCost = 0.25f)
        {
            Id = id;
            CarryCost = carryCost;
        }

        public EntityId Id { get; }
        public Point3 Position { get; set; }
        public float CarryCost { get; }
    }
}
