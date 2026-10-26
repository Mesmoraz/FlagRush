using NUnit.Framework;
using Unity.Mathematics;

namespace FlagRush.Demo.Tests
{
    public class PlayerInputMappingTests
    {
        static readonly float3 WorldForward = new float3(0, 0, 1);
        static readonly float3 WorldRight = new float3(1, 0, 0);

        [Test]
        public void WMovesAlongCameraForwardFlattened()
        {
            var forward = math.normalize(new float3(0, -0.77f, 0.64f));
            var input = PlayerInputMapping.FromAxes(new float2(0, 1), forward, new float3(1, 0, 0), false);
            Assert.That(input.Move.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(input.Move.y, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void DiagonalIsClampedToUnitLength()
        {
            var input = PlayerInputMapping.FromAxes(new float2(1, 1), WorldForward, WorldRight, false);
            Assert.That(math.length(input.Move), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void StickOvershootClamped()
        {
            var input = PlayerInputMapping.FromAxes(new float2(1.2f, 0), WorldForward, WorldRight, false);
            Assert.That(math.length(input.Move), Is.LessThanOrEqualTo(1f + 1e-5f));
        }

        [Test]
        public void ZeroCameraFallsBackToWorldAxes()
        {
            var input = PlayerInputMapping.FromAxes(new float2(1, 0), float3.zero, float3.zero, false);
            Assert.That(input.Move.x, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(input.Move.y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void SprintPassesThrough()
        {
            var input = PlayerInputMapping.FromAxes(float2.zero, WorldForward, WorldRight, true);
            Assert.That(input.Sprint, Is.True);
        }
    }
}
