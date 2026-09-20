using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlagRush.Spike.Tests
{
    /// <summary>The M1 gate, run in the Editor. The Web build shows the same readout in its HUD.</summary>
    public class SpikeGateTests
    {
        [UnityTest]
        public IEnumerator BurstJobsRunMultithreaded_GhostsReplicateOverIpc_SubSceneLoads()
        {
            SceneManager.LoadScene("SampleScene");
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline && !AllGreen()) yield return null;

            Debug.Log($"[SpikeGate] burstManaged={SpikeStats.SwarmJobRanManaged} threads={SpikeStats.SwarmJobThreadsSeen}/{SpikeStats.JobWorkerCount} frames={SpikeStats.SwarmFrames} " +
                      $"connected={SpikeStats.ClientConnected} inGame={SpikeStats.ClientInGame} ghostsClient={SpikeStats.GhostsOnClient} ghostsServer={SpikeStats.GhostsOnServer} " +
                      $"newestTick={SpikeStats.NewestReplicatedTick} serverTick={SpikeStats.ServerTick} rtt={SpikeStats.EstimatedRttMs:F1} subScene={SpikeStats.SubSceneEntitiesClient}/{SpikeStats.SubSceneEntitiesServer} drawn={SpikeStats.DrawnInstances} err='{SpikeStats.LastError}'");

            Assert.That(SpikeStats.SwarmFrames, Is.GreaterThan(0), "swarm job never ran");
            Assert.That(SpikeStats.SwarmJobRanManaged, Is.False, "(a) swarm job body executed as managed code, not Burst");
            Assert.That(SpikeStats.SwarmJobThreadsSeen, Is.GreaterThan(1), "(a) swarm job ran on a single thread");
            Assert.That(SpikeStats.ClientInGame, Is.True, "(b) client never went in-game over IPC");
            Assert.That(SpikeStats.GhostsOnClient, Is.EqualTo(SpikeServerSystem.GhostCount), "(b) ghosts did not replicate");
            Assert.That(SpikeStats.NewestReplicatedTick, Is.GreaterThan(0u), "(b) ghost fields never updated on the client");
            Assert.That(SpikeStats.SubSceneEntitiesClient, Is.EqualTo(3), "(c) SubScene entities missing on the client");
            Assert.That(SpikeStats.DrawnInstances, Is.EqualTo(SwarmSystem.AgentCount + SpikeServerSystem.GhostCount), "render bridge drew the wrong number of instances");
        }

        static bool AllGreen() =>
            SpikeStats.SwarmFrames > 30 && !SpikeStats.SwarmJobRanManaged && SpikeStats.SwarmJobThreadsSeen > 1 &&
            SpikeStats.ClientInGame && SpikeStats.GhostsOnClient == SpikeServerSystem.GhostCount && SpikeStats.NewestReplicatedTick > 0 &&
            SpikeStats.SubSceneEntitiesClient == 3 && SpikeStats.DrawnInstances == SwarmSystem.AgentCount + SpikeServerSystem.GhostCount;
    }
}
