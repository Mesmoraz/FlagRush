using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace FlagRush.Demo.Tests
{
    public class HitscanTests
    {
        static readonly float2 Right = new float2(1f, 0f);

        static int Cast(float2 origin, float2 dir, in ArenaLayout layout, float2[] pos, byte[] team, bool[] hittable, int shooter)
        {
            var p = new NativeArray<float2>(pos, Allocator.Temp);
            var t = new NativeArray<byte>(team, Allocator.Temp);
            var h = new NativeArray<bool>(hittable, Allocator.Temp);
            try { return Hitscan.Cast(origin, dir, MatchTuning.ShotRange, layout, p, t, h, team[shooter], shooter); }
            finally { p.Dispose(); t.Dispose(); h.Dispose(); }
        }

        [Test]
        public void HitsNearestEnemyOnRay()
        {
            int hit = Cast(new float2(-24f, 0f), Right, ArenaLayout.Empty,
                new[] { new float2(-24f, 0f), new float2(-18f, 0f), new float2(-20f, 0f) },
                new byte[] { 0, 1, 1 }, new[] { true, true, true }, 0);
            Assert.AreEqual(2, hit);
        }

        [Test]
        public void BoxBlocksShot()
        {
            int hit = Cast(new float2(-20f, 0f), Right, ArenaLayout.Match,
                new[] { new float2(-20f, 0f), new float2(-5f, 0f) },
                new byte[] { 0, 1 }, new[] { true, true }, 0);
            Assert.AreEqual(-1, hit);
        }

        [Test]
        public void TeammatesAreIgnored()
        {
            int hit = Cast(new float2(-24f, 0f), Right, ArenaLayout.Empty,
                new[] { new float2(-24f, 0f), new float2(-22f, 0f), new float2(-20f, 0f) },
                new byte[] { 0, 0, 1 }, new[] { true, true, true }, 0);
            Assert.AreEqual(2, hit);
        }

        [Test]
        public void DeadAreNotHittable()
        {
            int hit = Cast(new float2(-24f, 0f), Right, ArenaLayout.Empty,
                new[] { new float2(-24f, 0f), new float2(-20f, 0f) },
                new byte[] { 0, 1 }, new[] { true, false }, 0);
            Assert.AreEqual(-1, hit);
        }

        [Test]
        public void RangeLimitIs40m()
        {
            var team = new byte[] { 0, 1 };
            var ok = new[] { true, true };
            Assert.AreEqual(-1, Cast(float2.zero, Right, ArenaLayout.Empty,
                new[] { float2.zero, new float2(40.7f, 0f) }, team, ok, 0));
            Assert.AreEqual(1, Cast(float2.zero, Right, ArenaLayout.Empty,
                new[] { float2.zero, new float2(40.5f, 0f) }, team, ok, 0));
        }

        [Test]
        public void ZeroAimDoesNotFire()
        {
            int hit = Cast(new float2(-24f, 0f), float2.zero, ArenaLayout.Empty,
                new[] { new float2(-24f, 0f), new float2(-20f, 0f) },
                new byte[] { 0, 1 }, new[] { true, true }, 0);
            Assert.AreEqual(-1, hit);
        }

        [Test]
        public void ShooterNeverHitsSelf()
        {
            int hit = Cast(new float2(-24f, 0f), Right, ArenaLayout.Empty,
                new[] { new float2(-23f, 0f) },
                new byte[] { 0 }, new[] { true }, 0);
            Assert.AreEqual(-1, hit);
        }

        [Test]
        public void FireGateAllowsFourPerSecond()
        {
            uint next = 0;
            int fired = 0;
            for (uint tick = 0; tick < 60; tick++)
                if (Combat.TryFire(ref next, tick)) fired++;
            Assert.AreEqual(4, fired);
            Assert.AreEqual(60u, next);
        }

        [Test]
        public void ThreeHitsKill()
        {
            var c = new Combatant { Team = 1, Health = MatchTuning.MaxHealth };
            Assert.IsFalse(Combat.ApplyHit(ref c, 100));
            Assert.IsFalse(Combat.ApplyHit(ref c, 101));
            Assert.AreEqual(32, c.Health);
            Assert.IsTrue(Combat.ApplyHit(ref c, 102));
            Assert.AreEqual(0, c.Health);
            Assert.AreEqual(102u + MatchTuning.RespawnTicks, c.RespawnTick);
            Assert.AreEqual(102u, c.LastHitTick);
            Assert.IsFalse(c.Alive);
        }
    }
}
