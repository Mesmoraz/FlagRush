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

        static bool AllGreen() =>
            DemoStats.SwarmFrames > 30 && !DemoStats.SwarmJobRanManaged && DemoStats.SwarmJobThreadsSeen > 1 &&
            DemoStats.ClientInGame && DemoStats.GhostsOnClient == ProbeServerSystem.GhostCount && DemoStats.NewestReplicatedTick > 0 &&
            DemoStats.SubSceneEntitiesClient == 3 && DemoStats.DrawnInstances == SwarmSystem.AgentCount + ProbeServerSystem.GhostCount &&
            DemoStats.SnapshotsPerSecond > 0f;
    }
}
