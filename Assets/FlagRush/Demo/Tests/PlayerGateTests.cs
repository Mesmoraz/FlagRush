using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
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
            Sandbox.PredictionEnabled = true;
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

        [UnityTest]
        public IEnumerator PredictionLeadsServerUnderLatency()
        {
            yield return Until(OnePlayerEachSide, 10f);
            Assert.That(OnePlayerEachSide(), Is.True, "no player ghost");
            Sandbox.SetLink(100, 0, 0);
            yield return Until(() => DemoStats.EstimatedRttMs > 150f, 20f);
            Assert.That(DemoStats.EstimatedRttMs, Is.GreaterThan(150f), "link emulation did not raise RTT");
            yield return Until(OwnedIsPredicted, 3f);
            Assert.That(OwnedIsPredicted(), Is.True, "owned ghost is not predicted");

            var em = ClientWorld().EntityManager;
            var owned = DemoStats.OwnedPlayerEntity;
            float localStart = em.GetComponentData<LocalTransform>(owned).Position.x;
            float serverStart = em.GetComponentData<ServerPosition>(owned).Value.x;

            PlayerInputGatherSystem.TestOverride = new PlayerInput { Move = new float2(1, 0) };
            float local = localStart, server = serverStart;
            for (int frame = 0; frame < 2 && local <= localStart; frame++)
            {
                yield return null;
                local = em.GetComponentData<LocalTransform>(owned).Position.x;
                server = em.GetComponentData<ServerPosition>(owned).Value.x;
            }
            Debug.Log($"[PlayerGate] local x {localStart:F5} -> {local:F5}, server x {serverStart:F5} -> {server:F5}, " +
                      $"rtt {DemoStats.EstimatedRttMs:F0} ms, command age {DemoStats.CommandAgeTicks} ticks ({DemoStats.CommandAgeMs:F0} ms), " +
                      $"ticks/frame {DemoStats.PredictedTicksPerFrame}, rollbacks/s {DemoStats.RollbacksPerSecond:F1}, misprediction {DemoStats.MispredictionMeters:F3} m");
            Assert.That(local, Is.GreaterThan(localStart), "prediction did not move the owned ghost within 2 frames");
            Assert.That(server, Is.EqualTo(serverStart), "server position changed before the input could reach the server");
            Assert.That(DemoStats.CommandAgeTicks, Is.GreaterThanOrEqualTo(6), "command age under 100 ms one-way latency");
        }

        [UnityTest]
        public IEnumerator TogglingPredictionSwitchesGhostMode()
        {
            yield return Until(OwnedIsPredicted, 10f);
            Assert.That(OwnedIsPredicted(), Is.True, "owned ghost not predicted at start");

            Sandbox.PredictionEnabled = false;
            yield return Until(OwnedIsInterpolated, 3f);
            Assert.That(OwnedIsInterpolated(), Is.True, "owned ghost still predicted 3 s after turning prediction off");
            yield return null;
            Assert.That(DemoStats.OwnedPlayerPredicted, Is.False, "OwnedPlayerPredicted readout");
            Assert.That(DemoStats.MispredictionMeters, Is.EqualTo(0f), "misprediction must read 0 with prediction off");

            // Inputs keep flowing while interpolated: the server still moves the ghost.
            float startX = ServerPlayerPosition().x;
            uint startTick = DemoStats.ServerTick;
            float deadline = Time.realtimeSinceStartup + 5f;
            PlayerInputGatherSystem.TestOverride = new PlayerInput { Move = new float2(1, 0) };
            while (DemoStats.ServerTick - startTick < 30 && Time.realtimeSinceStartup < deadline)
                yield return null;
            PlayerInputGatherSystem.TestOverride = null;
            Assert.That(ServerPlayerPosition().x - startX, Is.GreaterThan(0.5f), "input did not reach the server while interpolated");
            Assert.That(OwnedIsInterpolated(), Is.True, "owned ghost left interpolated mode while prediction is off");

            Sandbox.PredictionEnabled = true;
            yield return Until(OwnedIsPredicted, 3f);
            Assert.That(OwnedIsPredicted(), Is.True, "owned ghost still interpolated 3 s after turning prediction on");
        }

        [UnityTest]
        public IEnumerator RapidToggleConvergesToCheckbox()
        {
            yield return Until(OwnedIsPredicted, 10f);
            Assert.That(OwnedIsPredicted(), Is.True, "owned ghost not predicted at start");

            Sandbox.PredictionEnabled = false;
            yield return null;
            Sandbox.PredictionEnabled = true;
            yield return null;
            Sandbox.PredictionEnabled = false;

            yield return Until(OwnedIsInterpolated, 3f);
            Assert.That(OwnedIsInterpolated(), Is.True, "owned ghost did not converge to interpolated within 3 s");
            float until = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < until)
            {
                Assert.That(OwnedIsInterpolated(), Is.True, "owned ghost left interpolated mode after converging");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Level3DrawsPlayersAndOutline()
        {
            Sandbox.Bots = 5;
            const int expected = 1 + 5 + 1; // owned + bots + server outline; no agents or probe ghosts at level 3
            yield return Until(() => DemoStats.PlayersOnClient == 6 && OwnedIsPredicted() && DemoStats.DrawnInstances == expected, 10f);
            Debug.Log($"[PlayerGate] drawn={DemoStats.DrawnInstances} draws={DemoStats.DrawCalls} players={DemoStats.PlayersOnClient} agents={DemoStats.SwarmCount} ghosts={DemoStats.GhostsOnClient}");
            Assert.That(DemoStats.DrawnInstances, Is.EqualTo(expected), "owned + 5 bots + outline");
        }

        [UnityTest]
        public IEnumerator OutlineHiddenWhenPredictionOff()
        {
            Sandbox.Bots = 5;
            yield return Until(() => DemoStats.PlayersOnClient == 6 && OwnedIsPredicted(), 10f);
            Sandbox.PredictionEnabled = false;
            const int expected = 1 + 5; // owned + bots, no outline
            yield return Until(() => OwnedIsInterpolated() && DemoStats.DrawnInstances == expected, 10f);
            Debug.Log($"[PlayerGate] prediction off: drawn={DemoStats.DrawnInstances} draws={DemoStats.DrawCalls}");
            Assert.That(DemoStats.DrawnInstances, Is.EqualTo(expected), "outline must not be drawn with prediction off");
        }

        [UnityTest]
        public IEnumerator CameraFollowsOwnedGhost()
        {
            yield return Until(OnePlayerEachSide, 10f);
            Assert.That(OnePlayerEachSide(), Is.True, "no player ghost to follow");
            yield return new WaitForSeconds(0.5f); // let the camera settle on the ghost
            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "no main camera");
            float startX = camera.transform.position.x;

            PlayerInputGatherSystem.TestOverride = new PlayerInput { Move = new float2(1, 0) };
            yield return new WaitForSeconds(2f);
            PlayerInputGatherSystem.TestOverride = null;

            float endX = camera.transform.position.x;
            Debug.Log($"[PlayerGate] camera x {startX:F2} -> {endX:F2}, camera pos {camera.transform.position}");
            Assert.That(endX - startX, Is.GreaterThan(2f), "camera did not follow the owned ghost");
        }

        static bool OwnedIsPredicted() => OwnedHas(true);
        static bool OwnedIsInterpolated() => OwnedHas(false);

        static bool OwnedHas(bool predicted)
        {
            var world = ClientWorld();
            var owned = DemoStats.OwnedPlayerEntity;
            if (world == null || owned == Entity.Null || !world.EntityManager.Exists(owned)) return false;
            return world.EntityManager.HasComponent<PredictedGhost>(owned) == predicted;
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
