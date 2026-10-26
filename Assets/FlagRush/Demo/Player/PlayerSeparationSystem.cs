using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace FlagRush.Demo
{
    /// <summary>
    /// Server: after movement, pushes overlapping players apart (<see cref="PlayerSeparation.Resolve"/>) and writes
    /// the result to <see cref="PlayerGhost.Position"/>, <see cref="LocalTransform"/> and <see cref="ServerPosition"/>
    /// (the authoritative post-separation position; clients never write it).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [BurstCompile]
    public partial struct PlayerSeparationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<PlayerGhost>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var positions = new NativeList<float3>(Allocator.Temp);
            // Same query shape in both passes, so the write-back sees the entities in the gather order.
            foreach (var (ghost, _, _) in SystemAPI
                         .Query<RefRW<PlayerGhost>, RefRW<LocalTransform>, RefRW<ServerPosition>>().WithAll<Simulate>())
                positions.Add(ghost.ValueRO.Position);

            PlayerSeparation.Resolve(positions.AsArray(), PlayerTuning.Radius, ArenaBounds.Default);

            int i = 0;
            foreach (var (ghost, transform, serverPosition) in SystemAPI
                         .Query<RefRW<PlayerGhost>, RefRW<LocalTransform>, RefRW<ServerPosition>>().WithAll<Simulate>())
            {
                var p = positions[i++];
                ghost.ValueRW.Position = p;
                transform.ValueRW.Position = p;
                serverPosition.ValueRW.Value = p;
            }
            positions.Dispose();
        }
    }
}
