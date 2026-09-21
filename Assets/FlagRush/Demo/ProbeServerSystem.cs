using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace FlagRush.Demo
{
    /// <summary>
    /// Server authority for the spike: once anyone is connected, spawn a ring of ghosts and move them
    /// every tick. Position is written to both the ghost field and LocalTransform (LocalTransform is
    /// not replicated here; the client re-derives it from the ghost field).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GoInGameSystem))]
    public partial struct ProbeServerSystem : ISystem
    {
        public static int GhostCount => DemoConfig.Ghosts;
        bool _spawned;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProbeGhostPrefab>();
            state.RequireForUpdate<NetworkTime>();
            DemoStats.GhostTarget = GhostCount;
        }

        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            DemoStats.ServerTick = tick.IsValid ? tick.TickIndexForValidTick : 0;

            int connections = 0;
            foreach (var _ in SystemAPI.Query<RefRO<NetworkStreamInGame>>()) connections++;
            DemoStats.ServerConnections = connections;
            if (!SystemAPI.TryGetSingleton<ClientServerTickRate>(out var rate)) rate = default;
            rate.ResolveDefaults();
            DemoStats.SimulationTickRate = rate.SimulationTickRate;
            DemoStats.NetworkTickRate = rate.NetworkTickRate;

            if (!_spawned && connections > 0)
            {
                var prefab = SystemAPI.GetSingleton<ProbeGhostPrefab>().Value;
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                for (int i = 0; i < GhostCount; i++)
                {
                    var e = ecb.Instantiate(prefab);
                    ecb.SetComponent(e, new ProbeGhost { Index = i });
                }
                ecb.Playback(state.EntityManager);
                _spawned = true;
            }

            int ghosts = 0;
            float t = DemoStats.ServerTick / 60f;
            foreach (var (ghost, transform) in SystemAPI.Query<RefRW<ProbeGhost>, RefRW<LocalTransform>>())
            {
                // 8 per ring, rings 2 units apart, alternating spin direction, so any count reads as a pattern.
                int ring = ghost.ValueRO.Index / 8;
                float radius = 6f + ring * 2f;
                float dir = (ring & 1) == 0 ? 1f : -1f;
                float a = dir * t + (ghost.ValueRO.Index % 8) * (math.PI * 2f / 8f) + ring * 0.2f;
                var pos = new float3(math.cos(a) * radius, 1.5f + ring * 0.25f, math.sin(a) * radius);
                ghost.ValueRW.ServerTick = DemoStats.ServerTick;
                ghost.ValueRW.Position = pos;
                transform.ValueRW.Position = pos;
                ghosts++;
            }
            DemoStats.GhostsOnServer = ghosts;
        }
    }
}
