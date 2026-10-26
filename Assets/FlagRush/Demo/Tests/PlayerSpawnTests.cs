using NUnit.Framework;
using Unity.Mathematics;

namespace FlagRush.Demo.Tests
{
    public class PlayerSpawnTests
    {
        [Test]
        public void MaxBotSpawnPointsArePairwiseApartAndInsideTheArena()
        {
            int n = Sandbox.MaxBots;
            Assert.That(n, Is.EqualTo(149));
            var arena = ArenaBounds.Default;
            float minDistance = 2f * PlayerTuning.Radius;
            float closest = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var p = PlayerSpawn.BotPoint(i);
                Assert.That(p.y, Is.EqualTo(0f), $"bot {i} not on the ground");
                Assert.That(p.x > arena.Min.x && p.x < arena.Max.x && p.z > arena.Min.y && p.z < arena.Max.y, Is.True, $"bot {i} at {p} outside the arena");
                for (int j = i + 1; j < n; j++)
                    closest = math.min(closest, math.distance(p.xz, PlayerSpawn.BotPoint(j).xz));
            }
            Assert.That(closest, Is.GreaterThanOrEqualTo(minDistance), "two bot spawn points closer than 2 x radius");
        }

        [Test]
        public void BotPointDoesNotDependOnHowManyBotsExist()
        {
            Assert.That(PlayerSpawn.BotPoint(7), Is.EqualTo(PlayerSpawn.BotPoint(7, Sandbox.MaxBots)));
        }
    }
}
