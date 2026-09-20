using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace FlagRush.Spike
{
    /// <summary>
    /// Builds the ghost prefab in code, identically in the client and server worlds, so the spike needs
    /// no baked ghost prefab (keeps the SubScene test independent from the netcode test).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [CreateAfter(typeof(DefaultVariantSystemGroup))]
    public partial class SpikeGhostPrefabSystem : SystemBase
    {
        protected override void OnCreate()
        {
            var em = EntityManager;
            var prefab = em.CreateEntity(typeof(SpikeGhostState), typeof(LocalTransform));
            em.SetComponentData(prefab, LocalTransform.Identity);
            em.SetName(prefab, "SpikeGhostPrefab");

            GhostPrefabCreation.ConvertToGhostPrefab(em, prefab, new GhostPrefabCreation.Config
            {
                Name = "SpikeGhost",
                Importance = 1000,
                SupportedGhostModes = GhostModeMask.Interpolated,
                DefaultGhostMode = GhostMode.Interpolated,
                OptimizationMode = GhostOptimizationMode.Dynamic,
            });

            var holder = em.CreateEntity(typeof(SpikeGhostPrefab));
            em.SetComponentData(holder, new SpikeGhostPrefab { Value = prefab });
            Debug.Log($"[Spike] ghost prefab created in {World.Name}");
            Enabled = false;
        }

        protected override void OnUpdate() { }
    }
}
