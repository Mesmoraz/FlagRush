using UnityEngine;

namespace FlagRush.Spike
{
    /// <summary>
    /// On-screen readout. Every row is "measurement -> what it means", so the numbers explain themselves.
    /// Self-instantiating; the scene needs no setup for it.
    /// </summary>
    public class SpikeHud : MonoBehaviour
    {
        const float RefHeight = 900f;
        static readonly Color Pass = new Color(0.45f, 0.9f, 0.5f);
        static readonly Color Fail = new Color(1f, 0.45f, 0.4f);
        static readonly Color Info = new Color(0.55f, 0.75f, 1f);
        static readonly Color Dim = new Color(0.72f, 0.72f, 0.72f);

        GUIStyle _title, _section, _label, _value, _meaning, _box;
        float _frameMs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            var go = new GameObject("SpikeHud");
            go.AddComponent<SpikeHud>();
            DontDestroyOnLoad(go);
        }

        void Update()
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            _frameMs = _frameMs == 0 ? ms : _frameMs * 0.95f + ms * 0.05f;
            SpikeStats.FrameMs = _frameMs;
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, richText = true };
            _section = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, richText = true };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, alignment = TextAnchor.UpperLeft };
            _value = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, richText = true };
            _meaning = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = true };
            _meaning.normal.textColor = Dim;
            _box = new GUIStyle(GUI.skin.box);
            _box.normal.background = Texture2D.grayTexture;
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        void Row(bool? pass, string label, string value, string meaning)
        {
            GUILayout.BeginHorizontal();
            var tag = pass == null ? $"<color=#{Hex(Info)}>[ info ]</color>" : pass.Value ? $"<color=#{Hex(Pass)}>[ PASS ]</color>" : $"<color=#{Hex(Fail)}>[ FAIL ]</color>";
            GUILayout.Label(tag, _label, GUILayout.Width(64));
            GUILayout.Label(label, _label, GUILayout.Width(210));
            GUILayout.Label(value, _value, GUILayout.Width(290));
            GUILayout.Label("→ " + meaning, _meaning, GUILayout.Width(470));
            GUILayout.EndHorizontal();
        }

        void Section(string title, string question)
        {
            GUILayout.Space(8);
            GUILayout.Label($"{title}  <color=#{Hex(Dim)}><i>{question}</i></color>", _section);
        }

        void OnGUI()
        {
            EnsureStyles();
            float scale = Screen.height / RefHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.88f);

            bool burstOk = SpikeStats.SwarmFrames > 0 && !SpikeStats.SwarmJobRanManaged;
            bool threadsOk = SpikeStats.SwarmJobThreadsSeen > 1;
            bool connOk = SpikeStats.ClientInGame;
            bool ghostsOk = SpikeStats.GhostTarget > 0 && SpikeStats.GhostsOnClient == SpikeStats.GhostTarget && SpikeStats.NewestReplicatedTick > 0;
            bool subOk = SpikeStats.SubSceneEntitiesClient == 3;

            int agents = SpikeStats.SwarmCount;
            float usPerAgent = agents > 0 ? SpikeStats.SwarmJobMs * 1000f / agents : 0f;
            float fps = SpikeStats.FrameMs > 0 ? 1000f / SpikeStats.FrameMs : 0f;
            int ticksBehind = (int)(SpikeStats.ServerTick - SpikeStats.NewestReplicatedTick);
            int simRate = SpikeStats.SimulationTickRate > 0 ? SpikeStats.SimulationTickRate : 60;
            float bytesPerGhostTick = SpikeStats.GhostsOnClient > 0 && SpikeStats.SnapshotsPerSecond > 0
                ? SpikeStats.SnapshotBytesPerSecond / (SpikeStats.SnapshotsPerSecond * SpikeStats.GhostsOnClient) : 0f;
            float est150 = bytesPerGhostTick * 150f * SpikeStats.SnapshotsPerSecond / 1024f;

            GUILayout.BeginArea(new Rect(12, 12, 1070, 640), _box);
            GUILayout.Label($"FlagRush  <color=#{Hex(Dim)}>networking simulator spike</color>", _title);
            GUILayout.Label($"<color=#{Hex(Dim)}>{Application.platform} · {SystemInfo.graphicsDeviceType} · {SystemInfo.processorCount} cores · {(Debug.isDebugBuild ? "development" : "release")} build</color>", _label);
            GUILayout.Label($"<color=#{Hex(Dim)}>Two ECS worlds run in this tab: a <b>server</b> (owns the truth) and a <b>client</b> (shows it), joined by an in-process transport that carries the same packets a real socket would.</color>", _meaning, GUILayout.Width(1040));

            Section("COMPUTE", "can heavy simulation run in a browser?");
            Row(burstOk, "Burst-compiled job", burstOk ? "YES" : "NO (managed fallback ran)", $"the {agents} blue agents are moved by native WebAssembly, not interpreted C#");
            Row(threadsOk, "Worker threads used", $"{SpikeStats.SwarmJobThreadsSeen} ({SpikeStats.JobWorkerCount} workers + main)", "the work is split across CPU cores; the main thread is not the bottleneck");
            Row(null, "Job time", $"{SpikeStats.SwarmJobMs:F2} ms / {agents} agents", $"≈ {usPerAgent:F2} µs per agent; 10,000 agents ≈ {usPerAgent * 10f:F1} ms (linear estimate)");
            Row(null, "Frame time", $"{SpikeStats.FrameMs:F1} ms ({fps:F0} fps)", "both worlds, the job and rendering all fit inside one frame");

            Section("NETWORK", "does server → client replication work here?");
            Row(connOk, "Connection", connOk ? "in-game over IPC" : SpikeStats.ClientConnected ? "connected, handshaking" : "not connected", "the client finished the handshake and asked the server for game state");
            Row(ghostsOk, "Replicated objects", $"{SpikeStats.GhostsOnClient} / {SpikeStats.GhostTarget}", "every server-owned orange object exists on the client, fed by snapshots");
            Row(null, "Server → client bandwidth", $"{SpikeStats.SnapshotBytesPerSecond / 1024f:F2} KB/s · {SpikeStats.SnapshotsPerSecond:F0} snapshots/s · {SpikeStats.AvgSnapshotBytes:F0} B each", $"≈ {bytesPerGhostTick:F1} B per object per tick; 150 such objects ≈ {est150:F1} KB/s (estimate)");
            Row(null, "Snapshot age", $"{ticksBehind} ticks ({ticksBehind * 1000f / simRate:F0} ms)", "the client shows the server's world this far in the past: transit + interpolation buffer");
            Row(null, "Round trip", $"{SpikeStats.EstimatedRttMs:F1} ms", "client → server → client; on the internet this is your ping");
            Row(null, "Packet loss", $"{SpikeStats.PacketLossPercent:F1}%", "snapshots that never arrived (IPC never drops; real networks do)");
            Row(null, "Tick rate", $"{SpikeStats.SimulationTickRate} sim / {SpikeStats.NetworkTickRate} net per second", "the server simulates and sends this often; ticks are the unit all timing is measured in");

            Section("LEVEL", "can baked level data load on the web?");
            Row(subOk, "SubScene entities", $"{SpikeStats.SubSceneEntitiesClient} / 3 client · {SpikeStats.SubSceneEntitiesServer} / 3 server", "the arena can be authored as a normal Unity scene, baked to entities and streamed in");

            Section("RENDERING", "without Unity's ECS renderer (no Web support)");
            Row(null, "Draw calls", $"{SpikeStats.DrawCalls} for {SpikeStats.DrawnInstances} objects", "instanced rendering straight from entity transforms; count barely grows with object count");

            GUILayout.Space(8);
            GUILayout.Label($"<color=#{Hex(Dim)}>Push it: add <b>?agents=5000&amp;ghosts=200</b> to the URL.  Source: github.com/Mesmoraz/FlagRush</color>", _meaning, GUILayout.Width(1040));
            if (!string.IsNullOrEmpty(SpikeStats.LastError)) GUILayout.Label($"<color=#{Hex(Fail)}>note: {SpikeStats.LastError}</color>", _meaning);
            GUILayout.EndArea();
        }
    }
}
