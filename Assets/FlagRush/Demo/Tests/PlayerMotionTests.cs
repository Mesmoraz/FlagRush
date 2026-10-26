using NUnit.Framework;
using Unity.Mathematics;

namespace FlagRush.Demo.Tests
{
    public class PlayerMotionTests
    {
        static (float3 pos, float2 vel) Run(PlayerInput input, int ticks, float dt = 1f / 60f)
        {
            float3 pos = float3.zero;
            float2 vel = float2.zero;
            var arena = ArenaBounds.Default;
            for (int i = 0; i < ticks; i++) PlayerMotion.Step(ref pos, ref vel, input, dt, arena);
            return (pos, vel);
        }

        [Test]
        public void AcceleratesTowardInput()
        {
            var (pos, vel) = Run(new PlayerInput { Move = new float2(1, 0) }, 5);
            Assert.That(vel.x, Is.GreaterThan(0f));
            Assert.That(pos.x, Is.GreaterThan(0f));
            Assert.That(pos.z, Is.EqualTo(0f));
        }

        [Test]
        public void CapsAtMaxSpeed()
        {
            var (_, vel) = Run(new PlayerInput { Move = new float2(1, 0) }, 120);
            Assert.That(math.length(vel), Is.EqualTo(PlayerTuning.MaxSpeed).Within(1e-4f));
        }

        [Test]
        public void SprintRaisesCap()
        {
            var (_, vel) = Run(new PlayerInput { Move = new float2(1, 0), Sprint = true }, 120);
            Assert.That(math.length(vel), Is.EqualTo(PlayerTuning.SprintSpeed).Within(1e-4f));
        }

        [Test]
        public void ZeroInputComesToRest()
        {
            float3 pos = float3.zero;
            float2 vel = float2.zero;
            var arena = ArenaBounds.Default;
            var go = new PlayerInput { Move = new float2(1, 0) };
            for (int i = 0; i < 120; i++) PlayerMotion.Step(ref pos, ref vel, go, 1f / 60f, arena);
            for (int i = 0; i < 60; i++) PlayerMotion.Step(ref pos, ref vel, default, 1f / 60f, arena);
            Assert.That(vel, Is.EqualTo(float2.zero));
        }

        [Test]
        public void ClampsToArenaAndZeroesVelocityIntoWall()
        {
            var (pos, vel) = Run(new PlayerInput { Move = new float2(1, 0) }, 600);
            Assert.That(pos.x, Is.EqualTo(30f - PlayerTuning.Radius));
            Assert.That(vel.x, Is.EqualTo(0f));
        }

        [Test]
        public void IsDeterministic()
        {
            var a = RunRandom();
            var b = RunRandom();
            Assert.AreEqual(a.x, b.x);
            Assert.AreEqual(a.y, b.y);
            Assert.AreEqual(a.z, b.z);
        }

        static float3 RunRandom()
        {
            var rng = new Random(42);
            float3 pos = float3.zero;
            float2 vel = float2.zero;
            var arena = ArenaBounds.Default;
            for (int i = 0; i < 300; i++)
            {
                var input = new PlayerInput { Move = rng.NextFloat2(-1f, 1f), Sprint = rng.NextBool() };
                PlayerMotion.Step(ref pos, ref vel, input, 1f / 60f, arena);
            }
            return pos;
        }
    }
}
