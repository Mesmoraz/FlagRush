using NUnit.Framework;
using Unity.Mathematics;

namespace FlagRush.Demo.Tests
{
    public class BotBrainTests
    {
        [Test]
        public void SameSeedSameInputs()
        {
            var arena = ArenaBounds.Default;
            var a = BotBrainState.Create(3);
            var b = BotBrainState.Create(3);
            float2 posA = float2.zero, posB = float2.zero;
            for (uint tick = 0; tick < 600; tick++)
            {
                var ia = BotBrain.Think(ref a, posA, tick, arena);
                var ib = BotBrain.Think(ref b, posB, tick, arena);
                Assert.That(ia.Move.Equals(ib.Move) && ia.Sprint == ib.Sprint, Is.True, $"inputs diverged at tick {tick}");
                posA += ia.Move * 0.1f;
                posB += ib.Move * 0.1f;
            }
        }

        [Test]
        public void TargetsStayInsideShrunkArena()
        {
            var arena = ArenaBounds.Default;
            var s = BotBrainState.Create(7);
            for (int i = 0; i < 1000; i++)
            {
                BotBrain.Think(ref s, new float2(100f, 100f), s.NextRetargetTick, arena); // tick >= NextRetargetTick forces a retarget
                Assert.That(math.all(math.abs(s.Target) <= 28f), Is.True, $"target {s.Target} outside the shrunk arena");
            }
        }

        [Test]
        public void MovesTowardTarget()
        {
            var arena = ArenaBounds.Default;
            for (int seed = 0; seed < 20; seed++)
            {
                var s = BotBrainState.Create(seed);
                var pos = new float2(29f, -29f);
                var input = BotBrain.Think(ref s, pos, 0, arena);
                Assert.That(math.dot(input.Move, s.Target - pos), Is.GreaterThan(0f), "Move does not point at the target");
                Assert.That(math.length(input.Move), Is.EqualTo(1f).Within(1e-4f), "Move is not unit length");
            }
        }
    }
}
