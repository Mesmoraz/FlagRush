using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlagRush.Demo.Tests
{
    /// <summary>Level 3 gate: one owner-predicted player ghost per connection, moved by its owner's input.</summary>
    public class PlayerGateTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DemoConfig.OverrideLevel(3);
            Sandbox.Bots = 0;
            SceneManager.LoadScene("SampleScene");
            yield return Until(() => DemoStats.ClientInGame, 30f);
            Assert.That(DemoStats.ClientInGame, Is.True, "client never went in-game");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DemoConfig.OverrideLevel(null);
            PlayerInputGatherSystem.TestOverride = null;
            Sandbox.Bots = 0;
            Sandbox.SetLink(0, 0, 0);
            // Let the server despawn the level-3 players before the next test runs.
            yield return Until(() => DemoStats.PlayersOnServer == 0 && DemoStats.PlayersOnClient == 0, 10f);
            Assert.That(DemoStats.PlayersOnServer, Is.EqualTo(0), "players not despawned on the server below Level 3");
            Assert.That(DemoStats.PlayersOnClient, Is.EqualTo(0), "players not despawned on the client below Level 3");
        }

        [UnityTest]
        public IEnumerator OneOwnedPredictedGhostPerConnection()
        {
            yield return Until(OnePlayerEachSide, 10f);
            Assert.That(DemoStats.PlayersOnServer, Is.EqualTo(1), "server player count");
            Assert.That(DemoStats.PlayersOnClient, Is.EqualTo(1), "client player count");

            var em = ClientWorld().EntityManager;
            var owned = DemoStats.OwnedPlayerEntity;
            Assert.That(owned != Entity.Null && em.Exists(owned), Is.True, "client has no owned player entity");
            Assert.That(em.HasComponent<PredictedGhost>(owned), Is.True, "owned ghost is not predicted");
            Assert.That(DemoStats.OwnedPlayerPredicted, Is.True, "OwnedPlayerPredicted readout");
            Assert.That(em.HasComponent<GhostOwnerIsLocal>(owned) && em.IsComponentEnabled<GhostOwnerIsLocal>(owned), Is.True,
                "GhostOwnerIsLocal not enabled on the owned ghost");
        }

        [UnityTest]
        public IEnumerator ScriptedInputMovesGhostOnServer()
        {
            yield return Until(OnePlayerEachSide, 10f);
            Assert.That(OnePlayerEachSide(), Is.True, "no player ghost to drive");

            float startX = ServerPlayerPosition().x;
            uint startTick = DemoStats.ServerTick;
            float deadline = Time.realtimeSinceStartup + 10f;
            PlayerInputGatherSystem.TestOverride = new PlayerInput { Move = new float2(1, 0) };
            while (DemoStats.ServerTick - startTick < 60 && Time.realtimeSinceStartup < deadline)
                yield return null;
            PlayerInputGatherSystem.TestOverride = null;

            float endX = ServerPlayerPosition().x;
            Debug.Log($"[PlayerGate] server x {startX:F2} -> {endX:F2} over {DemoStats.ServerTick - startTick} ticks");
            Assert.That(DemoStats.ServerTick - startTick, Is.GreaterThanOrEqualTo(60u), "server did not tick 60 times");
            Assert.That(endX - startX, Is.GreaterThan(2f), "scripted input did not move the server ghost");
        }

        [UnityTest]
        public IEnumerator ReconnectReplacesOwnedGhost()
        {
            yield return Until(OnePlayerEachSide, 10f);
            Assert.That(OnePlayerEachSide(), Is.True, "no player ghost before reconnect");
            int oldGhostId = OwnedGhostId();

            Sandbox.KillConnectionRequested = true;
            yield return Until(() => !DemoStats.ClientConnected, 5f);
            yield return Until(() => DemoStats.ClientInGame && OnePlayerEachSide() && OwnedGhostId() != oldGhostId, 20f);

            Assert.That(DemoStats.PlayersOnServer, Is.EqualTo(1), "server player count after reconnect");
            Assert.That(DemoStats.PlayersOnClient, Is.EqualTo(1), "client player count after reconnect");
            Assert.That(OwnedGhostId(), Is.Not.EqualTo(oldGhostId), "owned ghost was not replaced");
            Assert.That(OwnedGhostId(), Is.Not.EqualTo(0), "no owned ghost after reconnect");
        }

        [UnityTest]
        public IEnumerator BotsReconcileWithoutTouchingHumans()
        {
            yield return Until(OnePlayerEachSide, 10f);
            Assert.That(OnePlayerEachSide(), Is.True, "no human player before adding bots");
            int humanGhostId = OwnedGhostId();

            Sandbox.Bots = 20;
            yield return Until(() => DemoStats.PlayersOnClient == 21, 15f);
            Assert.That(DemoStats.PlayersOnClient, Is.EqualTo(21), "client player count with 20 bots");
            Assert.That(DemoStats.BotsOnServer, Is.EqualTo(20), "BotsOnServer with 20 bots");
            Assert.That(OwnedGhostId(), Is.EqualTo(humanGhostId), "human ghost changed while adding bots");

            Sandbox.Bots = 0;
            yield return Until(() => DemoStats.PlayersOnClient == 1, 15f);
            Assert.That(DemoStats.PlayersOnClient, Is.EqualTo(1), "client player count after removing bots");
            Assert.That(DemoStats.BotsOnServer, Is.EqualTo(0), "BotsOnServer after removing bots");
            Assert.That(OwnedGhostId(), Is.EqualTo(humanGhostId), "human ghost was touched by bot reconcile");

            Sandbox.Bots = 5;
            yield return Until(() => DemoStats.PlayersOnClient == 6, 15f);
            Assert.That(DemoStats.PlayersOnClient, Is.EqualTo(6), "client player count with 5 bots");
            Assert.That(OwnedGhostId(), Is.EqualTo(humanGhostId), "human ghost changed with 5 bots");
        }

        [UnityTest]
        public IEnumerator BotsMove()
        {
            yield return Until(OnePlayerEachSide, 10f);
            Sandbox.Bots = 5;
            yield return Until(() => DemoStats.BotsOnServer == 5, 10f);
            Assert.That(DemoStats.BotsOnServer, Is.EqualTo(5), "bots not spawned");
            yield return null;

            float3 start = SumBotServerPositions();
            uint startTick = DemoStats.ServerTick;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (DemoStats.ServerTick - startTick < 60 && Time.realtimeSinceStartup < deadline)
                yield return null;
            float3 end = SumBotServerPositions();

            Debug.Log($"[PlayerGate] bot position sum {start} -> {end} over {DemoStats.ServerTick - startTick} ticks");
            Assert.That(DemoStats.ServerTick - startTick, Is.GreaterThanOrEqualTo(60u), "server did not tick 60 times");
            Assert.That(math.distance(start, end), Is.GreaterThan(0.5f), "bots did not move");
        }

        static float3 SumBotServerPositions()
        {
            var em = ServerWorld().EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>(), ComponentType.ReadOnly<ServerPosition>());
            using var ghosts = query.ToComponentDataArray<PlayerGhost>(Unity.Collections.Allocator.Temp);
            using var positions = query.ToComponentDataArray<ServerPosition>(Unity.Collections.Allocator.Temp);
            float3 sum = float3.zero;
            for (int i = 0; i < ghosts.Length; i++)
                if (ghosts[i].BotIndex >= 0) sum += positions[i].Value;
            return sum;
        }

        static bool OnePlayerEachSide()
        {
            if (DemoStats.PlayersOnServer != 1 || DemoStats.PlayersOnClient != 1) return false;
            var world = ClientWorld();
            var owned = DemoStats.OwnedPlayerEntity;
            return world != null && owned != Entity.Null && world.EntityManager.Exists(owned);
        }

        static int OwnedGhostId()
        {
            var world = ClientWorld();
            var owned = DemoStats.OwnedPlayerEntity;
            if (world == null || owned == Entity.Null || !world.EntityManager.Exists(owned)) return 0;
            return world.EntityManager.GetComponentData<GhostInstance>(owned).ghostId;
        }

        static float3 ServerPlayerPosition()
        {
            var em = ServerWorld().EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>());
            return query.GetSingleton<PlayerGhost>().Position;
        }

        static World ClientWorld()
        {
            foreach (var world in World.All)
                if (world.IsClient() && !world.IsThinClient()) return world;
            return null;
        }

        static World ServerWorld()
        {
            foreach (var world in World.All)
                if (world.IsServer()) return world;
            return null;
        }

        static IEnumerator Until(System.Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline && !condition()) yield return null;
        }
    }
}
