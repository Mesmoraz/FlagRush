using NUnit.Framework;

namespace FlagRush.Demo.Tests
{
    public class LevelConfigTests
    {
        [TearDown]
        public void TearDown() => DemoConfig.OverrideLevel(null);

        [Test]
        public void Level3DefaultsClearAgentsAndGhostsAndSeedBots()
        {
            DemoConfig.OverrideLevel(3);
            Assert.AreEqual(0, Sandbox.Agents);
            Assert.AreEqual(0, Sandbox.Ghosts);
            Assert.AreEqual(31, Sandbox.Bots);
            Assert.IsTrue(DemoConfig.IsPlayerLevel);
        }

        [Test]
        public void OverrideNullRestoresLevel1Defaults()
        {
            DemoConfig.OverrideLevel(3);
            DemoConfig.OverrideLevel(null);
            Assert.AreEqual(1, DemoConfig.Level);
            Assert.AreEqual(DemoConfig.DefaultAgents, Sandbox.Agents);
            Assert.AreEqual(DemoConfig.DefaultGhosts, Sandbox.Ghosts);
            Assert.AreEqual(0, Sandbox.Bots);
            Assert.IsFalse(DemoConfig.IsPlayerLevel);
        }

        [Test]
        public void PredictionResetsToOn()
        {
            Sandbox.PredictionEnabled = false;
            DemoConfig.OverrideLevel(3);
            Assert.IsTrue(Sandbox.PredictionEnabled);
        }
    }
}
