namespace FlagRush.Spike
{
    /// <summary>
    /// The M1 gate readout. Systems write here; the HUD (and the PlayMode test) read it.
    /// Deliberately a static bag: this is a spike, not a system to keep.
    /// </summary>
    public static class SpikeStats
    {
        // (a) Burst + worker threads
        public static bool SwarmJobRanManaged;   // true means the job body executed WITHOUT Burst
        public static int SwarmJobThreadsSeen;   // distinct thread indices the job body ran on
        public static int JobWorkerCount;
        public static int SwarmCount;
        public static uint SwarmFrames;
        public static int DrawnInstances;      // instances submitted to Graphics.RenderMeshInstanced last frame

        // (b) Netcode: IPC client<->server worlds in one process
        public static bool ServerWorldExists;
        public static bool ClientWorldExists;
        public static bool ClientConnected;
        public static bool ClientInGame;
        public static int ServerConnections;
        public static uint ServerTick;
        public static uint ClientServerTick;      // NetworkTime.ServerTick as seen by the client
        public static int GhostsOnServer;
        public static int GhostsOnClient;
        public static uint NewestReplicatedTick;  // max SpikeGhostState.ServerTick received on the client
        public static float EstimatedRttMs;

        // (c) SubScene
        public static int SubSceneEntitiesClient;
        public static int SubSceneEntitiesServer;

        public static string LastError = "";
    }
}
