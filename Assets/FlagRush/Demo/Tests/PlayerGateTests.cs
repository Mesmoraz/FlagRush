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
            while (DemoStats.ServerTick - startTick < 60 && Time.realtimeSinceStartup < deadline)
            {
                var em = ClientWorld().EntityManager;
                var owned = DemoStats.OwnedPlayerEntity;
                if (owned != Entity.Null && em.Exists(owned))
                    em.SetComponentData(owned, new PlayerInput { Move = new float2(1, 0) });
                yield return null;
            }

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
