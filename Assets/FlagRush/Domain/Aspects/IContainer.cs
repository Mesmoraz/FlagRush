namespace FlagRush.Domain.Aspects
{
    /// <summary>
    /// Has inventory slots (an avatar's backpack, a vehicle trunk, a machine's output hopper). Contents are
    /// <c>Contains</c> relationships whose ordinal is the slot index; the container only declares capacity.
    /// </summary>
    public interface IContainer
    {
        int SlotCount { get; }
    }
}
