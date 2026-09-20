using System;

namespace FlagRush.Domain.Ids
{
    /// <summary>
    /// Identifies a kind of item (not an instance). Instances that exist in the world are entities; stacks inside a container are slot entries.
    /// Opaque, blittable, and comparable so it can later become an ECS component field unchanged.
    /// </summary>
    public readonly struct ItemKind : IEquatable<ItemKind>, IComparable<ItemKind>
    {
        public readonly int Value;

        public ItemKind(int value)
        {
            Value = value;
        }

        /// <summary>The "no ItemKind" sentinel. Relationships to <see cref="None"/> are always invalid.</summary>
        public static ItemKind None => default;

        public bool IsNone => Value == 0;

        public bool Equals(ItemKind other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ItemKind other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(ItemKind other) => Value.CompareTo(other.Value);
        public override string ToString() => IsNone ? "ItemKind.None" : $"ItemKind({Value})";

        public static bool operator ==(ItemKind a, ItemKind b) => a.Value == b.Value;
        public static bool operator !=(ItemKind a, ItemKind b) => a.Value != b.Value;
    }
}
