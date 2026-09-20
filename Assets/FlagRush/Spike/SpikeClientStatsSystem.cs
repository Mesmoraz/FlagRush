using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace FlagRush.Spike
{
    /// <summary>Client readout: connection state, ticks, ghost counts, RTT; also drives ghost transforms from the replicated field.</summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GoInGameSystem))]
    public partial struct SpikeClientStatsSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            bool connected = false, inGame = false;
            float rtt = 0f;
            foreach (var (_, ack, entity) in SystemAPI.Query<RefRO<NetworkId>, RefRO<NetworkSnapshotAck>>().WithEntityAccess())
            {
                connected = true;
                inGame = SystemAPI.HasComponent<NetworkStreamInGame>(entity);
                rtt = ack.ValueRO.EstimatedRTT;
            }
            SpikeStats.ClientConnected = connected;
            SpikeStats.ClientInGame = inGame;
            SpikeStats.EstimatedRttMs = rtt;

            if (SystemAPI.TryGetSingleton<NetworkTime>(out var time))
                SpikeStats.ClientServerTick = time.ServerTick.IsValid ? time.ServerTick.TickIndexForValidTick : 0;

            if (SystemAPI.TryGetSingleton<GhostCount>(out var ghostCount))
                SpikeStats.GhostsOnClient = ghostCount.GhostCountOnClient;

            uint newest = 0;
            foreach (var (ghost, transform) in SystemAPI.Query<RefRO<SpikeGhostState>, RefRW<LocalTransform>>())
            {
                transform.ValueRW.Position = ghost.ValueRO.Position;
                if (ghost.ValueRO.ServerTick > newest) newest = ghost.ValueRO.ServerTick;
            }
            SpikeStats.NewestReplicatedTick = newest;
        }
    }
}
