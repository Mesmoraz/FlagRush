using Unity.Entities;

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
        public static uint ClientInterpolationTick; // the server tick interpolated ghosts are shown at on the client
        public static int GhostsOnServer;
        public static int GhostsOnClient;
        public static int GhostTarget;
        public static uint NewestReplicatedTick;  // max ProbeGhost.ServerTick received on the client
        public static float SnapshotAgeMs;         // (server tick - newest replicated tick) in ms, smoothed
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

        // ---- player (Level 3)
        public static int PlayersOnServer;        // player ghosts alive on the server
        public static int BotsOnServer;           // bot player ghosts alive on the server (BotIndex >= 0)
        public static int PlayersOnClient;        // player ghosts the client has received
        public static Entity OwnedPlayerEntity;   // client: the ghost this client owns (Entity.Null if none)
        public static bool OwnedPlayerPredicted;  // client: the owned ghost is predicted (has PredictedGhost)
        public static int PredictedTicksPerFrame; // client: PredictedSimulationSystemGroup iterations this frame
        public static float RollbacksPerSecond;   // client: frames whose first predicted tick re-simulated an already fully predicted tick, per second (1 s window)
        public static int CommandAgeTicks;        // client: NetworkTime.ServerTick - NetworkSnapshotAck.LastReceivedSnapshotByLocal
        public static float CommandAgeMs;         // client: CommandAgeTicks at the simulation tick rate
        public static float MispredictionMeters;  // client: |ServerPosition - predicted position at the last received snapshot tick|; 0 with prediction off

        // ---- match (Level 4)
        public static int FlagsOnServer;          // flag ghosts alive on the server
        public static int FlagsOnClient;          // flag ghosts the client has received
        public static bool MatchStateOnClient;    // client has received the match-state ghost
        public static int RedPlayersOnServer;     // server players with Combatant.Team == 0
        public static int BluePlayersOnServer;    // server players with Combatant.Team == 1

        // ---- rendering
        public static int DrawnInstances;
        public static int DrawCalls;

        public static string LastError = "";
    }
}
