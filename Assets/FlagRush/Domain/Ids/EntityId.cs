using System;

namespace FlagRush.Domain.Ids
{
    /// <summary>
    /// Identifies any object that exists in the simulated world (avatar, flag, dropped item, vehicle, machine).
    /// Opaque, blittable, and comparable so it can later become an ECS component field unchanged.
    /// </summary>
    public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        public readonly int Value;

        public EntityId(int value)
        {
            Value = value;
        }

        /// <summary>The "no EntityId" sentinel. Relationships to <see cref="None"/> are always invalid.</summary>
        public static EntityId None => default;

        public bool IsNone => Value == 0;

        public bool Equals(EntityId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(EntityId other) => Value.CompareTo(other.Value);
        public override string ToString() => IsNone ? "EntityId.None" : $"EntityId({Value})";

        public static bool operator ==(EntityId a, EntityId b) => a.Value == b.Value;
        public static bool operator !=(EntityId a, EntityId b) => a.Value != b.Value;
    }
}
