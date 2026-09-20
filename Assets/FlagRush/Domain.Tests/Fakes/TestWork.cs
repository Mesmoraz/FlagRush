using System.Collections.Generic;
using System.Linq;
using FlagRush.Domain.Ids;
using FlagRush.Domain.Work;

namespace FlagRush.Domain.Tests.Fakes
{
    /// <summary>Completes after N advances. Stands in for "a machine that takes N ticks to craft one item".</summary>
    public sealed class CountdownWork : WorkItem
    {
        int _remaining;

        public CountdownWork(WorkId id, int ticks, WorkPriority priority = WorkPriority.Normal) : base(id, priority)
        {
            _remaining = ticks;
        }

        public int AdvanceCalls { get; private set; }

        protected override void OnAdvance(Tick tick)
        {
            AdvanceCalls++;
            if (--_remaining <= 0) Complete();
        }
    }

    public sealed class InMemoryWorkScheduler : IWorkScheduler
    {
        readonly Dictionary<WorkId, IWorkItem> _pending = new Dictionary<WorkId, IWorkItem>();

        public int PendingCount => _pending.Count;

        public WorkId Schedule(IWorkItem item)
        {
            _pending.Add(item.Id, item);
            return item.Id;
        }

        public bool Cancel(WorkId id) => _pending.Remove(id);

        public bool TryGet(WorkId id, out IWorkItem item) => _pending.TryGetValue(id, out item);

        public IReadOnlyList<IWorkItem> Advance(Tick tick)
        {
            var completed = new List<IWorkItem>();
            foreach (var item in _pending.Values.OrderByDescending(i => i.Priority).ToList())
            {
                item.Advance(tick);
                if (item.IsComplete) completed.Add(item);
            }
            foreach (var item in completed) _pending.Remove(item.Id);
            return completed;
        }
    }
}
