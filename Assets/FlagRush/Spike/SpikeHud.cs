using Unity.Burst;
using UnityEngine;

namespace FlagRush.Spike
{
    /// <summary>On-screen gate readout. Self-instantiating so the scene needs no setup for it.</summary>
    public class SpikeHud : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            var go = new GameObject("SpikeHud");
            go.AddComponent<SpikeHud>();
            DontDestroyOnLoad(go);
        }

        static string Yes(bool b) => b ? "YES" : "no";

        void OnGUI()
        {
            var burstOk = SpikeStats.SwarmFrames > 0 && !SpikeStats.SwarmJobRanManaged;
            var threadsOk = SpikeStats.SwarmJobThreadsSeen > 1;
            var netOk = SpikeStats.ClientInGame && SpikeStats.GhostsOnClient > 0 && SpikeStats.NewestReplicatedTick > 0;
            var subSceneOk = SpikeStats.SubSceneEntitiesClient > 0;

            GUI.skin.label.fontSize = 14;
            GUILayout.BeginArea(new Rect(10, 10, 620, 520), GUI.skin.box);
            GUILayout.Label($"FlagRush M1 spike  |  {Application.platform}  |  {SystemInfo.graphicsDeviceType}  |  {SystemInfo.processorCount} cores");
            GUILayout.Space(6);
            GUILayout.Label($"[{(burstOk ? "PASS" : "FAIL")}] (a) Burst: job body compiled by Burst = {Yes(!SpikeStats.SwarmJobRanManaged)}  (BurstCompiler.IsEnabled={BurstCompiler.IsEnabled})");
            GUILayout.Label($"[{(threadsOk ? "PASS" : "FAIL")}] (a) Threads: job ran on {SpikeStats.SwarmJobThreadsSeen} distinct threads, JobWorkerCount={SpikeStats.JobWorkerCount}, swarm={SpikeStats.SwarmCount} frames={SpikeStats.SwarmFrames}");
            GUILayout.Label($"[{(netOk ? "PASS" : "FAIL")}] (b) Netcode over IPC: worlds server={Yes(SpikeStats.ServerWorldExists)} client={Yes(SpikeStats.ClientWorldExists)}  connected={Yes(SpikeStats.ClientConnected)} inGame={Yes(SpikeStats.ClientInGame)}");
            GUILayout.Label($"      server: tick={SpikeStats.ServerTick} connections={SpikeStats.ServerConnections} ghosts={SpikeStats.GhostsOnServer}");
            GUILayout.Label($"      client: serverTick={SpikeStats.ClientServerTick} ghosts={SpikeStats.GhostsOnClient} newestReplicatedTick={SpikeStats.NewestReplicatedTick} rtt={SpikeStats.EstimatedRttMs:F1}ms");
            GUILayout.Label($"[{(subSceneOk ? "PASS" : "FAIL")}] (c) SubScene: baked entities client={SpikeStats.SubSceneEntitiesClient} server={SpikeStats.SubSceneEntitiesServer}");
            if (!string.IsNullOrEmpty(SpikeStats.LastError)) GUILayout.Label($"note: {SpikeStats.LastError}");
            GUILayout.EndArea();
        }
    }
}
