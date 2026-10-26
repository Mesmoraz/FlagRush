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
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<PlayerGhost>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var arena = ArenaBounds.Default;
            bool isServer = state.WorldUnmanaged.IsServer();

            foreach (var (ghost, input, transform, serverPosition) in SystemAPI
                         .Query<RefRW<PlayerGhost>, RefRO<PlayerInput>, RefRW<LocalTransform>, RefRW<ServerPosition>>()
                         .WithAll<Simulate>())
            {
                var position = ghost.ValueRO.Position;
                var velocity = ghost.ValueRO.Velocity;
                PlayerMotion.Step(ref position, ref velocity, input.ValueRO, dt, arena);
                ghost.ValueRW.Position = position;
                ghost.ValueRW.Velocity = velocity;
                transform.ValueRW.Position = position;
                if (isServer) serverPosition.ValueRW.Value = position;
            }
        }
    }
}
