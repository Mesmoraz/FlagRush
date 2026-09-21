using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace FlagRush.Demo
{
    /// <summary>
    /// Builds the ghost prefab in code, identically in the client and server worlds, so the spike needs
    /// no baked ghost prefab (keeps the SubScene test independent from the netcode test).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [CreateAfter(typeof(DefaultVariantSystemGroup))]
    public partial class ProbeGhostPrefabSystem : SystemBase
    {
        protected override void OnCreate()
        {
            var em = EntityManager;
            var prefab = em.CreateEntity(typeof(ProbeGhost), typeof(LocalTransform), typeof(LocalToWorld));
            em.SetComponentData(prefab, LocalTransform.Identity);
            em.SetName(prefab, "ProbeGhostPrefab");

            GhostPrefabCreation.ConvertToGhostPrefab(em, prefab, new GhostPrefabCreation.Config
            {
                Name = "ProbeGhost",
                Importance = 1000,
                SupportedGhostModes = GhostModeMask.Interpolated,
                DefaultGhostMode = GhostMode.Interpolated,
                OptimizationMode = GhostOptimizationMode.Dynamic,
            });

            var holder = em.CreateEntity(typeof(ProbeGhostPrefab));
            em.SetComponentData(holder, new ProbeGhostPrefab { Value = prefab });
            Debug.Log($"[Demo] ghost prefab created in {World.Name}");
            Enabled = false;
        }

        protected override void OnUpdate() { }
    }
}
