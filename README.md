# FlagRush

**▶ Play the prototype: https://mesmoraz.github.io/FlagRush/** (Chrome/Edge/Firefox; ~13 MB; WebGL2 — see the WebGPU note in [docs/m1-web-spike.md](docs/m1-web-spike.md))

A capture-the-flag **networking simulator** built to demonstrate 100–150 player, server-authoritative
multiplayer in Unity 6.6 with DOTS (Entities + Netcode for Entities), playable in the browser.

Portfolio game #1. Target skills: modern netcode at scale, state that survives disconnects, physical
inventory, crafting/production loops, vehicles — all built as *simple* systems.

## What you're looking at

The browser tab runs **two ECS worlds at once**: a *server* world that owns the truth and a *client*
world that displays it, joined by an in-process transport carrying the same packets a real socket
would. 150 blue agents are moved by a Burst-compiled job on worker threads; 8 orange objects are moved
by the server and reach the client only through network snapshots. The panel explains every number.
[Plain-language tour →](docs/how-the-prototype-works.md)

Open the **Sandbox** tab (`?level=2`) for live controls: latency/jitter/loss, tick rate, object counts, interpolation, freeze/kill.

## What the numbers mean

| Panel row | Typical value | It means |
|---|---|---|
| Burst-compiled job | YES | the simulation code runs as native WebAssembly, not interpreted C# |
| Worker threads used | 6 (5 workers + main) | the job is split across CPU cores; the main thread is not the bottleneck |
| Job time | 0.02 ms / 150 agents | ≈ 0.1 µs per agent; 10,000 agents would cost ≈ 1 ms |
| Frame time | 16.7 ms (60 fps) | both worlds, the job and rendering fit in one frame |
| Connection | in-game over IPC | the client completed the netcode handshake and asked for game state |
| Replicated objects | 8 / 8 | every server-owned object exists on the client |
| Server → client bandwidth | 2.6 KB/s · 60 snapshots/s · 44 B each | ≈ 5.5 bytes per object per tick; 150 such objects ≈ 48 KB/s |
| Snapshot age | 2–5 ticks (33–83 ms) | the client shows the server's world this far in the past (transit + interpolation buffer) |
| Round trip | ~15 ms | client → server → client; on the internet this is your ping |
| Packet loss | 0% | snapshots that never arrived (IPC never drops; real networks do) |
| Tick rate | 60 sim / 60 net | the server simulates and sends 60 times a second |
| SubScene entities | 3 / 3 | baked level data streams in on the web, so the arena can be a normal Unity scene |
| Draw calls | 2 for 158 objects | instanced rendering straight from entity transforms |

Values above were measured in Chromium on a 20-core Windows machine; see
[docs/m1-web-spike.md](docs/m1-web-spike.md) for the full gate results and gotchas.

## Status

| Iteration | What | State |
|---|---|---|
| 1 | Engine-free domain contracts (`Assets/FlagRush/Domain`) + tests | done |
| 2 | Web de-risk spike: Entities + Burst + Netcode for Entities in the browser — all gates pass, published | done |
| 2b | Sandbox: live knobs for counts, tick rate, link latency/jitter/loss, interpolation, freeze/kill ([roadmap](docs/roadmap.md)) | done |
| 3 | Player: WASD-controlled predicted ghost, bots on the same input path | next |
| 4 | Dedicated server (UDP + WebSocket) + browser/native clients in one match | planned |
| 5 | itch.io publish | planned |

## Layout

```
Assets/FlagRush/Domain          plain C# contracts: ids, aspects, relationships, simulation step, ports
Assets/FlagRush/Domain.Tests    in-memory fakes + NUnit tests (EditMode)
Assets/FlagRush/Demo           the web prototype: bootstrap, systems, HUD, PlayMode gate test, build scripts
Assets/WebGLTemplates/FlagRush  the web page (responsive canvas + COOP/COEP service worker for threads)
Assets/Editor/ProjectBootstrap  headless package install / save helpers
Tools/serve.py                  local web server with the COOP/COEP headers
Tools/deploy-pages.ps1          pushes Builds/Web to the gh-pages branch
docs/how-the-prototype-works.md plain-language tour of what runs today and how the data flows
docs/domain-model.md            the model, the one rule, and how it maps to the requirements
docs/m1-web-spike.md            measured Web gate results and gotchas
docs/roadmap.md                 the level-by-level plan from simulator to usable prototype
```

## Build / test

```powershell
unity test  C:\Projects\Portfolio\FlagRush --editor-version 6000.6.2f1 --mode EditMode   # domain contracts
unity test  C:\Projects\Portfolio\FlagRush --editor-version 6000.6.2f1 --mode PlayMode   # the prototype gate
unity build C:\Projects\Portfolio\FlagRush --editor-version 6000.6.2f1 --target WebGL --execute-method FlagRush.Demo.Editor.DemoBuilder.BuildWebGl2   # BuildWeb = WebGPU (release stalls on 6.6.2)
python Tools\serve.py Builds\Web 8080                                                    # http://localhost:8080
.\Tools\deploy-pages.ps1                                                                 # publish Builds/Web
```

Requires Unity 6000.6.x with the Web, Windows IL2CPP and Windows Dedicated Server modules.
