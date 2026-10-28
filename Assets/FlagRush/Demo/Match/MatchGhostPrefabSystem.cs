using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace FlagRush.Demo
{
    /// <summary>Per-world singleton pointing at the runtime-created flag and match-state ghost prefabs.</summary>
    public struct MatchGhostPrefabs : IComponentData
    {
        public Entity Flag;
        public Entity State;
    }

    /// <summary>
    /// Builds the flag and match-state ghost prefabs in code, identically in the client and server worlds (same pattern as
    /// <see cref="PlayerGhostPrefabSystem"/>). Created at every level so both worlds always agree on the ghost collection;
    /// only <see cref="MatchSpawnSystem"/> is gated on the level. Both ghosts are interpolated only.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [CreateAfter(typeof(DefaultVariantSystemGroup))]
    public partial class MatchGhostPrefabSystem : SystemBase
    {
        protected override void OnCreate()
        {
            var em = EntityManager;

            var flag = em.CreateEntity(typeof(FlagGhost), typeof(LocalTransform), typeof(LocalToWorld));
            em.SetComponentData(flag, LocalTransform.Identity);
            em.SetName(flag, "FlagGhostPrefab");

            // LocalTransform is not replicated: FlagGhost.Position already carries the position and the client derives
            // LocalTransform from it (MatchClientStatsSystem).
            using var overrides = new NativeParallelHashMap<GhostPrefabCreation.Component, GhostPrefabCreation.ComponentOverride>(1, Allocator.Temp);
            overrides.Add(new GhostPrefabCreation.Component { ComponentType = ComponentType.ReadWrite<LocalTransform>(), ChildIndex = 0 },
                new GhostPrefabCreation.ComponentOverride
                {
                    OverrideType = GhostPrefabCreation.ComponentOverrideType.Variant,
                    Variant = GhostVariantsUtility.DontSerializeHash,
                });
            GhostPrefabCreation.ConvertToGhostPrefab(em, flag, new GhostPrefabCreation.Config
            {
                Name = "FlagGhost",
                Importance = 3000,
                SupportedGhostModes = GhostModeMask.Interpolated,
                DefaultGhostMode = GhostMode.Interpolated,
                OptimizationMode = GhostOptimizationMode.Dynamic,
            }, overrides);

            var state = em.CreateEntity(typeof(MatchStateGhost));
            em.SetName(state, "MatchStateGhostPrefab");
            GhostPrefabCreation.ConvertToGhostPrefab(em, state, new GhostPrefabCreation.Config
            {
                Name = "MatchStateGhost",
                Importance = 3000,
                SupportedGhostModes = GhostModeMask.Interpolated,
                DefaultGhostMode = GhostMode.Interpolated,
                OptimizationMode = GhostOptimizationMode.Dynamic,
            });

            var holder = em.CreateEntity(typeof(MatchGhostPrefabs));
            em.SetComponentData(holder, new MatchGhostPrefabs { Flag = flag, State = state });
            Debug.Log($"[Demo] match ghost prefabs created in {World.Name}");
            Enabled = false;
        }

        protected override void OnUpdate() { }
    }
}
