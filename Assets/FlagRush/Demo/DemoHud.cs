using UnityEngine;
using UnityEngine.InputSystem;

namespace FlagRush.Demo
{
    /// <summary>
    /// On-screen readout. Every row is "measurement -> what it means", so the numbers explain themselves.
    /// Self-instantiating; the scene needs no setup for it.
    /// </summary>
    public class DemoHud : MonoBehaviour
    {
        const float RefHeight = 900f;
        const float Margin = 12f;
        const float FullLeftMaxWidth = 1070f;
        const float CompactLeftWidth = 560f;   // Level 3: title, NETWORK essentials, PLAYER
        const float CollapsedLeftWidth = 340f; // Level 3 collapsed: title bar only
        const float ControlPanelWidth = 360f;

        /// <summary>Level 3: the left panel is collapsed to its title bar (toggled by its button or the H key).</summary>
        public static bool Collapsed;

        static float Scale => Screen.height / RefHeight;

        /// <summary>Width of the left panel in reference (900 px tall) units for an available reference width.</summary>
        static float LeftWidthRef(float availW)
        {
            if (DemoConfig.IsPlayerLevel)
                return Collapsed ? Mathf.Min(CollapsedLeftWidth, availW - 24f) : Mathf.Min(CompactLeftWidth, availW - ControlPanelWidth - 36f);
            return DemoConfig.Level >= 2 ? Mathf.Min(FullLeftMaxWidth, availW - ControlPanelWidth - 36f) : Mathf.Min(FullLeftMaxWidth, availW - 24f);
        }

        static float ControlPanelLeftRef(float availW) => availW - ControlPanelWidth - Margin;

        /// <summary>Right edge of the left readout panel, in screen pixels (x from the left).</summary>
        public static float LeftPanelRight
        {
            get
            {
                float scale = Scale;
                return scale > 0f ? (Margin + LeftWidthRef(Screen.width / scale)) * scale : 0f;
            }
        }

        /// <summary>Left edge of the control panel, in screen pixels; the screen's right edge when there is no control panel (Level 1).</summary>
        public static float ControlPanelLeft
        {
            get
            {
                float scale = Scale;
                if (DemoConfig.Level < 2 || scale <= 0f) return Screen.width;
                return ControlPanelLeftRef(Screen.width / scale) * scale;
            }
        }

        /// <summary>Centre of the screen band no panel covers (screen pixels); the screen centre if the panels overlap.</summary>
        public static float UncoveredBandCentre
        {
            get
            {
                float left = LeftPanelRight, right = ControlPanelLeft;
                return right > left ? (left + right) * 0.5f : Screen.width * 0.5f;
            }
        }
        static readonly Color Pass = new Color(0.45f, 0.9f, 0.5f);
        static readonly Color Fail = new Color(1f, 0.45f, 0.4f);
        static readonly Color Info = new Color(0.55f, 0.75f, 1f);
        static readonly Color Dim = new Color(0.72f, 0.72f, 0.72f);

        GUIStyle _title, _section, _label, _value, _meaning, _box;
        float _frameMs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            var go = new GameObject("DemoHud");
            go.AddComponent<DemoHud>();
            DontDestroyOnLoad(go);
        }

        float _nextTrace;

        void Update()
        {
            if (DemoConfig.IsPlayerLevel && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
                Collapsed = !Collapsed;
            if (Time.realtimeSinceStartup >= _nextTrace)
            {
                _nextTrace = Time.realtimeSinceStartup + 5f;
                Debug.Log($"[Demo] t={Time.realtimeSinceStartup:F1}s serverTick={DemoStats.ServerTick} clientServerTick={DemoStats.ClientServerTick} swarmFrames={DemoStats.SwarmFrames} connected={DemoStats.ClientConnected} inGame={DemoStats.ClientInGame} ghosts={DemoStats.GhostsOnClient}/{DemoStats.GhostsOnServer} rtt={DemoStats.EstimatedRttMs:F0} frameMs={DemoStats.FrameMs:F1} {DemoStats.LastError}");
            }
            float ms = Time.unscaledDeltaTime * 1000f;
            if (ms > 250f) return; // a stall (tab hidden, first frame) is not a frame time
            _frameMs = _frameMs == 0 ? ms : _frameMs * 0.95f + ms * 0.05f;
            DemoStats.FrameMs = _frameMs;
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

        static string Tag(bool? pass) => pass == null ? $"<color=#{Hex(Info)}>[ info ]</color>" : pass.Value ? $"<color=#{Hex(Pass)}>[ PASS ]</color>" : $"<color=#{Hex(Fail)}>[ FAIL ]</color>";

        void Row(bool? pass, string label, string value, string meaning)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tag(pass), _label, GUILayout.Width(64));
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
            float scale = Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.88f);

            int agents = DemoStats.SwarmCount;
            bool? burstOk = HudVerdicts.Burst(agents, DemoStats.SwarmFrames, DemoStats.SwarmJobRanManaged);
            bool? threadsOk = HudVerdicts.Threads(agents, DemoStats.SwarmJobThreadsSeen);
            bool connOk = DemoStats.ClientInGame;
            bool ghostsOk = HudVerdicts.Ghosts(DemoStats.GhostsOnClient, DemoStats.GhostTarget, DemoStats.PlayersOnServer, DemoStats.NewestReplicatedTick);
            int expectedGhosts = HudVerdicts.ExpectedGhosts(DemoStats.GhostTarget, DemoStats.PlayersOnServer);
            bool subOk = DemoStats.SubSceneEntitiesClient == 3;

            float usPerAgent = agents > 0 ? DemoStats.SwarmJobMs * 1000f / agents : 0f;
            float fps = DemoStats.FrameMs > 0 ? 1000f / DemoStats.FrameMs : 0f;
            int ticksBehind = HudVerdicts.SnapshotAgeTicks(DemoStats.ServerTick, DemoStats.NewestReplicatedTick, DemoStats.ClientInterpolationTick);
            int simRate = DemoStats.SimulationTickRate > 0 ? DemoStats.SimulationTickRate : 60;
            float bytesPerGhostTick = DemoStats.GhostsOnClient > 0 && DemoStats.SnapshotsPerSecond > 0
                ? DemoStats.SnapshotBytesPerSecond / (DemoStats.SnapshotsPerSecond * DemoStats.GhostsOnClient) : 0f;
            float est150 = bytesPerGhostTick * 150f * DemoStats.SnapshotsPerSecond / 1024f;

            float availW = Screen.width / scale;
            bool sandbox = DemoConfig.Level >= 2;
            bool player = DemoConfig.IsPlayerLevel;
            float leftW = LeftWidthRef(availW);
            if (sandbox) ControlPanel(ControlPanelLeftRef(availW), ControlPanelWidth);

            string ghostsValue = $"{DemoStats.GhostsOnClient} / {expectedGhosts}";
            string snapshotAge = $"{ticksBehind} ticks ({ticksBehind * 1000f / simRate:F0} ms)";
            if (player)
            {
                CompactPanel(leftW, connOk, ghostsOk, ghostsValue, snapshotAge);
                return;
            }

            GUILayout.BeginArea(new Rect(Margin, Margin, leftW, 660), _box);
            GUILayout.Label($"FlagRush  <color=#{Hex(Dim)}>{(sandbox ? "level 2 · sandbox" : "level 1 · networking simulator")}</color>", _title);
            GUILayout.Label($"<color=#{Hex(Dim)}>{Application.platform} · {SystemInfo.graphicsDeviceType} · {SystemInfo.processorCount} cores · {(Debug.isDebugBuild ? "development" : "release")} build · up {Time.realtimeSinceStartup:F0}s · server tick {DemoStats.ServerTick}</color>", _label);
            GUILayout.Label($"<color=#{Hex(Dim)}>Two ECS worlds run in this tab: a <b>server</b> (owns the truth) and a <b>client</b> (shows it), joined by an in-process transport that carries the same packets a real socket would.</color>", _meaning, GUILayout.Width(1040));

            const string noAgents = "— (no agents at this level)";
            Section("COMPUTE", "can heavy simulation run in a browser?");
            Row(burstOk, "Burst-compiled job", burstOk == null ? noAgents : burstOk.Value ? "YES" : "NO (managed fallback ran)", $"the {agents} blue agents are moved by native WebAssembly, not interpreted C#");
            Row(threadsOk, "Worker threads used", threadsOk == null ? noAgents : $"{DemoStats.SwarmJobThreadsSeen} ({DemoStats.JobWorkerCount} workers + main)", "the work is split across CPU cores; the main thread is not the bottleneck");
            Row(null, "Job time", $"{DemoStats.SwarmJobMs:F2} ms / {agents} agents", $"≈ {usPerAgent:F2} µs per agent; 10,000 agents ≈ {usPerAgent * 10f:F1} ms (linear estimate)");
            Row(null, "Frame time", $"{DemoStats.FrameMs:F1} ms ({fps:F0} fps)", "both worlds, the job and rendering all fit inside one frame");

            Section("NETWORK", "does server → client replication work here?");
            Row(connOk, "Connection", ConnectionText(connOk), "the client finished the handshake and asked the server for game state");
            Row(ghostsOk, "Replicated objects", ghostsValue, "every player and every server-owned orange object exists on the client, fed by snapshots");
            Row(null, "Server → client bandwidth", $"{DemoStats.SnapshotBytesPerSecond / 1024f:F2} KB/s · {DemoStats.SnapshotsPerSecond:F0} snapshots/s · {DemoStats.AvgSnapshotBytes:F0} B each", $"≈ {bytesPerGhostTick:F1} B per object per tick; 150 such objects ≈ {est150:F1} KB/s (estimate)");
            Row(null, "Snapshot age", snapshotAge, "the client shows the server's world this far in the past: transit + interpolation buffer");
            Row(null, "Round trip", $"{DemoStats.EstimatedRttMs:F1} ms", "client → server → client; on the internet this is your ping");
            if (sandbox)
                Row(null, "Simulated link", $"{Sandbox.LatencyMs} ms ±{Sandbox.JitterMs} each way · {Sandbox.LossPercent}% loss · interp {(Sandbox.InterpolationMs == 0 ? "auto" : Sandbox.InterpolationMs + " ms")}", $"added by a transport pipeline stage on the client; expect round trip ≈ {2 * Sandbox.LatencyMs} ms");
            Row(null, "Packet loss", $"{DemoStats.PacketLossPercent:F1}%", "snapshots that never arrived (IPC never drops; real networks do)");
            Row(null, "Tick rate", $"{DemoStats.SimulationTickRate} sim / {DemoStats.NetworkTickRate} net per second", "the server simulates and sends this often; ticks are the unit all timing is measured in");

            Section("LEVEL", "can baked level data load on the web?");
            Row(subOk, "SubScene entities", $"{DemoStats.SubSceneEntitiesClient} / 3 client · {DemoStats.SubSceneEntitiesServer} / 3 server", "the arena can be authored as a normal Unity scene, baked to entities and streamed in");

            Section("RENDERING", "without Unity's ECS renderer (no Web support)");
            Row(null, "Draw calls", $"{DemoStats.DrawCalls} for {DemoStats.DrawnInstances} objects", "instanced rendering straight from entity transforms; count barely grows with object count");

            GUILayout.Space(8);
            GUILayout.Label(sandbox
                ? $"<color=#{Hex(Dim)}>Level 1 shows the readouts; this level lets you cause them. Source: github.com/Mesmoraz/FlagRush</color>"
                : $"<color=#{Hex(Dim)}>Push it: add <b>?agents=5000&amp;ghosts=200</b> to the URL, open <b>?level=2</b> for live controls, or <b>?level=3</b> to drive a player.  Source: github.com/Mesmoraz/FlagRush</color>", _meaning, GUILayout.Width(leftW - 30f));
            if (!string.IsNullOrEmpty(DemoStats.LastError)) GUILayout.Label($"<color=#{Hex(Fail)}>note: {DemoStats.LastError}</color>", _meaning);
            GUILayout.EndArea();
        }

        static string ConnectionText(bool connOk)
            => connOk ? "in-game over IPC" : DemoStats.ClientConnected ? "connected, handshaking" : "not connected";

        // ---------------------------------------------------------------- Level 3: compact panel

        float _compactHeight = 560f; // measured from the laid-out content on each repaint

        void CompactRow(bool? pass, string label, string value, float width)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tag(pass), _label, GUILayout.Width(64));
            GUILayout.Label(label, _label, GUILayout.Width(180));
            GUILayout.Label(value, _value, GUILayout.Width(width - 64f - 180f - 30f));
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Level 3 keeps the player visible: a narrow panel (title, NETWORK essentials, PLAYER) that collapses to its
        /// title bar with its button or the H key. The camera frames the player in the band between this panel and
        /// the control panel (<see cref="UncoveredBandCentre"/>).
        /// </summary>
        void CompactPanel(float width, bool connOk, bool ghostsOk, string ghostsValue, string snapshotAge)
        {
            float height = Collapsed ? 46f : _compactHeight;
            GUILayout.BeginArea(new Rect(Margin, Margin, width, height), _box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"FlagRush  <color=#{Hex(Dim)}>level 3 · player</color>", _title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Collapsed ? "show (H)" : "hide (H)", GUILayout.Width(80))) Collapsed = !Collapsed;
            GUILayout.EndHorizontal();

            if (!Collapsed)
            {
                GUILayout.Label($"<color=#{Hex(Dim)}>up {Time.realtimeSinceStartup:F0}s · server tick {DemoStats.ServerTick} · {DemoStats.FrameMs:F1} ms/frame</color>", _label);

                Section("NETWORK", "server → client");
                CompactRow(connOk, "Connection", ConnectionText(connOk), width);
                CompactRow(ghostsOk, "Replicated objects", ghostsValue + "  (players + orange)", width);
                CompactRow(null, "Bandwidth", $"{DemoStats.SnapshotBytesPerSecond / 1024f:F2} KB/s · {DemoStats.AvgSnapshotBytes:F0} B/snapshot", width);
                CompactRow(null, "Snapshot age", snapshotAge, width);
                CompactRow(null, "Round trip", $"{DemoStats.EstimatedRttMs:F1} ms", width);
                CompactRow(null, "Simulated link", $"{Sandbox.LatencyMs} ms ±{Sandbox.JitterMs} · {Sandbox.LossPercent}% loss", width);

                Section("PLAYER", "does it feel instant at 200 ms?");
                CompactRow(null, "Command age", $"{DemoStats.CommandAgeTicks} ticks ({DemoStats.CommandAgeMs:F0} ms)", width);
                CompactRow(null, "Predicted ticks / frame", $"{DemoStats.PredictedTicksPerFrame}", width);
                CompactRow(null, "Rollbacks / s", $"{DemoStats.RollbacksPerSecond:F0}", width);
                CompactRow(null, "Misprediction", DemoStats.OwnedPlayerPredicted ? $"{DemoStats.MispredictionMeters:F3} m" : "— (prediction off)", width);
                CompactRow(null, "Players (srv / client)", $"{DemoStats.PlayersOnServer} / {DemoStats.PlayersOnClient} ({DemoStats.BotsOnServer} bots)", width);

                GUILayout.Space(8);
                GUILayout.Label($"<color=#{Hex(Dim)}>Click the game, then WASD. Try: 200 ms latency, uncheck Prediction, walk into a bot. H hides this panel.  Source: github.com/Mesmoraz/FlagRush</color>", _meaning, GUILayout.Width(width - 30f));
                if (!string.IsNullOrEmpty(DemoStats.LastError)) GUILayout.Label($"<color=#{Hex(Fail)}>note: {DemoStats.LastError}</color>", _meaning, GUILayout.Width(width - 30f));
                if (Event.current.type == EventType.Repaint) _compactHeight = GUILayoutUtility.GetLastRect().yMax + 16f;
            }
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- Level 2: control panel

        GUIStyle _panelTitle, _hint, _slider, _thumb;
        static readonly (string name, int lat, int jit, int loss)[] Presets =
        {
            ("LAN", 0, 0, 0), ("Wi-Fi", 15, 5, 1), ("4G", 60, 20, 2), ("Bad hotel", 150, 60, 8), ("Satellite", 300, 30, 3),
        };

        int IntSlider(string label, int value, int min, int max, string expect, string unit = "")
        {
            GUILayout.Label($"{label}: <b>{value}{unit}</b>", _label);
            int v = (int)GUILayout.HorizontalSlider(value, min, max, _slider, _thumb);
            GUILayout.Label($"<color=#{Hex(Dim)}>expect: {expect}</color>", _hint);
            GUILayout.Space(4);
            return v;
        }

        void ControlPanel(float x, float width)
        {
            if (_panelTitle == null)
            {
                _panelTitle = new GUIStyle(_section);
                _hint = new GUIStyle(_meaning) { fontSize = 12 };
                _slider = new GUIStyle(GUI.skin.horizontalSlider);
                _thumb = new GUIStyle(GUI.skin.horizontalSliderThumb);
            }

            GUILayout.BeginArea(new Rect(x, 12, width, DemoConfig.IsPlayerLevel ? 860 : 760), _box);
            GUILayout.Label("SANDBOX  <color=#" + Hex(Dim) + "><i>change a variable, watch the readout move</i></color>", _panelTitle);
            GUILayout.Space(6);

            Sandbox.Agents = IntSlider("Agents (client, Burst job)", Sandbox.Agents, 0, Sandbox.MaxAgents, "job time grows linearly; frame time barely moves");
            Sandbox.Ghosts = IntSlider("Replicated objects (server)", Sandbox.Ghosts, 0, Sandbox.MaxGhosts, "bandwidth grows linearly; snapshots get bigger, not more frequent");
            if (DemoConfig.IsPlayerLevel)
            {
                Sandbox.Bots = IntSlider("Bots (server)", Sandbox.Bots, 0, Sandbox.MaxBots, "more grey players to walk into; bandwidth grows with them");
                Sandbox.PredictionEnabled = GUILayout.Toggle(Sandbox.PredictionEnabled, " Prediction (client-side, for your player)");
                GUILayout.Label($"<color=#{Hex(Dim)}>expect: off -> your green cube lags your keys by a round trip; the translucent cube (server position) disappears</color>", _hint);
                GUILayout.Space(4);
            }

            GUILayout.Label($"Network tick rate: <b>{Sandbox.NetworkTickRate}/s</b>", _label);
            GUILayout.BeginHorizontal();
            foreach (var r in new[] { 10, 20, 30, 60 })
                if (GUILayout.Toggle(Sandbox.NetworkTickRate == r, $" {r}", GUI.skin.button) && Sandbox.NetworkTickRate != r) Sandbox.NetworkTickRate = r;
            GUILayout.EndHorizontal();
            GUILayout.Label($"<color=#{Hex(Dim)}>expect: fewer snapshots/s, less bandwidth, higher snapshot age (reconnects to apply)</color>", _hint);
            GUILayout.Space(4);

            int lat = IntSlider("Link latency, one way", Sandbox.LatencyMs, 0, 300, "round trip ~= 2x this; snapshot age rises by the same amount", " ms");
            int jit = IntSlider("Link jitter", Sandbox.JitterMs, 0, 100, "snapshot age wobbles; raise interpolation delay to hide it", " ms");
            int loss = IntSlider("Packet loss", Sandbox.LossPercent, 0, 30, "packet loss % rises; objects stay smooth until the interpolation buffer runs dry", " %");
            if (lat != Sandbox.LatencyMs || jit != Sandbox.JitterMs || loss != Sandbox.LossPercent) Sandbox.SetLink(lat, jit, loss);

            GUILayout.BeginHorizontal();
            foreach (var p in Presets)
                if (GUILayout.Button(p.name)) Sandbox.SetLink(p.lat, p.jit, p.loss);
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            Sandbox.InterpolationMs = IntSlider("Interpolation delay (0 = auto)", Sandbox.InterpolationMs, 0, 300, "snapshot age grows by this much; jitter and loss stop showing as stutter", " ms");

            GUILayout.BeginHorizontal();
            bool frozen = GUILayout.Toggle(Sandbox.ServerFrozen, Sandbox.ServerFrozen ? " Server frozen — unfreeze" : " Freeze server (hitch)", GUI.skin.button);
            if (frozen != Sandbox.ServerFrozen) SetServerFrozen(frozen);
            if (GUILayout.Button("Kill connection")) Sandbox.KillConnectionRequested = true;
            GUILayout.EndHorizontal();
            GUILayout.Label($"<color=#{Hex(Dim)}>expect: frozen -> objects stop, snapshot age climbs, after 30 s the client times out and reconnects. Kill -> objects vanish and respawn from snapshots in ~3 s. Reconnects so far: {Sandbox.Reconnects}</color>", _hint);
            GUILayout.EndArea();
        }

        static void SetServerFrozen(bool frozen)
        {
            Sandbox.ServerFrozen = frozen;
            var server = Unity.NetCode.ClientServerBootstrap.ServerWorld;
            if (server == null) return;
            var group = server.GetExistingSystemManaged<Unity.Entities.SimulationSystemGroup>();
            if (group != null) group.Enabled = !frozen;
        }
    }
}
