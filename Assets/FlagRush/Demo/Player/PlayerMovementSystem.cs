using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace FlagRush.Demo
{
    /// <summary>
    /// Predicted movement: the same <see cref="PlayerMotion.Step"/> runs on the server (authority) and on the
    /// owning client (prediction + rollback). <see cref="Simulate"/> limits the client to ghosts it predicts this tick.
    /// </summary>
    /// <remarks>Burst is applied to OnUpdate only; PlayerMotion.Step is inlined from there (see Task 1, BC1063).</remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [BurstCompile]
    public partial struct PlayerMovementSystem : ISystem
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
            float dt = SystemAPI.Time.DeltaTime;
            var arena = ArenaBounds.Default;
            var layout = SystemAPI.GetSingleton<ActiveArena>().Layout;

            // ServerPosition is not touched here: on the server PlayerSeparationSystem runs right after this system
            // and writes it for every player; on the client it is replicated-only (never written locally).
            foreach (var (ghost, input, transform) in SystemAPI
                         .Query<RefRW<PlayerGhost>, RefRO<PlayerInput>, RefRW<LocalTransform>>()
                         .WithAll<Simulate>())
            {
                var position = ghost.ValueRO.Position;
                var velocity = ghost.ValueRO.Velocity;
                PlayerMotion.Step(ref position, ref velocity, input.ValueRO, dt, arena, layout);
                ghost.ValueRW.Position = position;
                ghost.ValueRW.Velocity = velocity;
                transform.ValueRW.Position = position;
            }
        }
    }
}
