using NUnit.Framework;

namespace FlagRush.Demo.Tests
{
    public class HudVerdictsTests
    {
        [Test]
        public void Level3WithNoAgentsNoGhostsAnd150PlayersHasNoFalseVerdicts()
        {
            // Level 3 defaults: 0 agents, 0 probe ghosts, 1 human + 149 bots, no probe tick ever received.
            Assert.That(HudVerdicts.HasAgents(0), Is.False);
            Assert.That(HudVerdicts.Burst(0, 0, false), Is.Null, "Burst row must be info with no agents");
            Assert.That(HudVerdicts.Burst(0, 500, false), Is.Null, "an empty-query job run is not a Burst PASS");
            Assert.That(HudVerdicts.Threads(0, 1), Is.Null, "threads row must be info with no agents");
            Assert.That(HudVerdicts.ExpectedGhosts(0, 150), Is.EqualTo(150));
            Assert.That(HudVerdicts.Ghosts(150, 0, 150, 0), Is.True, "all 150 players replicated is a PASS");
            Assert.That(HudVerdicts.Ghosts(149, 0, 150, 0), Is.False, "a missing player is a FAIL");
            Assert.That(HudVerdicts.Ghosts(0, 0, 0, 0), Is.False, "nothing expected yet (connecting) is not a PASS");
        }

        [Test]
        public void Level4CountsFlagAndMatchStateGhosts()
        {
            // Level 4 browser case: 0 probe ghosts, 1 human + 149 bots, 2 flags + 1 match state replicated = 153.
            Assert.That(HudVerdicts.ExpectedGhosts(0, 150, 3), Is.EqualTo(153));
            Assert.That(HudVerdicts.Ghosts(153, 0, 150, 0, 3), Is.True, "players + flags + match state all replicated is a PASS");
            Assert.That(HudVerdicts.Ghosts(152, 0, 150, 0, 3), Is.False, "a missing flag or match state is a FAIL");
            Assert.That(HudVerdicts.Ghosts(150, 0, 150, 0, 3), Is.False, "players only, match ghosts not yet replicated, is a FAIL");
            Assert.That(HudVerdicts.GhostsLabel(3), Is.EqualTo("players + flags + match state"));
            // Below Level 4 there are no match ghosts and the row reads exactly as before.
            Assert.That(HudVerdicts.Ghosts(150, 0, 150, 0, 0), Is.EqualTo(HudVerdicts.Ghosts(150, 0, 150, 0)));
            Assert.That(HudVerdicts.GhostsLabel(0), Is.EqualTo("players + orange"));
        }

        [Test]
        public void SnapshotAgeFallsBackToInterpolationTickWithoutProbes()
        {
            Assert.That(HudVerdicts.ShownTick(0, 990), Is.EqualTo(990u));
            Assert.That(HudVerdicts.SnapshotAgeTicks(1000, 0, 990), Is.EqualTo(10), "no probes: server tick - interpolation tick");
            Assert.That(HudVerdicts.SnapshotAgeTicks(1000, 995, 990), Is.EqualTo(5), "probes: server tick - newest probe tick");
        }

        [Test]
        public void Levels1And2VerdictsMatchThePreviousFormulas()
        {
            // With agents present and no players, every verdict equals the HUD's original expressions.
            foreach (int agents in new[] { 1, 150, 5000 })
            foreach (uint frames in new uint[] { 0, 1, 100 })
            foreach (bool managed in new[] { false, true })
                Assert.That(HudVerdicts.Burst(agents, frames, managed), Is.EqualTo(frames > 0 && !managed));

            foreach (int agents in new[] { 1, 150 })
            foreach (int threads in new[] { 0, 1, 2, 8 })
                Assert.That(HudVerdicts.Threads(agents, threads), Is.EqualTo(threads > 1));

            foreach (int target in new[] { 0, 8, 40 })
            foreach (int onClient in new[] { 0, 7, 8, 40 })
            foreach (uint newest in new uint[] { 0, 1234 })
            {
                bool before = target > 0 && onClient == target && newest > 0;
                Assert.That(HudVerdicts.Ghosts(onClient, target, 0, newest), Is.EqualTo(before), $"target {target} client {onClient} newest {newest}");
                Assert.That(HudVerdicts.ExpectedGhosts(target, 0), Is.EqualTo(target));
            }

            foreach (uint newest in new uint[] { 1, 900, 1000 })
                Assert.That(HudVerdicts.SnapshotAgeTicks(1000, newest, 777), Is.EqualTo((int)(1000 - newest)));
        }
    }
}
