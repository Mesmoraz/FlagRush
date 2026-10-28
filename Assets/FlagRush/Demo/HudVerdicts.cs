namespace FlagRush.Demo
{
    /// <summary>
    /// The HUD's PASS / FAIL / info verdicts as pure functions of the readout, so they can be tested without
    /// a GUI. <c>null</c> means "info" (nothing to judge). At Levels 1-2 with default counts these give exactly
    /// the verdicts the HUD always showed; at Level 3 (no agents, no probe ghosts) they no longer report
    /// false results.
    /// </summary>
    public static class HudVerdicts
    {
        /// <summary>The swarm has agents to judge. With none, the compute rows are info, not PASS or FAIL.</summary>
        public static bool HasAgents(int swarmCount) => swarmCount > 0;

        /// <summary>Burst row: the job ran and its body was not managed code. Info when there are no agents.</summary>
        public static bool? Burst(int swarmCount, uint swarmFrames, bool ranManaged)
            => HasAgents(swarmCount) ? swarmFrames > 0 && !ranManaged : (bool?)null;

        /// <summary>Worker-threads row: the job body ran on more than one thread. Info when there are no agents.</summary>
        public static bool? Threads(int swarmCount, int threadsSeen)
            => HasAgents(swarmCount) ? threadsSeen > 1 : (bool?)null;

        /// <summary>
        /// Ghosts the client should hold: the orange probe objects plus every player (human and bot), plus at Level 4
        /// the match ghosts on the server (2 flags + 1 match state; 0 below Level 4).
        /// </summary>
        public static int ExpectedGhosts(int ghostTarget, int playersOnServer, int matchGhostsOnServer = 0)
            => ghostTarget + playersOnServer + matchGhostsOnServer;

        /// <summary>
        /// Replicated-objects row: the client holds exactly the expected ghosts, and if there are probe ghosts their
        /// replicated tick field has arrived (the probe tick is how the HUD knows ghost fields update).
        /// </summary>
        public static bool Ghosts(int ghostsOnClient, int ghostTarget, int playersOnServer, uint newestReplicatedTick, int matchGhostsOnServer = 0)
        {
            int expected = ExpectedGhosts(ghostTarget, playersOnServer, matchGhostsOnServer);
            return expected > 0 && ghostsOnClient == expected && (ghostTarget == 0 || newestReplicatedTick > 0);
        }

        /// <summary>What the replicated-objects count is made of, for the compact row's label.</summary>
        public static string GhostsLabel(int matchGhostsOnServer)
            => matchGhostsOnServer > 0 ? "players + flags + match state" : "players + orange";

        /// <summary>
        /// The server tick the client is currently showing: the newest probe tick when probes exist, else the
        /// client's interpolation tick (what interpolated players are rendered at).
        /// </summary>
        public static uint ShownTick(uint newestReplicatedTick, uint clientInterpolationTick)
            => newestReplicatedTick > 0 ? newestReplicatedTick : clientInterpolationTick;

        /// <summary>Snapshot age in ticks: the true server tick minus the tick the client shows.</summary>
        public static int SnapshotAgeTicks(uint serverTick, uint newestReplicatedTick, uint clientInterpolationTick)
            => (int)(serverTick - ShownTick(newestReplicatedTick, clientInterpolationTick));
    }
}
