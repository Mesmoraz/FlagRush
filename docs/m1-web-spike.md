# M1 — Web de-risk spike: results

Question: can Entities + Burst jobs + Netcode for Entities run **in the browser** (WebGPU, Unity 6.6.2),
with a server world hosted in-process? Answer: **yes, all gates pass.** Measured 2026-09-20 in the
Claude desktop browser (Chromium), Windows, 20-core machine, development build served with COOP/COEP.

| Gate | Editor (PlayMode test) | Browser (WebGPU) |
|---|---|---|
| (a) `[BurstCompile]` job body compiled by Burst (no managed fallback) | PASS | **PASS** (`BurstCompiler.IsEnabled=True`) |
| (a) job ran on worker threads | PASS, 4–20 threads | **PASS**, 6 distinct threads, `JobWorkerCount=5` |
| (b) client + server worlds in one process over IPC, ghosts replicate | PASS, 8/8 | **PASS**, 8/8 ghosts, RTT 16 ms, client 2 ticks behind server |
| (c) SubScene (baked entities) loads | PASS, 3/3 | **PASS**, 3/3, streamed from `StreamingAssets/EntityScenes` |
| custom render bridge (`Graphics.RenderMeshInstanced` from `LocalToWorld`) | PASS, 158 instances | **PASS**, 2 draw calls for 158 objects |
| release build (Brotli, no splash) on GitHub Pages | – | **PASS** on WebGL2; WebGPU release stalls (see gotchas) |

## Level 3 — Player: 150 players

Gate: `PlayerGateTests.HundredFiftyPlayersReplicate` (1 human + 149 bots, PlayMode). Browser numbers were
measured in Chromium (the Claude desktop browser) on the WebGL2 build at `?level=3&bots=149`, at
1600×900 and 1024×768 viewports, after the replication cut (one position per player: `LocalTransform`
not serialized, `ServerPosition` sent to its owner only).

| Measure | Editor (PlayMode test) | Browser (WebGL2, LAN link) |
|---|---|---|
| Players on server / client | 150 / 150 | **150 / 150** (149 bots); Connection PASS, Replicated objects PASS |
| Frame time | 0.61 ms (headless test, unthrottled) | 12.1–12.3 ms (≈ 82 fps) |
| Snapshot bandwidth, before the replication cut | 57,929 B/s at 60 snapshots/s (avg 966 B) | 51.7–55 KB/s (≈ 880–934 B per snapshot) |
| Snapshot bandwidth, after the replication cut | ≈ 32,000 B/s at 60 snapshots/s (avg 527–543 B) | **27.9 KB/s** at 60 snapshots/s (477 B per snapshot) |
| Round trip / command age | – | ≈ 16 ms / 2 ticks (33 ms) |
| Draw calls (drawn instances) | 3 (151) | not measured in the browser |

Prediction under latency (browser, 4G preset: 60 ms ±20 ms one way, 2 % loss):

| Readout | Prediction on | Prediction off |
|---|---|---|
| Round trip | ≈ 140 ms | ≈ 140 ms |
| Command age | 10 ticks (167 ms) | – |
| Snapshot age | 6 ticks (100 ms) | – |
| Predicted ticks / frame | 10 | 0 |
| Rollbacks / s | ≈ 41 | 0 |
| Misprediction | 0.000 m walking in open space | "(prediction off)" |
| Server-position cube | visibly trails the green cube while moving | hidden |
| Feel | the green cube answers keys immediately | inputs still move the player, one round trip late |

Layout: at both 1600×900 and 1024×768 the player sits in the open band between the HUD panels.

Not verified: the misprediction spike when walking into a bot. Synthetic key events could not steer the
player into a bot reliably, so this experiment is unconfirmed in the browser.

Satellite preset (300 ms one way, ≈ 600 ms round trip): the first Level 3 build fell behind the server
here. The cause was the transport type, not the predict-ahead cap: registered as IPC, the client driver
makes Netcode assume a one-tick round trip and steer the prediction lead from command-age feedback alone,
which snaps back once the error passes 10 ticks. The client driver is now registered as `Socket`, so
Netcode uses the measured RTT (simulator delay included). Above `ClientTickRate.MaxPredictAheadTimeMS`
(500 ms, also Netcode's hard maximum) the client still predicts 500 ms ahead and the rest becomes input
delay. The PlayMode gate `PredictionStaysAheadAtSatelliteLatency` measures a 605 ms RTT with the client
12 ticks ahead of the server.

## Level 4 — Match: 150 players

Measured in the Claude desktop browser (Chromium) on the WebGL2 build at `?level=4&bots=149` (150 players),
LAN link, rebuilt at the final match code. The pane stayed visible throughout; readings taken after it
auto-hid are throttled and were discarded.

| Measure | Browser (WebGL2, LAN link) |
|---|---|
| Replicated objects | 153 / 153 PASS (150 players + 2 flags + 1 match state) |
| Frame time | 6.1 ms at 30 s, 128 s and 295 s (flat; target 16.7 ms met) |
| Tick rate | 60 ticks/s |
| Snapshot bandwidth | 22.5 KB/s at 30 s, 22.7 KB/s at 128 s, 30.4-39 KB/s late in the round; 368-670 B per snapshot |
| Snapshot age / round trip / command age | 1-2 ticks / 12-15 ms / 4 ticks |
| Misprediction | 0.000 m |
| Shots resolved on server | 47-135/s |
| Round | one full round: Red 1 - 1 Blue at 2:54, ended Red 1 - 2 Blue (3 captures), then reset to 0-0 and 5:00 |

4G preset (60 ms +/-20 ms one way, 2 % loss), from the earlier build; no netcode change since:
RTT 119-131 ms, command age 10-12 ticks, misprediction 0.000 m, 41-47 KB/s at about 930-1040 B per
snapshot, frame time 6.0-6.1 ms. **Fire to server confirm: 157 ms** (held press).

Not verified:
- The end-of-round banner was not caught on screen; the reset happened inside the 10 s sampling gap.
- Captures at 150 players are a single run: 3 in the browser, 0 in Editor diagnostics at 149 bots (159
  takes in 300 s). Treat them as rare, not as a rate.
- The fire-to-confirm figure comes from the earlier build (instant synthetic clicks do not register, so a
  held press was used) and was not re-measured on the final one.
- Visuals checked at the earlier build: boxes, bases, scoreboard and clock, white ring, team colours,
  hit flash. Real gamepad aiming was not tried.
- Shots have no lag compensation; the 157 ms is the round trip a hit takes to become real, not a
  correction for it.

Editor, not browser: frame time was flat over 300 s (0.7-1.2 ms) with 149 bots, so the early suspicion of
frame-time growth over minutes was not reproduced.

## Decisions confirmed

- **Stay on Netcode for Entities** (no fallback to Netcode for GameObjects needed).
- Browser "simulator" mode = `ClientServerBootstrap` with an **IPC-only driver constructor**
  (`DemoBootstrap`/`IpcOnlyDriverConstructor`): the server world never tries to open a socket.
- Runtime ghost prefabs (`GhostPrefabCreation.ConvertToGhostPrefab`) work; the creating system must be
  `[CreateAfter(typeof(DefaultVariantSystemGroup))]`.
- SubScenes are fine on Web, so the arena can be authored/baked normally.
- Entities Graphics is not needed: one `RenderMeshInstanced` system covers Editor, native and Web.

## Gotchas found

- Runtime-created entities need `LocalToWorld` added explicitly; `TransformSystemGroup` does not add it.
- `Graphics.RenderMeshInstanced<T>` requires a field named `objectToWorld` — reinterpret `LocalToWorld`.
- A hidden/background tab is throttled to ~0 fps, which makes the 5 s netcode handshake time out.
  Not a bug; the HUD readout only makes sense with the tab visible.
- Web multithreading needs `crossOriginIsolated` (COOP/COEP headers). `Tools/serve.py` sets them;
  itch.io has a "SharedArrayBuffer support" toggle; GitHub Pages cannot set headers.
- First Web IL2CPP build: 32 min, 228 MB development build (201 MB wasm). The **release build is
  13 MB** (9.5 MB wasm + 4.7 MB data, Brotli) and builds in ~11 min.
- **WebGPU works in development builds but non-development (release) builds stall right after
  "Unity WebGPU: ... Vendor" on 6000.6.2f1** — reproduced with Medium and Low stripping and with the
  splash screen off. WebGL2 release builds run fine, so the published build uses WebGL2. Open item:
  retest on the next 6.6 patch / try WebGPU compatibility mode.
- The "Made with Unity" splash (release builds only) blocks frames for several seconds: the netcode
  handshake times out (it recovers by retrying) and the first frame-time/RTT samples are garbage.
  Disabled via `PlayerSettings.SplashScreen.show = false` (Pro license) and stall frames > 250 ms are
  ignored by the HUD average.

## How to reproduce

```powershell
unity test C:\Projects\Portfolio\FlagRush --editor-version 6000.6.2f1 --mode PlayMode
unity build C:\Projects\Portfolio\FlagRush --editor-version 6000.6.2f1 --target WebGL --execute-method FlagRush.Demo.Editor.DemoBuilder.BuildWeb
python Tools\serve.py Builds\Web 8080   # then open http://localhost:8080 with the tab visible
```
