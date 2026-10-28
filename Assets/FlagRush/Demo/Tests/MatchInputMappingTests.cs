using NUnit.Framework;
using Unity.Mathematics;

namespace FlagRush.Demo.Tests
{
    public class MatchInputMappingTests
    {
        static readonly float3 Origin = new float3(0, 18, -14);

        [Test]
        public void CursorAimPointsAtGroundHit()
        {
            var dir = math.normalize(new float3(5, 0, 0) - Origin);
            var aim = PlayerInputMapping.AimFromCursor(Origin, dir, float2.zero);
            Assert.That(aim.x, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(aim.y, Is.EqualTo(0f).Within(1e-4f));

            dir = math.normalize(new float3(5, 0, 14) - Origin); // ground hit at (5, 14)
            aim = PlayerInputMapping.AimFromCursor(Origin, dir, float2.zero);
            var expected = math.normalize(new float2(5, 14));
            Assert.That(aim.x, Is.EqualTo(expected.x).Within(1e-4f));
            Assert.That(aim.y, Is.EqualTo(expected.y).Within(1e-4f));
        }

        [Test]
        public void CursorOnPlayerGivesZero()
        {
            var dir = math.normalize(new float3(0.03f, 0, 0) - Origin);
            Assert.That(PlayerInputMapping.AimFromCursor(Origin, dir, float2.zero), Is.EqualTo(float2.zero));
        }

        [Test]
        public void UpwardRayGivesZero()
        {
            Assert.That(PlayerInputMapping.AimFromCursor(Origin, new float3(0, 0, 1), float2.zero), Is.EqualTo(float2.zero));
            Assert.That(PlayerInputMapping.AimFromCursor(Origin, new float3(0, 1, 1), float2.zero), Is.EqualTo(float2.zero));
        }

        [Test]
        public void StickAimIsCameraRelative()
        {
            var fwd = new float3(0, -0.77f, 0.64f);
            var right = new float3(1, 0, 0);
            var aim = PlayerInputMapping.AimFromStick(new float2(0, 1), fwd, right);
            Assert.That(aim.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(aim.y, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(PlayerInputMapping.AimFromStick(new float2(0.1f, 0.1f), fwd, right), Is.EqualTo(float2.zero));
            Assert.That(PlayerInputMapping.AimFromStick(new float2(0.5f, 0f), fwd, right).x, Is.EqualTo(1f).Within(1e-4f), "aim is a unit direction");
        }
    }
}
