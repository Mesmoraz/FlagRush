using NUnit.Framework;
using Unity.Mathematics;

namespace FlagRush.Demo.Tests
{
    public class ObstaclesTests
    {
        const float Tol = 1e-4f;

        static bool Inside(float2 p, in ArenaLayout l)
        {
            for (int i = 0; i < l.Boxes.Length; i++)
            {
                var b = l.Boxes[i];
                float2 d = math.abs(p - b.Center);
                if (d.x < b.HalfExtents.x - 1e-5f && d.y < b.HalfExtents.y - 1e-5f) return true;
            }
            return false;
        }

        [Test]
        public void MatchLayoutHasEightMirroredBoxes()
        {
            var l = ArenaLayout.Match;
            Assert.AreEqual(8, l.Boxes.Length);
            for (int i = 0; i < l.Boxes.Length; i++)
            {
                var b = l.Boxes[i];
                Assert.IsTrue(Has(l, new float2(-b.Center.x, b.Center.y), b.HalfExtents), $"x mirror of box {i}");
                Assert.IsTrue(Has(l, new float2(b.Center.x, -b.Center.y), b.HalfExtents), $"z mirror of box {i}");
            }
        }

        static bool Has(in ArenaLayout l, float2 c, float2 h)
        {
            for (int i = 0; i < l.Boxes.Length; i++)
                if (math.all(math.abs(l.Boxes[i].Center - c) < 1e-5f) && math.all(l.Boxes[i].HalfExtents == h)) return true;
            return false;
        }

        [Test]
        public void EmptyLayoutHasNoBoxes() => Assert.AreEqual(0, ArenaLayout.Empty.Boxes.Length);

        [Test]
        public void SlidePushesCircleOutOfBoxFace()
        {
            var pos = new float3(11.5f, 0f, 0f);
            var vel = new float2(5f, 0f);
            Obstacles.Slide(ref pos, ref vel, 0.6f, ArenaLayout.Match);
            Assert.That(pos.x, Is.EqualTo(11f - 0.6f).Within(Tol));
            Assert.That(vel.x, Is.EqualTo(0f));
            Assert.That(vel.y, Is.EqualTo(0f));
        }

        [Test]
        public void SlideKeepsTangentialVelocity()
        {
            var pos = new float3(11.3f, 0f, 2f);
            var vel = new float2(3f, 4f);
            Obstacles.Slide(ref pos, ref vel, 0.6f, ArenaLayout.Match);
            Assert.That(vel.x, Is.EqualTo(0f).Within(Tol));
            Assert.That(vel.y, Is.EqualTo(4f).Within(Tol));
        }

        [Test]
        public void SlideFromCornerPushesDiagonally()
        {
            var pos = new float3(13.2f, 0f, 5.2f);
            var vel = float2.zero;
            Obstacles.Slide(ref pos, ref vel, 0.6f, ArenaLayout.Match);
            Assert.That(math.distance(pos.xz, new float2(13f, 5f)), Is.EqualTo(0.6f).Within(Tol));
        }

        [Test]
        public void SegmentHitReportsNearestBox()
        {
            var l = ArenaLayout.Match;
            Assert.IsTrue(Obstacles.SegmentHit(new float2(-20f, 0f), new float2(20f, 0f), l, out float t, out int box));
            Assert.That(l.Boxes[box].Center, Is.EqualTo(new float2(-12f, 0f)));
            Assert.That(t, Is.EqualTo((-13f - -20f) / 40f).Within(Tol));
        }

        [Test]
        public void SegmentMissReturnsFalse() =>
            Assert.IsFalse(Obstacles.SegmentHit(new float2(-20f, 7f), new float2(20f, 7f), ArenaLayout.Match, out _, out _));

        [Test]
        public void PushOutMovesPointToBoxEdge()
        {
            var l = ArenaLayout.Match;
            var p = Obstacles.PushOut(new float2(12f, 0f), 0f, l);
            Assert.IsFalse(Inside(p, l));
            Assert.That(math.abs(p.x - 12f), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void SpawnPointsStayInZone()
        {
            var l = ArenaLayout.Match;
            for (byte team = 0; team < 2; team++)
            for (int i = 0; i < 80; i++)
            {
                var s = ArenaLayout.SpawnPoint(team, i);
                float sign = team == 0 ? -1f : 1f;
                Assert.That(s.x * sign, Is.InRange(26f, 29f), $"team {team} slot {i}");
                Assert.That(math.abs(s.z), Is.InRange(12f, 20f), $"team {team} slot {i} not in a flank strip");
                Assert.That(s.y, Is.EqualTo(0f));
                Assert.IsFalse(Inside(s.xz, l));
            }
        }

        [Test]
        public void SpawnSlotsAreOffTheFlagStandAndSplitBetweenBothFlanks()
        {
            var l = ArenaLayout.Match;
            for (byte team = 0; team < 2; team++)
            {
                int plus = 0, minus = 0;
                float2 stand = l.Base(team);
                for (int i = 0; i < ArenaLayout.SpawnSlots; i++)
                {
                    var s = ArenaLayout.SpawnPoint(team, i);
                    Assert.That(math.abs(s.z), Is.GreaterThanOrEqualTo(12f), $"team {team} slot {i} in front of the stand");
                    Assert.That(math.distance(s.xz, stand), Is.GreaterThan(12f), $"team {team} slot {i}");
                    Assert.That(s, Is.EqualTo(ArenaLayout.SpawnPoint(team, i + ArenaLayout.SpawnSlots)), "slots wrap mod 80");
                    if (s.z > 0f) plus++; else minus++;
                    for (int j = i + 1; j < ArenaLayout.SpawnSlots; j++)
                        Assert.That(math.distance(s.xz, ArenaLayout.SpawnPoint(team, j).xz), Is.GreaterThan(0.1f), $"slots {i} and {j} coincide");
                }
                Assert.That(plus, Is.EqualTo(40));
                Assert.That(minus, Is.EqualTo(40));
            }
        }
    }
}
