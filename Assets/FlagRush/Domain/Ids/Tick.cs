using System;

namespace FlagRush.Domain.Ids
{
    /// <summary>
    /// Discrete simulation time. Everything the simulation does happens "at" a tick, which is what makes
    /// server, client-prediction and tests replayable against the same inputs.
    /// </summary>
    public readonly struct Tick : IEquatable<Tick>, IComparable<Tick>
    {
        public readonly uint Value;

        public Tick(uint value)
        {
            Value = value;
        }

        public static Tick Zero => default;

        public Tick Next() => new Tick(Value + 1);
        public Tick Add(uint ticks) => new Tick(Value + ticks);

        public bool Equals(Tick other) => Value == other.Value;
        public override bool Equals(object obj) => obj is Tick other && Equals(other);
        public override int GetHashCode() => (int)Value;
        public int CompareTo(Tick other) => Value.CompareTo(other.Value);
        public override string ToString() => $"Tick({Value})";

        public static bool operator ==(Tick a, Tick b) => a.Value == b.Value;
        public static bool operator !=(Tick a, Tick b) => a.Value != b.Value;
        public static bool operator <(Tick a, Tick b) => a.Value < b.Value;
        public static bool operator >(Tick a, Tick b) => a.Value > b.Value;
        public static bool operator <=(Tick a, Tick b) => a.Value <= b.Value;
        public static bool operator >=(Tick a, Tick b) => a.Value >= b.Value;
    }
}
