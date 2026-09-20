using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Work
{
    /// <summary>
    /// Template base for <see cref="IWorkItem"/>: subclasses implement <see cref="OnAdvance"/> and call
    /// <see cref="Complete"/> when done. Keeps the "done" bookkeeping in one place.
    /// </summary>
    public abstract class WorkItem : IWorkItem
    {
        protected WorkItem(WorkId id, WorkPriority priority = WorkPriority.Normal)
        {
            Id = id;
            Priority = priority;
        }

        public WorkId Id { get; }
        public WorkPriority Priority { get; }
        public bool IsComplete { get; private set; }

        public void Advance(Tick tick)
        {
            if (IsComplete) return;
            OnAdvance(tick);
        }

        protected abstract void OnAdvance(Tick tick);

        protected void Complete()
        {
            IsComplete = true;
        }
    }
}
