using System;

namespace FlagRush.Domain.Ids
{
    /// <summary>
    /// Stable identity of a player across connections. This is the key that lets inventory, money and XP
    /// survive a disconnect: a reconnecting client presents the same <see cref="AuthId"/> and is bound to a
    /// fresh <see cref="PlayerId"/>.
    /// </summary>
    public readonly struct AuthId : IEquatable<AuthId>
    {
        public readonly Guid Value;

        public AuthId(Guid value)
        {
            Value = value;
        }

        public static AuthId None => default;
        public static AuthId NewRandom() => new AuthId(Guid.NewGuid());

        public bool IsNone => Value == Guid.Empty;

        public bool Equals(AuthId other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is AuthId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => IsNone ? "AuthId.None" : $"AuthId({Value:N})";

        public static bool operator ==(AuthId a, AuthId b) => a.Equals(b);
        public static bool operator !=(AuthId a, AuthId b) => !a.Equals(b);
    }
}
