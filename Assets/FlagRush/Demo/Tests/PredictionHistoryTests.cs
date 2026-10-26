using NUnit.Framework;
using Unity.Mathematics;

namespace FlagRush.Demo.Tests
{
    public class PredictionHistoryTests
    {
        [Test]
        public void ReturnsRecordedPosition()
        {
            var history = new PredictionHistory(64);
            history.Record(10, new float3(1, 2, 3));
            Assert.That(history.TryGet(10, out var position), Is.True);
            Assert.That(position, Is.EqualTo(new float3(1, 2, 3)));
        }

        [Test]
        public void OverwrittenSlotRejectsOldTick()
        {
            var history = new PredictionHistory(64);
            history.Record(1, new float3(1, 0, 0));
            history.Record(65, new float3(2, 0, 0));
            Assert.That(history.TryGet(1, out _), Is.False, "tick 1 was overwritten by tick 65 (same slot)");
            Assert.That(history.TryGet(65, out var position), Is.True);
            Assert.That(position, Is.EqualTo(new float3(2, 0, 0)));
        }

        [Test]
        public void UnknownTickFalse()
        {
            var history = new PredictionHistory(64);
            Assert.That(history.TryGet(5, out _), Is.False, "empty history");
            history.Record(4, float3.zero);
            Assert.That(history.TryGet(5, out _), Is.False, "neighbouring tick");
            Assert.That(default(PredictionHistory).TryGet(0, out _), Is.False, "default-constructed history");
        }
    }
}
