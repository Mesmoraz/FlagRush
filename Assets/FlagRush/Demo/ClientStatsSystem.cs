using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace FlagRush.Demo
{
    /// <summary>Client readout: connection state, ticks, ghost counts, RTT; also drives ghost transforms from the replicated field.</summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GoInGameSystem))]
    public partial struct ClientStatsSystem : ISystem
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
            DemoStats.ClientConnected = connected;
            DemoStats.ClientInGame = inGame;
            DemoStats.EstimatedRttMs = rtt;

            if (SystemAPI.TryGetSingleton<NetworkTime>(out var time))
                DemoStats.ClientServerTick = time.ServerTick.IsValid ? time.ServerTick.TickIndexForValidTick : 0;

            if (SystemAPI.TryGetSingleton<GhostCount>(out var ghostCount))
                DemoStats.GhostsOnClient = ghostCount.GhostCountOnClient;

            uint newest = 0;
            foreach (var (ghost, transform) in SystemAPI.Query<RefRO<ProbeGhost>, RefRW<LocalTransform>>())
            {
                transform.ValueRW.Position = ghost.ValueRO.Position;
                if (ghost.ValueRO.ServerTick > newest) newest = ghost.ValueRO.ServerTick;
            }
            DemoStats.NewestReplicatedTick = newest;
        }
    }
}
