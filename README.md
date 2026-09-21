# FlagRush

A capture-the-flag **networking simulator** built to demonstrate 100–150 player server-authoritative
multiplayer in Unity 6.6 with DOTS (Entities + Netcode for Entities), playable in the browser on WebGPU.

Portfolio game #1. Target skills: modern netcode at scale, state that survives disconnects, physical
inventory, crafting/production loops, vehicles — all built as *simple* systems.

## Status

| Iteration | What | State |
|---|---|---|
| 1 | Engine-free domain contracts (`Assets/FlagRush/Domain`) + tests | done |
| 2 | Web de-risk spike: Entities + Burst + Netcode for Entities in a WebGPU build — all gates pass ([results](docs/m1-web-spike.md)) | done |
| 3 | CTF rule set, bots, prediction/interpolation, relevancy, persistence | next |
| 4 | Dedicated server (UDP + WebSocket) + browser/native clients in one match | planned |
| 5 | itch.io publish | planned |

## Layout

```
Assets/FlagRush/Domain        plain C# contracts: ids, aspects, relationships, simulation step, ports
Assets/FlagRush/Domain.Tests  in-memory fakes + NUnit tests (EditMode)
Assets/Editor/ProjectBootstrap headless package install / save helpers
docs/how-the-prototype-works.md  plain-language tour of what runs today and how the data flows
docs/domain-model.md          the model, the one rule, and how it maps to the requirements
docs/m1-web-spike.md          measured Web gate results and gotchas
```

## Build / test

```powershell
unity open C:\Projects\Portfolio\FlagRush
unity test C:\Projects\Portfolio\FlagRush --mode EditMode --editor-version 6000.6.2f1
```

Requires Unity 6000.6.x with Web, Windows IL2CPP and Windows Dedicated Server modules.
