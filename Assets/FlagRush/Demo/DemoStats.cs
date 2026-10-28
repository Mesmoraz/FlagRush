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
        public static int MatchStatesOnServer;    // match-state ghosts alive on the server (1 at Level 4, else 0)
        public static int FlagsOnClient;          // flag ghosts the client has received
        public static bool MatchStateOnClient;    // client has received the match-state ghost
        public static int RedPlayersOnServer;     // server players with Combatant.Team == 0
        public static int BluePlayersOnServer;    // server players with Combatant.Team == 1
        public static float ShotsPerSecond;       // server: shots resolved per second across all players (1 s window)
        public static float FireConfirmMs;        // client: ms from the latest fire press to the snapshot whose ShotsFired includes it (0 = no sample yet)
        public static short MatchRedScore;        // client: match-state ghost scores
        public static short MatchBlueScore;
        public static bool MatchEnded;            // client: match phase is Ended
        public static uint MatchTicksLeft;        // client: round ticks left (0 once ended)
        public static short YouKills;             // client: owned player's MatchStats
        public static short YouDeaths;
        public static short YouCaptures;
        public static string RedFlagLine = "";    // client: MatchHudText.FlagLine for each flag
        public static string BlueFlagLine = "";
        public static readonly MatchEventRing LastMatchEvents = new MatchEventRing(16); // server: the newest match events
        public static readonly int[] MatchEventTotals = new int[System.Enum.GetValues(typeof(MatchEventKind)).Length]; // server: events per MatchEventKind since the match level started (the ring above keeps only 16); sized from the enum

        // ---- rendering
        public static int DrawnInstances;
        public static int DrawCalls;

        public static string LastError = "";
    }

    /// <summary>Fixed-size ring of the newest <see cref="MatchEvent"/>s; index 0 is the oldest kept.</summary>
    public sealed class MatchEventRing
    {
        readonly MatchEvent[] _items;
        int _start;

        public MatchEventRing(int capacity) => _items = new MatchEvent[capacity];

        public int Capacity => _items.Length;
        public int Count { get; private set; }

        public MatchEvent this[int index] => _items[(_start + index) % _items.Length];

        public void Add(MatchEvent e)
        {
            if (Count < _items.Length)
            {
                _items[(_start + Count) % _items.Length] = e;
                Count++;
                return;
            }
            _items[_start] = e;
            _start = (_start + 1) % _items.Length;
        }

        public void Clear()
        {
            _start = 0;
            Count = 0;
        }

        public MatchEvent[] ToArray()
        {
            var copy = new MatchEvent[Count];
            for (int i = 0; i < Count; i++) copy[i] = this[i];
            return copy;
        }
    }
}
