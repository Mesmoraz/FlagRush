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
            // Combatant replicates to the owner, so the client applies the same dead / carrying rules it predicts with.
            foreach (var (ghost, input, combatant, transform) in SystemAPI
                         .Query<RefRW<PlayerGhost>, RefRO<PlayerInput>, RefRO<Combatant>, RefRW<LocalTransform>>()
                         .WithAll<Simulate>())
            {
                var command = input.ValueRO;
                if (!combatant.ValueRO.Alive) command = default;            // the dead get zero input
                else if (combatant.ValueRO.Carrying) command.Sprint = false; // flag carriers cannot sprint

                var position = ghost.ValueRO.Position;
                var velocity = ghost.ValueRO.Velocity;
                PlayerMotion.Step(ref position, ref velocity, command, dt, arena, layout);
                ghost.ValueRW.Position = position;
                ghost.ValueRW.Velocity = velocity;
                transform.ValueRW.Position = position;
            }
        }
    }
}
