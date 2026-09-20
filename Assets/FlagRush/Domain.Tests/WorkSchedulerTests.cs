using System.Linq;
using FlagRush.Domain.Ids;
using FlagRush.Domain.Tests.Fakes;
using FlagRush.Domain.Work;
using NUnit.Framework;

namespace FlagRush.Domain.Tests
{
    public class WorkSchedulerTests
    {
        [Test]
        public void WorkAdvancesEachTickAndIsDroppedWhenComplete()
        {
            var scheduler = new InMemoryWorkScheduler();
            var craft = new CountdownWork(new WorkId(1), ticks: 3);
            scheduler.Schedule(craft);

            Assert.That(scheduler.Advance(new Tick(1)), Is.Empty);
            Assert.That(scheduler.Advance(new Tick(2)), Is.Empty);
            var completed = scheduler.Advance(new Tick(3));

            Assert.That(completed.Single().Id, Is.EqualTo(new WorkId(1)));
            Assert.That(craft.IsComplete, Is.True);
            Assert.That(scheduler.PendingCount, Is.Zero);
        }

        [Test]
        public void CompletedWorkIgnoresFurtherAdvances()
        {
            var craft = new CountdownWork(new WorkId(1), ticks: 1);
            craft.Advance(new Tick(1));
            craft.Advance(new Tick(2));
            Assert.That(craft.AdvanceCalls, Is.EqualTo(1));
        }

        [Test]
        public void HigherPriorityWorkAdvancesFirst()
        {
            var scheduler = new InMemoryWorkScheduler();
            var low = new CountdownWork(new WorkId(1), ticks: 1, WorkPriority.Low);
            var high = new CountdownWork(new WorkId(2), ticks: 1, WorkPriority.High);
            scheduler.Schedule(low);
            scheduler.Schedule(high);

            var completed = scheduler.Advance(new Tick(1));
            Assert.That(completed.Select(w => w.Id.Value), Is.EqualTo(new[] { 2, 1 }));
        }

        [Test]
        public void CancelledWorkNeverRuns()
        {
            var scheduler = new InMemoryWorkScheduler();
            var craft = new CountdownWork(new WorkId(1), ticks: 1);
            scheduler.Schedule(craft);

            Assert.That(scheduler.Cancel(new WorkId(1)), Is.True);
            scheduler.Advance(new Tick(1));
            Assert.That(craft.AdvanceCalls, Is.Zero);
            Assert.That(scheduler.TryGet(new WorkId(1), out _), Is.False);
        }
    }
}
