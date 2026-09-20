using System;
using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Profile
{
    /// <summary>
    /// One stack in a persisted inventory: which slot, what kind, how many. Item *instances* that exist
    /// in the world are entities; this is the durable, off-world form of the same thing.
    /// </summary>
    public readonly struct SlotEntry : IEquatable<SlotEntry>
    {
        public readonly int Slot;
        public readonly ItemKind Kind;
        public readonly int Quantity;

        public SlotEntry(int slot, ItemKind kind, int quantity)
        {
            if (slot < 0) throw new ArgumentOutOfRangeException(nameof(slot));
            if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            Slot = slot;
            Kind = kind;
            Quantity = quantity;
        }

        public bool Equals(SlotEntry other) => Slot == other.Slot && Kind == other.Kind && Quantity == other.Quantity;
        public override bool Equals(object obj) => obj is SlotEntry other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Slot, Kind, Quantity);
        public override string ToString() => $"[{Slot}] {Kind} x{Quantity}";
    }
}
