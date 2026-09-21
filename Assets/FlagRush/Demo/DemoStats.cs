namespace FlagRush.Demo
{
    /// <summary>
    /// The live readout. Systems write here; the HUD, the PlayMode test and the docs read it.
    /// Deliberately a static bag: this is a spike, not a system to keep.
    /// </summary>
    public static class DemoStats
    {
        // ---- compute: can heavy simulation run in a browser?
        public static bool SwarmJobRanManaged;   // true = the job body executed WITHOUT Burst this frame
        public static int SwarmJobThreadsSeen;   // distinct thread indices the job body ran on
        public static int JobWorkerCount;
        public static int SwarmCount;
        public static uint SwarmFrames;
        public static float SwarmJobMs;          // wall time from schedule to complete, smoothed
        public static float FrameMs;             // smoothed

        // ---- network: does server->client replication work here?
        public static bool ServerWorldExists;
        public static bool ClientWorldExists;
        public static bool ClientConnected;
        public static bool ClientInGame;
        public static int ServerConnections;
        public static uint ServerTick;            // true server tick (same process, so we can read it directly)
        public static uint ClientServerTick;      // the client's own estimate of the server tick
        public static int GhostsOnServer;
        public static int GhostsOnClient;
        public static int GhostTarget;
        public static uint NewestReplicatedTick;  // max ProbeGhost.ServerTick received on the client
        public static float EstimatedRttMs;
        public static float SnapshotBytesPerSecond;
        public static float SnapshotsPerSecond;
        public static float AvgSnapshotBytes;
        public static double PacketLossPercent;
        public static int SimulationTickRate;
        public static int NetworkTickRate;

        // ---- level: can baked level data load on the web?
        public static int SubSceneEntitiesClient;
        public static int SubSceneEntitiesServer;

        // ---- rendering
        public static int DrawnInstances;
        public static int DrawCalls;

        public static string LastError = "";
    }
}
