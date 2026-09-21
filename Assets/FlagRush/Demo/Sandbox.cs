namespace FlagRush.Demo
{
    /// <summary>
    /// The live knobs (Level 2). The control panel writes them; systems read them on the main thread each
    /// frame and apply changes. Bumping <see cref="LinkVersion"/> tells the client to push new link
    /// parameters into the transport's simulator stage.
    /// </summary>
    public static class Sandbox
    {
        public const int MaxAgents = 20000;
        public const int MaxGhosts = 1000;
        public const int SimulatorPacketBuffer = 4096; // fixed at driver creation; cannot change at runtime

        public static int Agents = DemoConfig.Agents;
        public static int Ghosts = DemoConfig.Ghosts;

        // link emulation on the client driver, applied to both directions => RTT ~= 2 * LatencyMs
        public static int LatencyMs;
        public static int JitterMs;
        public static int LossPercent;
        public static int LinkVersion = 1;
        public static int LinkVersionApplied;

        public static int NetworkTickRate = 60;      // 10/20/30/60; changing forces a reconnect so both sides agree
        public static int InterpolationMs;           // 0 = netcode default (derived from tick rates)

        public static bool ServerFrozen;
        public static bool KillConnectionRequested;
        public static int Reconnects;

        public static void SetLink(int latencyMs, int jitterMs, int lossPercent)
        {
            LatencyMs = latencyMs; JitterMs = jitterMs; LossPercent = lossPercent;
            LinkVersion++;
        }
    }
}
