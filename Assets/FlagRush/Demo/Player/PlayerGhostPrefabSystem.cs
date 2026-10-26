using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace FlagRush.Demo
{
    /// <summary>
    /// Builds the player ghost prefab (predicted by its owner, interpolated elsewhere) in code, identically in the client and server worlds
    /// (same pattern as <see cref="ProbeGhostPrefabSystem"/>). Created at every level so both worlds always
    /// agree on the ghost collection; only <see cref="PlayerSpawnSystem"/> is gated on the level.
    /// </summary>
    /// <remarks>
    /// Baking normally adds two things that <see cref="GhostPrefabCreation.ConvertToGhostPrefab"/> does not:
    /// the <c>InputBufferData&lt;PlayerInput&gt;</c> command buffer (GhostInputBufferBakingSystem) and
    /// <see cref="GhostOwnerIsLocal"/> (GhostAuthoringComponentBaker, HasOwner). Both are added here explicitly.
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [CreateAfter(typeof(DefaultVariantSystemGroup))]
    public partial class PlayerGhostPrefabSystem : SystemBase
    {
        protected override void OnCreate()
        {
            var em = EntityManager;
            var prefab = em.CreateEntity(
                typeof(PlayerGhost), typeof(ServerPosition), typeof(PlayerInput), typeof(InputBufferData<PlayerInput>),
                typeof(GhostOwner), typeof(GhostOwnerIsLocal), typeof(LocalTransform), typeof(LocalToWorld));
            em.SetComponentData(prefab, LocalTransform.Identity);
            em.SetComponentData(prefab, new PlayerGhost { BotIndex = -1 });
            em.SetName(prefab, "PlayerGhostPrefab");

            GhostPrefabCreation.ConvertToGhostPrefab(em, prefab, new GhostPrefabCreation.Config
            {
                Name = "PlayerGhost",
                Importance = 2000,
                SupportedGhostModes = GhostModeMask.All,
                // Not OwnerPredicted: Netcode cannot prediction-switch OwnerPredicted ghosts on demand. The owner's
                // client spawns its own ghost predicted (PlayerSpawnClassificationSystem); everyone else interpolates.
                DefaultGhostMode = GhostMode.Interpolated,
                OptimizationMode = GhostOptimizationMode.Dynamic,
            });

            var holder = em.CreateEntity(typeof(PlayerGhostPrefab));
            em.SetComponentData(holder, new PlayerGhostPrefab { Value = prefab });
            Debug.Log($"[Demo] player ghost prefab created in {World.Name}");
            Enabled = false;
        }

        protected override void OnUpdate() { }
    }
}
