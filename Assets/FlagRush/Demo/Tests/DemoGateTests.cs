using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlagRush.Demo.Tests
{
    /// <summary>The M1 gate, run in the Editor. The Web build shows the same readout in its HUD.</summary>
    public class DemoGateTests
    {
        [UnityTest]
        public IEnumerator BurstJobsRunMultithreaded_GhostsReplicateOverIpc_SubSceneLoads()
        {
            SceneManager.LoadScene("SampleScene");
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline && !AllGreen()) yield return null;

            Debug.Log($"[DemoGate] burstManaged={DemoStats.SwarmJobRanManaged} threads={DemoStats.SwarmJobThreadsSeen}/{DemoStats.JobWorkerCount} frames={DemoStats.SwarmFrames} " +
                      $"connected={DemoStats.ClientConnected} inGame={DemoStats.ClientInGame} ghostsClient={DemoStats.GhostsOnClient} ghostsServer={DemoStats.GhostsOnServer} " +
                      $"newestTick={DemoStats.NewestReplicatedTick} serverTick={DemoStats.ServerTick} rtt={DemoStats.EstimatedRttMs:F1} subScene={DemoStats.SubSceneEntitiesClient}/{DemoStats.SubSceneEntitiesServer} drawn={DemoStats.DrawnInstances} draws={DemoStats.DrawCalls} jobMs={DemoStats.SwarmJobMs:F3} net={DemoStats.SnapshotBytesPerSecond:F0}B/s@{DemoStats.SnapshotsPerSecond:F0}/s loss={DemoStats.PacketLossPercent:F1}% tick={DemoStats.SimulationTickRate}/{DemoStats.NetworkTickRate} err='{DemoStats.LastError}'");

            Assert.That(DemoStats.SwarmFrames, Is.GreaterThan(0), "swarm job never ran");
            Assert.That(DemoStats.SwarmJobRanManaged, Is.False, "(a) swarm job body executed as managed code, not Burst");
            Assert.That(DemoStats.SwarmJobThreadsSeen, Is.GreaterThan(1), "(a) swarm job ran on a single thread");
            Assert.That(DemoStats.ClientInGame, Is.True, "(b) client never went in-game over IPC");
            Assert.That(DemoStats.GhostsOnClient, Is.EqualTo(ProbeServerSystem.GhostCount), "(b) ghosts did not replicate");
            Assert.That(DemoStats.NewestReplicatedTick, Is.GreaterThan(0u), "(b) ghost fields never updated on the client");
            Assert.That(DemoStats.SubSceneEntitiesClient, Is.EqualTo(3), "(c) SubScene entities missing on the client");
            Assert.That(DemoStats.DrawnInstances, Is.EqualTo(SwarmSystem.AgentCount + ProbeServerSystem.GhostCount), "render bridge drew the wrong number of instances");
            Assert.That(DemoStats.SnapshotsPerSecond, Is.GreaterThan(0f), "no snapshot bytes were measured on the wire");
        }

        [UnityTest]
        public IEnumerator SandboxKnobsMoveTheReadouts()
        {
            SceneManager.LoadScene("SampleScene");
            yield return Until(() => DemoStats.ClientInGame && DemoStats.GhostsOnClient == Sandbox.Ghosts, 30f);

            // Latency: RTT must follow the slider (both directions are delayed => ~2x).
            Sandbox.SetLink(100, 0, 0);
            yield return Until(() => DemoStats.EstimatedRttMs > 150f, 15f);
            Assert.That(DemoStats.EstimatedRttMs, Is.GreaterThan(150f), "RTT did not follow the latency slider");

            // Ghost count: server reconciles, client receives them all.
            Sandbox.Ghosts = 40;
            yield return Until(() => DemoStats.GhostsOnClient == 40, 15f);
            Assert.That(DemoStats.GhostsOnClient, Is.EqualTo(40), "ghost count did not reconcile");

            // Agent count: client reconciles and the render bridge draws them.
            Sandbox.Agents = 1200;
            yield return Until(() => DemoStats.SwarmCount == 1200 && DemoStats.DrawnInstances == 1240, 10f);
            Assert.That(DemoStats.DrawnInstances, Is.EqualTo(1240), "agent count did not reconcile");

            // Kill the connection: the reconnect system must heal it.
            int reconnects = Sandbox.Reconnects;
            Sandbox.KillConnectionRequested = true;
            yield return Until(() => !DemoStats.ClientConnected, 5f);
            yield return Until(() => DemoStats.ClientInGame && DemoStats.GhostsOnClient == 40, 20f);
            Assert.That(Sandbox.Reconnects, Is.GreaterThan(reconnects), "no reconnect happened");

            Debug.Log($"[DemoGate] sandbox rtt={DemoStats.EstimatedRttMs:F0} ghosts={DemoStats.GhostsOnClient} agents={DemoStats.SwarmCount} reconnects={Sandbox.Reconnects}");
            Sandbox.SetLink(0, 0, 0); Sandbox.Ghosts = DemoConfig.DefaultGhosts; Sandbox.Agents = DemoConfig.DefaultAgents;
        }

        static IEnumerator Until(System.Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline && !condition()) yield return null;
        }

        static bool AllGreen() =>
            DemoStats.SwarmFrames > 30 && !DemoStats.SwarmJobRanManaged && DemoStats.SwarmJobThreadsSeen > 1 &&
            DemoStats.ClientInGame && DemoStats.GhostsOnClient == ProbeServerSystem.GhostCount && DemoStats.NewestReplicatedTick > 0 &&
            DemoStats.SubSceneEntitiesClient == 3 && DemoStats.DrawnInstances == SwarmSystem.AgentCount + ProbeServerSystem.GhostCount &&
            DemoStats.SnapshotsPerSecond > 0f;
    }
}
