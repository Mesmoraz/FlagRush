using System;

namespace FlagRush.Domain.Ids
{
    /// <summary>
    /// Identifies a live session participant (human or bot) for the duration of one connection. Not stable across reconnects — see AuthId.
    /// Opaque, blittable, and comparable so it can later become an ECS component field unchanged.
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>, IComparable<PlayerId>
    {
        public readonly int Value;

        public PlayerId(int value)
        {
            Value = value;
        }

        /// <summary>The "no PlayerId" sentinel. Relationships to <see cref="None"/> are always invalid.</summary>
        public static PlayerId None => default;

        public bool IsNone => Value == 0;

        public bool Equals(PlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(PlayerId other) => Value.CompareTo(other.Value);
        public override string ToString() => IsNone ? "PlayerId.None" : $"PlayerId({Value})";

        public static bool operator ==(PlayerId a, PlayerId b) => a.Value == b.Value;
        public static bool operator !=(PlayerId a, PlayerId b) => a.Value != b.Value;
    }
}
