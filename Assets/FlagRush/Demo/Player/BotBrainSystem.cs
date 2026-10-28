using Unity.Burst;
using Unity.Collections;
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
            state.RequireForUpdate<ActiveArena>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var arena = SystemAPI.GetSingleton<ActiveArena>();
            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.TickIndexForValidTick;

            if (arena.Match)
            {
                UpdateMatch(ref state, arena.Layout);
                return;
            }

            var bounds = ArenaBounds.Default;
            foreach (var (brain, input, ghost) in SystemAPI
                         .Query<RefRW<BotBrainState>, RefRW<PlayerInput>, RefRO<PlayerGhost>>()
                         .WithAll<Simulate>())
            {
                if (ghost.ValueRO.BotIndex < 0) continue;
                input.ValueRW = BotBrain.Think(ref brain.ValueRW, ghost.ValueRO.Position.xz, tick, bounds);
            }
        }

        /// <summary>Match levels: bots see every player and both flags, then drive the same PlayerInput humans use.</summary>
        void UpdateMatch(ref SystemState state, in ArenaLayout layout)
        {
            var views = new NativeList<PlayerView>(Allocator.Temp);
            foreach (var (ghost, combatant, instance) in SystemAPI
                         .Query<RefRO<PlayerGhost>, RefRO<Combatant>, RefRO<GhostInstance>>()
                         .WithAll<Simulate>())
                views.Add(MatchViews.ToView(ghost.ValueRO, combatant.ValueRO, instance.ValueRO, views.Length));

            FlagData red = default, blue = default;
            bool haveRed = false, haveBlue = false;
            foreach (var flag in SystemAPI.Query<RefRO<FlagGhost>>())
            {
                var f = MatchViews.ToData(flag.ValueRO);
                if (f.Team == 0) { red = f; haveRed = true; }
                else { blue = f; haveBlue = true; }
            }
            bool haveFlags = haveRed && haveBlue;

            var players = views.AsArray();
            foreach (var (brain, input, ghost, combatant, instance) in SystemAPI
                         .Query<RefRW<BotBrainState>, RefRW<PlayerInput>, RefRO<PlayerGhost>, RefRO<Combatant>, RefRO<GhostInstance>>()
                         .WithAll<Simulate>())
            {
                int botIndex = ghost.ValueRO.BotIndex;
                if (botIndex < 0) continue;
                var self = MatchViews.ToView(ghost.ValueRO, combatant.ValueRO, instance.ValueRO, players.Length);
                if (!self.Alive || !haveFlags)
                {
                    input.ValueRW = default;
                    continue;
                }
                var own = self.Team == 0 ? red : blue;
                var enemy = self.Team == 0 ? blue : red;
                input.ValueRW = BotBrain.ThinkMatch(ref brain.ValueRW, botIndex, self, own, enemy, players, layout);
            }
            views.Dispose();
        }
    }
}
