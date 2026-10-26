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
    /// <summary>
    /// Level 3 gate: one player ghost per connection, predicted on its owner's client and interpolated everywhere
    /// else (the prefab defaults to Interpolated; the owner's spawn is classified predicted), moved by its owner's input.
    /// </summary>
    public class PlayerGateTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DemoConfig.OverrideLevel(3);
            Sandbox.Bots = 0;
            DemoHud.Collapsed = false;
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
            DemoHud.Collapsed = false;
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

            // Spec: the predicted ghost moves within 2 ticks while the server copy cannot have moved yet (the input
            // needs half a round trip, ~6 ticks, just to reach the server). Count client ticks, not frames: at a high
            // frame rate two frames can fall inside a single tick.
            uint startTick = ClientNetworkTick();
            PlayerInputGatherSystem.TestOverride = new PlayerInput { Move = new float2(1, 0) };
            float tickDeadline = Time.realtimeSinceStartup + 1f;
            while (ClientNetworkTick() - startTick < 2 && Time.realtimeSinceStartup < tickDeadline)
                yield return null;
            uint ticks = ClientNetworkTick() - startTick;
            float local = em.GetComponentData<LocalTransform>(owned).Position.x;
            float server = em.GetComponentData<ServerPosition>(owned).Value.x;
            Debug.Log($"[PlayerGate] local x {localStart:F5} -> {local:F5}, server x {serverStart:F5} -> {server:F5}, " +
                      $"rtt {DemoStats.EstimatedRttMs:F0} ms, command age {DemoStats.CommandAgeTicks} ticks ({DemoStats.CommandAgeMs:F0} ms), " +
                      $"ticks/frame {DemoStats.PredictedTicksPerFrame}, rollbacks/s {DemoStats.RollbacksPerSecond:F1}, misprediction {DemoStats.MispredictionMeters:F3} m");
            Assert.That(ticks, Is.GreaterThanOrEqualTo(2u), "client did not advance 2 ticks within 1 s");
            Assert.That(local, Is.GreaterThan(localStart), "prediction did not move the owned ghost within 2 ticks");
            Assert.That(server, Is.EqualTo(serverStart), "server position changed before the input could reach the server");
            Assert.That(DemoStats.CommandAgeTicks, Is.GreaterThanOrEqualTo(6), "command age under 100 ms one-way latency");
        }

        [UnityTest]
        public IEnumerator PredictionStaysAheadAtSatelliteLatency()
        {
            yield return Until(OnePlayerEachSide, 10f);
            Assert.That(OnePlayerEachSide(), Is.True, "no player ghost");
            // Satellite preset without jitter/loss (300 ms each way, ~600 ms RTT): past Netcode's 500 ms predict-ahead
            // cap, so the client should run ahead of the server with some input delay, never behind it.
            Sandbox.SetLink(300, 0, 0);
            yield return Until(() => DemoStats.EstimatedRttMs > 550f, 20f);
            Assert.That(DemoStats.EstimatedRttMs, Is.GreaterThan(550f), "link emulation did not raise RTT");
            float settle = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < settle)
                yield return null;

            int minLead = int.MaxValue;
            float until = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < until)
            {
                minLead = math.min(minLead, (int)(DemoStats.ClientServerTick - DemoStats.ServerTick));
                yield return null;
            }
            Debug.Log($"[PlayerGate] satellite: rtt {DemoStats.EstimatedRttMs:F0} ms, min client lead {minLead} ticks, command age {DemoStats.CommandAgeTicks}");
            Assert.That(minLead, Is.GreaterThan(0), "client predicted tick is not ahead of the server at ~600 ms RTT");
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

        [UnityTest]
        public IEnumerator HundredFiftyPlayersReplicate()
        {
            Sandbox.Bots = 149;
            yield return Until(() => DemoStats.PlayersOnClient == 150 && DemoStats.PlayersOnServer == 150, 20f);
            yield return new WaitForSeconds(3f); // let the smoothed readouts settle
            Debug.Log($"[DemoGate] players150 server={DemoStats.PlayersOnServer} client={DemoStats.PlayersOnClient} bots={DemoStats.BotsOnServer} " +
                      $"frameMs={DemoStats.FrameMs:F2} net={DemoStats.SnapshotBytesPerSecond:F0}B/s@{DemoStats.SnapshotsPerSecond:F0}/s avgSnapshot={DemoStats.AvgSnapshotBytes:F0}B " +
                      $"draws={DemoStats.DrawCalls} drawn={DemoStats.DrawnInstances}");
            Assert.That(DemoStats.PlayersOnServer, Is.EqualTo(150), "server player count");
            Assert.That(DemoStats.PlayersOnClient, Is.EqualTo(150), "client player count");
            Assert.That(DemoStats.BotsOnServer, Is.EqualTo(149), "BotsOnServer");
        }

        [UnityTest]
        public IEnumerator Level3HudShowsNoFalseFail()
        {
            Sandbox.Bots = 5;
            yield return Until(() => DemoStats.PlayersOnServer == 6 && DemoStats.GhostsOnClient == 6 && DemoStats.ClientInterpolationTick > 0, 15f);
            yield return new WaitForSeconds(0.5f);

            int ageTicks = HudVerdicts.SnapshotAgeTicks(DemoStats.ServerTick, DemoStats.NewestReplicatedTick, DemoStats.ClientInterpolationTick);
            Debug.Log($"[PlayerGate] hud: agents={DemoStats.SwarmCount} probes={DemoStats.GhostTarget} ghosts={DemoStats.GhostsOnClient} players={DemoStats.PlayersOnServer} " +
                      $"serverTick={DemoStats.ServerTick} interpTick={DemoStats.ClientInterpolationTick} newestProbe={DemoStats.NewestReplicatedTick} age={ageTicks} ticks");
            Assert.That(DemoStats.SwarmCount, Is.EqualTo(0), "level 3 runs no agents by default");
            Assert.That(DemoStats.GhostTarget, Is.EqualTo(0), "level 3 runs no probe ghosts by default");
            Assert.That(HudVerdicts.Burst(DemoStats.SwarmCount, DemoStats.SwarmFrames, DemoStats.SwarmJobRanManaged), Is.Null, "Burst row must be info, not PASS/FAIL");
            Assert.That(HudVerdicts.Threads(DemoStats.SwarmCount, DemoStats.SwarmJobThreadsSeen), Is.Null, "threads row must be info, not FAIL");
            Assert.That(HudVerdicts.Ghosts(DemoStats.GhostsOnClient, DemoStats.GhostTarget, DemoStats.PlayersOnServer, DemoStats.NewestReplicatedTick), Is.True,
                "replicated objects row must PASS with every player replicated");
            Assert.That(DemoStats.ClientInGame, Is.True, "connection row");
            Assert.That(ageTicks, Is.InRange(0, 30), "snapshot age must be the interpolation delay, not the server's uptime");
        }

        [UnityTest]
        public IEnumerator CameraFramesPlayerInUncoveredBand()
        {
            yield return Until(OnePlayerEachSide, 10f);
            Assert.That(OnePlayerEachSide(), Is.True, "no player ghost to frame");
            yield return new WaitForSeconds(1f); // let the camera settle
            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "no main camera");

            float left = DemoHud.LeftPanelRight, right = DemoHud.ControlPanelLeft;
            Vector3 player = ClientWorld().EntityManager.GetComponentData<LocalToWorld>(DemoStats.OwnedPlayerEntity).Position;
            Vector3 screen = camera.WorldToScreenPoint(player);
            Debug.Log($"[PlayerGate] screen {Screen.width}x{Screen.height}, band x {left:F0}..{right:F0} (centre {DemoHud.UncoveredBandCentre:F0}), player at screen {screen}");
            Assert.That(right, Is.GreaterThan(left), "no uncovered band at this screen size");
            Assert.That(screen.z, Is.GreaterThan(0f), "player behind the camera");
            Assert.That(screen.x, Is.InRange(left, right), "player hidden behind a HUD panel");
            Assert.That(screen.y, Is.InRange(0f, (float)Screen.height), "player off screen vertically");
        }

        static uint ClientNetworkTick()
        {
            var world = ClientWorld();
            if (world == null) return 0;
            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkTime>());
            if (query.IsEmpty) return 0;
            var tick = query.GetSingleton<NetworkTime>().ServerTick;
            return tick.IsValid ? tick.TickIndexForValidTick : 0;
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
