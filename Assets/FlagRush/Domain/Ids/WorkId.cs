using System;

namespace FlagRush.Domain.Ids
{
    /// <summary>
    /// Identifies a scheduled unit of background work.
    /// Opaque, blittable, and comparable so it can later become an ECS component field unchanged.
    /// </summary>
    public readonly struct WorkId : IEquatable<WorkId>, IComparable<WorkId>
    {
        public readonly int Value;

        public WorkId(int value)
        {
            Value = value;
        }

        /// <summary>The "no WorkId" sentinel. Relationships to <see cref="None"/> are always invalid.</summary>
        public static WorkId None => default;

        public bool IsNone => Value == 0;

        public bool Equals(WorkId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is WorkId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(WorkId other) => Value.CompareTo(other.Value);
        public override string ToString() => IsNone ? "WorkId.None" : $"WorkId({Value})";

        public static bool operator ==(WorkId a, WorkId b) => a.Value == b.Value;
        public static bool operator !=(WorkId a, WorkId b) => a.Value != b.Value;
    }
}
