using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace FlagRush.Spike
{
    /// <summary>
    /// Server authority for the spike: once anyone is connected, spawn a ring of ghosts and move them
    /// every tick. Position is written to both the ghost field and LocalTransform (LocalTransform is
    /// not replicated here; the client re-derives it from the ghost field).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GoInGameSystem))]
    public partial struct SpikeServerSystem : ISystem
    {
        public const int GhostCount = 8;
        bool _spawned;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpikeGhostPrefab>();
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            SpikeStats.ServerTick = tick.IsValid ? tick.TickIndexForValidTick : 0;

            int connections = 0;
            foreach (var _ in SystemAPI.Query<RefRO<NetworkStreamInGame>>()) connections++;
            SpikeStats.ServerConnections = connections;

            if (!_spawned && connections > 0)
            {
                var prefab = SystemAPI.GetSingleton<SpikeGhostPrefab>().Value;
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                for (int i = 0; i < GhostCount; i++)
                {
                    var e = ecb.Instantiate(prefab);
                    ecb.SetComponent(e, new SpikeGhostState { Index = i });
                }
                ecb.Playback(state.EntityManager);
                _spawned = true;
            }

            int ghosts = 0;
            float t = SpikeStats.ServerTick / 60f;
            foreach (var (ghost, transform) in SystemAPI.Query<RefRW<SpikeGhostState>, RefRW<LocalTransform>>())
            {
                float a = t + ghost.ValueRO.Index * (math.PI * 2f / GhostCount);
                var pos = new float3(math.cos(a) * 6f, 1.5f, math.sin(a) * 6f);
                ghost.ValueRW.ServerTick = SpikeStats.ServerTick;
                ghost.ValueRW.Position = pos;
                transform.ValueRW.Position = pos;
                ghosts++;
            }
            SpikeStats.GhostsOnServer = ghosts;
        }
    }
}
