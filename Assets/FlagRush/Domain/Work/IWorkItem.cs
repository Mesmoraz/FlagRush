using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Work
{
    /// <summary>
    /// A unit of background work that progresses over ticks: a production machine crafting, a bot brain
    /// re-planning, a spatial index rebuilding. The contract says nothing about threads; a scheduler may run
    /// items inline, on a job system, or on a server thread pool.
    /// </summary>
    public interface IWorkItem
    {
        WorkId Id { get; }
        WorkPriority Priority { get; }
        bool IsComplete { get; }

        /// <summary>Advance one tick. Must be safe to call again after <see cref="IsComplete"/> becomes true.</summary>
        void Advance(Tick tick);
    }
}
