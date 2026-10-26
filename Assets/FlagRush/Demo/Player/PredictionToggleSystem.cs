using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.LowLevel;

namespace FlagRush.Demo
{
    /// <summary>
    /// Client: keeps the owned player ghost's mode equal to <see cref="Sandbox.PredictionEnabled"/> using Netcode
    /// prediction switching. Every frame, if the mode (presence of <see cref="PredictedGhost"/>) differs from the
    /// checkbox and no switch is in progress (<see cref="SwitchPredictionSmoothing"/>), it queues one conversion.
    /// Re-checking every frame is what makes rapid toggling converge on the final checkbox value.
    /// </summary>
    /// <remarks>
    /// Netcode refuses to switch <c>OwnerPredicted</c> ghosts through these queues (GhostPredictionSwitchingSystem logs
    /// "owner predicted ghosts cannot be switched on demand"), so the player prefab defaults to Interpolated and
    /// <see cref="PlayerSpawnClassificationSystem"/> spawns the owned ghost predicted. Inputs keep flowing while it is
    /// interpolated: the input buffer is present in both modes and the CommandTarget is unchanged.
    /// Runs after <see cref="GhostSimulationSystemGroup"/> so a queued entry is consumed (next frame) before this
    /// system looks again, and never queues a duplicate.
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GhostSimulationSystemGroup))]
    public partial class PredictionToggleSystem : SystemBase
    {
        public const float TransitionDurationSeconds = 0.25f;

        protected override void OnCreate()
        {
            RequireForUpdate<GhostPredictionSwitchingQueues>();
            RequireForUpdate<PlayerGhost>();
        }

        protected override void OnUpdate()
        {
            if (!DemoConfig.IsPlayerLevel) return;

            var owned = Entity.Null;
            foreach (var (_, entity) in SystemAPI.Query<RefRO<PlayerGhost>>().WithAll<GhostOwnerIsLocal, GhostInstance>().WithEntityAccess())
                owned = entity;
            if (owned == Entity.Null) return;
            if (SystemAPI.HasComponent<SwitchPredictionSmoothing>(owned)) return;

            bool predicted = SystemAPI.HasComponent<PredictedGhost>(owned);
            bool want = Sandbox.PredictionEnabled;
            if (predicted == want) return;

            var queues = SystemAPI.GetSingleton<GhostPredictionSwitchingQueues>(); // parallel writers: enqueue through a copy
            var entry = new ConvertPredictionEntry { TargetEntity = owned, TransitionDurationSeconds = TransitionDurationSeconds };
            if (want) queues.ConvertToPredictedQueue.Enqueue(entry);
            else queues.ConvertToInterpolatedQueue.Enqueue(entry);
        }
    }

    /// <summary>
    /// Client: spawns this client's own player ghost predicted (when <see cref="Sandbox.PredictionEnabled"/>), the
    /// owner-predicted behaviour the prefab gave up so that <see cref="PredictionToggleSystem"/> can switch it.
    /// Every other player (bots, other humans) falls through to the prefab default, Interpolated.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostSpawnClassificationSystemGroup))]
    [UpdateBefore(typeof(GhostSpawnClassificationSystem))]
    [CreateAfter(typeof(GhostCollectionSystem))]
    [CreateAfter(typeof(GhostReceiveSystem))]
    public partial struct PlayerSpawnClassificationSystem : ISystem
    {
        SnapshotDataLookupHelper _lookupHelper;

        public void OnCreate(ref SystemState state)
        {
            _lookupHelper = new SnapshotDataLookupHelper(ref state,
                SystemAPI.GetSingletonEntity<GhostCollection>(),
                SystemAPI.GetSingletonEntity<SpawnedGhostEntityMap>());
            state.RequireForUpdate<NetworkId>();
            state.RequireForUpdate<GhostCollection>();
            state.RequireForUpdate<GhostSpawnQueue>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!Sandbox.PredictionEnabled) return;
            state.CompleteDependency();
            _lookupHelper.Update(ref state);
            var lookup = _lookupHelper.CreateSnapshotBufferLookup();
            int localId = SystemAPI.GetSingleton<NetworkId>().Value;

            foreach (var (ghosts, data) in SystemAPI.Query<DynamicBuffer<GhostSpawnBuffer>, DynamicBuffer<SnapshotDataBuffer>>()
                         .WithAll<GhostSpawnQueue>())
            {
                for (int i = 0; i < ghosts.Length; i++)
                {
                    ref var ghost = ref ghosts.ElementAt(i);
                    if (ghost.SpawnType != GhostSpawnBuffer.Type.Unknown) continue;
                    if (!lookup.HasComponent<PlayerGhost>(ghost.GhostType) || !lookup.HasGhostOwner(ghost)) continue;
                    if (lookup.GetGhostOwner(ghost, data) == localId)
                        ghost.SpawnType = GhostSpawnBuffer.Type.Predicted;
                }
            }
        }
    }
}
