using System.Collections.Generic;
using FlagRush.Domain.Aspects;

namespace FlagRush.Domain.Profile
{
    /// <summary>The durable form of a container's contents. Read-only; a rule set produces a new one to change it.</summary>
    public interface IInventorySnapshot : IContainer
    {
        IReadOnlyList<SlotEntry> Entries { get; }
    }
}
