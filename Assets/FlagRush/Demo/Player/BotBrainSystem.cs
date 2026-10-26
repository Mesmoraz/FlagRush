using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace FlagRush.Demo
{
    /// <summary>
    /// Server: writes each bot's <see cref="PlayerInput"/> from <see cref="BotBrain.Think"/>, so bots then move
    /// through the same <see cref="PlayerMovementSystem"/> as humans.
    /// </summary>
    /// <remarks>
    /// Ordering matters. Server ghosts carry <see cref="PredictedGhost"/>, so Netcode's generated
    /// ApplyCurrentInputBufferElementToInputDataSystem (OrderFirst in CopyCommandBufferToInputSystemGroup, itself
    /// OrderFirst in PredictedSimulationSystemGroup) overwrites every bot's PlayerInput from its (empty) input buffer
    /// each tick. This system runs after that group and before movement, so the brain's value is what movement reads.
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(CopyCommandBufferToInputSystemGroup))]
    [UpdateBefore(typeof(PlayerMovementSystem))]
    [BurstCompile]
    public partial struct BotBrainSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BotBrainState>();
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.TickIndexForValidTick;
            var arena = ArenaBounds.Default;

            foreach (var (brain, input, ghost) in SystemAPI
                         .Query<RefRW<BotBrainState>, RefRW<PlayerInput>, RefRO<PlayerGhost>>()
                         .WithAll<Simulate>())
            {
                if (ghost.ValueRO.BotIndex < 0) continue;
                input.ValueRW = BotBrain.Think(ref brain.ValueRW, ghost.ValueRO.Position.xz, tick, arena);
            }
        }
    }
}
