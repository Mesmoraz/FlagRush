using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace FlagRush.Demo.Tests
{
    public class PlayerSeparationTests
    {
        const float R = PlayerTuning.Radius;

        static float3[] Resolve(params float3[] points)
        {
            using var positions = new NativeArray<float3>(points, Allocator.Temp);
            PlayerSeparation.Resolve(positions, R, ArenaBounds.Default, ArenaLayout.Empty);
            return positions.ToArray();
        }

        static float XZDistance(float3 a, float3 b) => math.distance(a.xz, b.xz);

        [Test]
        public void OverlappingPairEndsAtTwoRadii()
        {
            var r = Resolve(new float3(0, 0, 0), new float3(0.3f, 0, 0.4f));
            Assert.That(XZDistance(r[0], r[1]), Is.GreaterThanOrEqualTo(2f * R - 1e-4f));
        }

        [Test]
        public void CoincidentPointsSeparateDeterministically()
        {
            var r = Resolve(float3.zero, float3.zero);
            Assert.That(r[0].x, Is.LessThan(r[1].x), "first point should go -X, second +X");
            Assert.That(XZDistance(r[0], r[1]), Is.GreaterThanOrEqualTo(2f * R - 1e-4f));
            var again = Resolve(float3.zero, float3.zero);
            Assert.That(again[0].Equals(r[0]) && again[1].Equals(r[1]), Is.True, "not deterministic");
        }

        [Test]
        public void FarPointsUntouched()
        {
            var a = new float3(1, 0, 2);
            var b = new float3(4, 0, 6); // 5 m apart
            var r = Resolve(a, b);
            Assert.That(r[0].Equals(a) && r[1].Equals(b), Is.True);
        }

        [Test]
        public void ResultStaysInArena()
        {
            var arena = ArenaBounds.Default;
            float wall = arena.Max.x - R;
            var r = Resolve(new float3(wall, 0, 0), new float3(wall, 0, 0.1f), new float3(wall - 0.2f, 0, 0));
            foreach (var p in r)
            {
                Assert.That(p.x, Is.InRange(arena.Min.x + R - 1e-4f, arena.Max.x - R + 1e-4f), $"x out of arena: {p}");
                Assert.That(p.z, Is.InRange(arena.Min.y + R - 1e-4f, arena.Max.y - R + 1e-4f), $"z out of arena: {p}");
            }
        }
    }
}
