using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace FlagRush.Demo
{
    /// <summary>Spawn points: a ring of 16 slots, 10 m out, on the ground.</summary>
    public static class PlayerSpawn
    {
        public const int Slots = 16;
        public const float Radius = 10f;

        public static float3 Point(int slot)
        {
            int i = ((slot % Slots) + Slots) % Slots;
            float a = i * (math.PI * 2f / Slots);
            return new float3(math.cos(a) * Radius, 0f, math.sin(a) * Radius);
        }
    }

    /// <summary>
    /// Server: gives every in-game connection one owner-predicted player ghost. The ghost is the
    /// connection's <see cref="CommandTarget"/> and lives in its <see cref="LinkedEntityGroup"/>, so a
    /// disconnect destroys it. Below Level 3 any leftover players are despawned (the worlds outlive
    /// PlayMode tests, and Level 1-2 must not see extra ghosts).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GoInGameSystem))]
    public partial struct PlayerSpawnSystem : ISystem
    {
        EntityQuery _players;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerGhostPrefab>();
            _players = state.GetEntityQuery(ComponentType.ReadOnly<PlayerGhost>());
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!DemoConfig.IsPlayerLevel)
            {
                if (!_players.IsEmpty) DespawnAll(ref state);
                DemoStats.PlayersOnServer = 0;
                return;
            }

            var needPlayer = new NativeList<Entity>(Allocator.Temp);
            foreach (var (target, entity) in SystemAPI.Query<RefRO<CommandTarget>>()
                         .WithAll<NetworkStreamInGame, NetworkId>().WithEntityAccess())
            {
                var current = target.ValueRO.targetEntity;
                if (current == Entity.Null || !state.EntityManager.Exists(current)) needPlayer.Add(entity);
            }

            if (needPlayer.Length > 0)
            {
                var em = state.EntityManager;
                var prefab = SystemAPI.GetSingleton<PlayerGhostPrefab>().Value;
                foreach (var connection in needPlayer)
                {
                    int networkId = em.GetComponentData<NetworkId>(connection).Value;
                    var spawn = PlayerSpawn.Point(networkId);
                    var player = em.Instantiate(prefab);
                    em.SetComponentData(player, new GhostOwner { NetworkId = networkId });
                    em.SetComponentData(player, new PlayerGhost { Position = spawn, BotIndex = -1 });
                    em.SetComponentData(player, new ServerPosition { Value = spawn });
                    em.SetComponentData(player, LocalTransform.FromPosition(spawn));
                    em.SetComponentData(connection, new CommandTarget { targetEntity = player });
                    em.GetBuffer<LinkedEntityGroup>(connection).Add(new LinkedEntityGroup { Value = player });
                }
            }
            needPlayer.Dispose();

            DemoStats.PlayersOnServer = _players.CalculateEntityCount();
        }

        /// <summary>Detach every player from its connection (CommandTarget + LinkedEntityGroup), then destroy them.</summary>
        void DespawnAll(ref SystemState state)
        {
            var em = state.EntityManager;
            foreach (var (target, group) in SystemAPI.Query<RefRW<CommandTarget>, DynamicBuffer<LinkedEntityGroup>>())
            {
                var player = target.ValueRO.targetEntity;
                if (player == Entity.Null || !SystemAPI.HasComponent<PlayerGhost>(player)) continue;
                target.ValueRW.targetEntity = Entity.Null;
                for (int i = group.Length - 1; i >= 0; i--)
                    if (group[i].Value == player) group.RemoveAt(i);
            }
            em.DestroyEntity(_players);
        }
    }

    /// <summary>
    /// Client: counts player ghosts, finds the one this client owns, and points the client connection's
    /// <see cref="CommandTarget"/> at it so its input buffer is sent to the server. Also drives
    /// interpolated (non-owned) players' transforms from the replicated position.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GoInGameSystem))]
    public partial struct PlayerStatsSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            int players = 0;
            foreach (var (ghost, transform) in SystemAPI.Query<RefRO<PlayerGhost>, RefRW<LocalTransform>>())
            {
                players++;
                transform.ValueRW.Position = ghost.ValueRO.Position;
            }
            DemoStats.PlayersOnClient = players;

            var owned = Entity.Null;
            foreach (var (_, entity) in SystemAPI.Query<RefRO<PlayerGhost>>().WithAll<GhostOwnerIsLocal>().WithEntityAccess())
                owned = entity;
            DemoStats.OwnedPlayerEntity = owned;
            DemoStats.OwnedPlayerPredicted = owned != Entity.Null && SystemAPI.HasComponent<PredictedGhost>(owned);

            foreach (var target in SystemAPI.Query<RefRW<CommandTarget>>().WithAll<NetworkId>())
            {
                var current = target.ValueRO.targetEntity;
                if (owned != Entity.Null ? current != owned : (current != Entity.Null && !state.EntityManager.Exists(current)))
                    target.ValueRW.targetEntity = owned;
            }
        }
    }
}
