using System.Collections.Generic;
using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Work
{
    /// <summary>
    /// Owns a set of <see cref="IWorkItem"/>s and advances them each tick. Completed items are dropped
    /// after the tick in which they complete.
    /// </summary>
    public interface IWorkScheduler
    {
        int PendingCount { get; }

        WorkId Schedule(IWorkItem item);

        bool Cancel(WorkId id);

        bool TryGet(WorkId id, out IWorkItem item);

        /// <summary>Advance every pending item for this tick and return the ones that completed.</summary>
        IReadOnlyList<IWorkItem> Advance(Tick tick);
    }
}
