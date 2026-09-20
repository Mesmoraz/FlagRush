using System;

namespace FlagRush.Domain.Ids
{
    /// <summary>
    /// Identifies a team. Team membership is a relationship, not a field on the player.
    /// Opaque, blittable, and comparable so it can later become an ECS component field unchanged.
    /// </summary>
    public readonly struct TeamId : IEquatable<TeamId>, IComparable<TeamId>
    {
        public readonly int Value;

        public TeamId(int value)
        {
            Value = value;
        }

        /// <summary>The "no TeamId" sentinel. Relationships to <see cref="None"/> are always invalid.</summary>
        public static TeamId None => default;

        public bool IsNone => Value == 0;

        public bool Equals(TeamId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TeamId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(TeamId other) => Value.CompareTo(other.Value);
        public override string ToString() => IsNone ? "TeamId.None" : $"TeamId({Value})";

        public static bool operator ==(TeamId a, TeamId b) => a.Value == b.Value;
        public static bool operator !=(TeamId a, TeamId b) => a.Value != b.Value;
    }
}
