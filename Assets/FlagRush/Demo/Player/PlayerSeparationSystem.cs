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
    /// (the authoritative post-separation position; clients never write it). At the match level only Alive players take
    /// part: a dead player is not drawn, so it must not block either.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [BurstCompile]
    public partial struct PlayerSeparationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerGhost>();
            state.RequireForUpdate<ActiveArena>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var arena = SystemAPI.GetSingleton<ActiveArena>();
            var layout = arena.Layout;
            bool match = arena.Match;
            var positions = new NativeList<float3>(Allocator.Temp);
            // Same query shape in both passes and the same `takesPart` predicate, so the write-back sees the
            // participating entities in the gather order. At the match level a dead player is invisible and
            // frozen, so it neither pushes nor is pushed (Levels 1-3: everyone takes part, as before).
            foreach (var (ghost, combatant) in SystemAPI
                         .Query<RefRO<PlayerGhost>, RefRO<Combatant>>().WithAll<LocalTransform, ServerPosition, Simulate>())
                if (!match || combatant.ValueRO.Alive) positions.Add(ghost.ValueRO.Position);

            PlayerSeparation.Resolve(positions.AsArray(), PlayerTuning.Radius, ArenaBounds.Default, layout);

            int i = 0;
            foreach (var (ghost, combatant, transform, serverPosition) in SystemAPI
                         .Query<RefRW<PlayerGhost>, RefRO<Combatant>, RefRW<LocalTransform>, RefRW<ServerPosition>>().WithAll<Simulate>())
            {
                if (match && !combatant.ValueRO.Alive) continue;
                var p = positions[i++];
                ghost.ValueRW.Position = p;
                transform.ValueRW.Position = p;
                serverPosition.ValueRW.Value = p;
            }
            positions.Dispose();
        }
    }
}
