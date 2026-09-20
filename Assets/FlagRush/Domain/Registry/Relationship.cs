using System;
using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Registry
{
    /// <summary>
    /// A directed edge: Subject --Kind--> Object, with an optional ordinal (slot index, seat number).
    /// This is the whole "who holds what" model. Because it is three ids and an int it can be a buffer
    /// element on an ECS entity or a row in a save file without translation.
    /// </summary>
    public readonly struct Relationship : IEquatable<Relationship>
    {
        public readonly RelationshipKind Kind;
        public readonly EntityId Subject;
        public readonly EntityId Object;
        public readonly int Ordinal;

        public Relationship(RelationshipKind kind, EntityId subject, EntityId target, int ordinal = 0)
        {
            if (subject.IsNone) throw new ArgumentException("Relationship subject cannot be None.", nameof(subject));
            if (target.IsNone) throw new ArgumentException("Relationship object cannot be None.", nameof(target));
            Kind = kind;
            Subject = subject;
            Object = target;
            Ordinal = ordinal;
        }

        public bool Equals(Relationship other) =>
            Kind == other.Kind && Subject == other.Subject && Object == other.Object && Ordinal == other.Ordinal;

        public override bool Equals(object obj) => obj is Relationship other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Kind, Subject, Object, Ordinal);
        public override string ToString() => $"{Subject} -{Kind}[{Ordinal}]-> {Object}";

        public static bool operator ==(Relationship a, Relationship b) => a.Equals(b);
        public static bool operator !=(Relationship a, Relationship b) => !a.Equals(b);
    }
}
