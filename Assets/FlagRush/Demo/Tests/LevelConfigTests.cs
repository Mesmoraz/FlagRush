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
        public void Level4IsMatchAndPlayerLevel()
        {
            DemoConfig.OverrideLevel(4);
            Assert.AreEqual(4, DemoConfig.Level);
            Assert.IsTrue(DemoConfig.IsMatchLevel);
            Assert.IsTrue(DemoConfig.IsPlayerLevel);
        }

        [Test]
        public void Level3IsNotMatch()
        {
            DemoConfig.OverrideLevel(3);
            Assert.IsFalse(DemoConfig.IsMatchLevel);
        }

        [Test]
        public void Level5ClampsTo4()
        {
            DemoConfig.OverrideLevel(5);
            Assert.AreEqual(4, DemoConfig.Level);
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
