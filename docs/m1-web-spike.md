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
| custom render bridge (`Graphics.RenderMeshInstanced` from `LocalToWorld`) | PASS, 158 instances | PASS after fix (see below) |

## Decisions confirmed

- **Stay on Netcode for Entities** (no fallback to Netcode for GameObjects needed).
- Browser "simulator" mode = `ClientServerBootstrap` with an **IPC-only driver constructor**
  (`SpikeBootstrap`/`IpcOnlyDriverConstructor`): the server world never tries to open a socket.
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
- First Web IL2CPP build: 32 min, 228 MB development build (201 MB wasm). Release + compression will
  shrink this a lot; Wasm size will be the main thing to watch for itch.io.

## How to reproduce

```powershell
unity test C:\Projects\Portfolio\FlagRush --editor-version 6000.6.2f1 --mode PlayMode
unity build C:\Projects\Portfolio\FlagRush --editor-version 6000.6.2f1 --target WebGL --execute-method FlagRush.Spike.Editor.SpikeBuilder.BuildWeb
python Tools\serve.py Builds\Web 8080   # then open http://localhost:8080 with the tab visible
```
