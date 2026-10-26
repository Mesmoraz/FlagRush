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
        public const int MaxBotChangesPerFrame = 50;
        public const float BotRingRadius = 20f;

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
                DemoStats.BotsOnServer = 0;
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

            ReconcileBots(ref state);
            DemoStats.PlayersOnServer = _players.CalculateEntityCount();
        }

        /// <summary>
        /// Add or remove unowned bot ghosts, at most <see cref="MaxBotChangesPerFrame"/> per frame, until the bot count
        /// matches <see cref="Sandbox.Bots"/>. Bots have <c>GhostOwner.NetworkId = -1</c> (no owner) and are never a
        /// connection's CommandTarget or in its LinkedEntityGroup; only entities with <c>BotIndex &gt;= 0</c> are ever destroyed here.
        /// </summary>
        void ReconcileBots(ref SystemState state)
        {
            var em = state.EntityManager;
            int want = math.clamp(Sandbox.Bots, 0, Sandbox.MaxBots);

            var bots = new NativeList<int2>(Allocator.Temp); // (BotIndex, list position) for sorting
            var botEntities = new NativeList<Entity>(Allocator.Temp);
            foreach (var (ghost, entity) in SystemAPI.Query<RefRO<PlayerGhost>>().WithEntityAccess())
            {
                if (ghost.ValueRO.BotIndex < 0) continue;
                bots.Add(new int2(ghost.ValueRO.BotIndex, botEntities.Length));
                botEntities.Add(entity);
            }

            int have = bots.Length;
            if (have < want)
            {
                var used = new NativeHashSet<int>(have, Allocator.Temp);
                foreach (var b in bots) used.Add(b.x);
                var prefab = SystemAPI.GetSingleton<PlayerGhostPrefab>().Value;
                int toAdd = math.min(want - have, MaxBotChangesPerFrame);
                int index = 0;
                for (int n = 0; n < toAdd; n++)
                {
                    while (used.Contains(index)) index++;
                    used.Add(index);
                    var spawn = BotSpawnPoint(index);
                    var bot = em.Instantiate(prefab);
                    // No owning connection. Netcode rejects NetworkId 0 on owner-predicted ghosts (GhostSendSystem
                    // logs an error) and documents -1 as "not owned by a player"; clients then interpolate it.
                    em.SetComponentData(bot, new GhostOwner { NetworkId = -1 });
                    em.SetComponentData(bot, new PlayerGhost { Position = spawn, BotIndex = index });
                    em.SetComponentData(bot, new ServerPosition { Value = spawn });
                    em.SetComponentData(bot, LocalTransform.FromPosition(spawn));
                    em.AddComponentData(bot, BotBrainState.Create(index));
                }
                used.Dispose();
                have += toAdd;
            }
            else if (have > want)
            {
                bots.Sort(new HighestBotIndexFirst());
                int toRemove = math.min(have - want, MaxBotChangesPerFrame);
                var doomed = new NativeArray<Entity>(toRemove, Allocator.Temp);
                for (int n = 0; n < toRemove; n++) doomed[n] = botEntities[bots[n].y];
                em.DestroyEntity(doomed);
                doomed.Dispose();
                have -= toRemove;
            }

            bots.Dispose();
            botEntities.Dispose();
            DemoStats.BotsOnServer = have;
        }

        /// <summary>Bot spawn: the spawn-ring slot for <c>16 + index</c>, pushed out to a 20 m ring.</summary>
        static float3 BotSpawnPoint(int index) => math.normalize(PlayerSpawn.Point(PlayerSpawn.Slots + index)) * BotRingRadius;

        struct HighestBotIndexFirst : System.Collections.Generic.IComparer<int2>
        {
            public int Compare(int2 a, int2 b) => b.x.CompareTo(a.x);
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
