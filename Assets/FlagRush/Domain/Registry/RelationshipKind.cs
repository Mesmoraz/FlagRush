using System;

namespace FlagRush.Domain.Registry
{
    /// <summary>
    /// Names a kind of edge between two entities. Open-ended on purpose: a rule set can introduce its own
    /// kinds without touching the domain. Well-known kinds are provided for the concepts every mode shares.
    /// </summary>
    public readonly struct RelationshipKind : IEquatable<RelationshipKind>
    {
        public readonly string Name;

        public RelationshipKind(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Relationship kind needs a name.", nameof(name));
            Name = name;
        }

        /// <summary>Subject owns Object (player owns machine, vehicle, dropped item).</summary>
        public static readonly RelationshipKind Owns = new RelationshipKind("Owns");

        /// <summary>Subject physically carries Object (avatar carries flag, trunk carries crate).</summary>
        public static readonly RelationshipKind Carries = new RelationshipKind("Carries");

        /// <summary>Subject (an IContainer) holds Object in slot Ordinal.</summary>
        public static readonly RelationshipKind Contains = new RelationshipKind("Contains");

        /// <summary>Subject is a member of Object (avatar is a member of a team entity, player of a squad).</summary>
        public static readonly RelationshipKind MemberOf = new RelationshipKind("MemberOf");

        /// <summary>Subject controls Object (player controls avatar, driver controls vehicle).</summary>
        public static readonly RelationshipKind Controls = new RelationshipKind("Controls");

        public bool Equals(RelationshipKind other) => string.Equals(Name, other.Name, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is RelationshipKind other && Equals(other);
        public override int GetHashCode() => Name == null ? 0 : Name.GetHashCode();
        public override string ToString() => Name;

        public static bool operator ==(RelationshipKind a, RelationshipKind b) => a.Equals(b);
        public static bool operator !=(RelationshipKind a, RelationshipKind b) => !a.Equals(b);
    }
}
