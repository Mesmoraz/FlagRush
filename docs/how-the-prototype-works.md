# How the prototype works (plain-language tour)

This page explains what is on screen right now, what is happening underneath, and how the data is
organised — assuming you have never used Unity's DOTS or its networking before.

If you only read one paragraph: **the browser is running a tiny multiplayer game server and a game
client at the same time, in the same program, and they talk to each other through an in-memory
mailbox instead of the internet.** The blue cubes prove the maths can run on many CPU cores in a
browser; the orange cubes prove the server-to-client networking works; the small panel proves the
level-loading works. Nothing here is the actual game yet — it is the plumbing test the game will be
built on.

---

## 1. What you are looking at

| On screen | What it is | Who owns it |
|---|---|---|
| **150 blue cubes** drifting in loose circles | "swarm agents": throwaway objects whose only job is to be moved by a piece of code compiled with Burst and run on several CPU threads | the **client** world (local only, never sent over the network) |
| **8 orange cubes** in a neat ring | "ghosts": objects that the **server** moves and the client only *receives*. Their position arrives through the network layer every tick | the **server** world; the client holds a copy |
| **The grey panel** top-left | a readout of the three experiments: PASS/FAIL for Burst + threads, networking, and level loading, plus live numbers (ticks, ghost counts, round-trip time) | a plain Unity `MonoBehaviour` that just prints numbers |

The camera is fixed. There is no input, no goal, no score. That is deliberate: every line of code in
this prototype exists to answer one question — *does this technology work in a browser?* — and the
answer is yes.

---

## 2. The big idea: two worlds in one program

Normal single-player Unity has one "world" — one big bag of objects. Unity's networking framework
(**Netcode for Entities**) instead creates **two separate worlds inside the same program**:

- **ServerWorld** — the referee. It owns the truth: where things are, who scored. Nobody else may
  change that.
- **ClientWorld** — the spectator/player. It shows you what the server says is happening, and it
  sends the server your inputs.

In a real deployment these two live on different machines and talk over the internet. In this
prototype they live in the same browser tab and talk through **IPC** ("inter-process
communication" — think of it as a mail slot between two rooms in the same building). The messages
are *identical* to the ones that would go over the internet; only the delivery is shortcut.

Why do it this way? Because a web page **cannot open a listening network port** — browsers forbid
it. So a browser can never be a server for other people. But a browser *can* run a server world for
itself, which gives us a "networking simulator" that behaves exactly like the real thing, right down
to the round-trip time you see in the panel (~15 ms). Later, the real dedicated server will be a
normal Windows/Linux program running this same ServerWorld code with the mail slot swapped for a
real socket.

---

## 3. How the data is organised (ECS in five minutes)

Unity's DOTS stores game data in a style called **ECS — Entities, Components, Systems**. The easiest
way to picture it is a spreadsheet.

- An **Entity** is just an ID number. A row. It has no behaviour and no data by itself.
- A **Component** is a small chunk of data attached to an entity. A cell — or rather, a column that a
  row may or may not have. Components are plain structs like `Position { x, y, z }`.
- A **System** is a function that runs every frame and says "give me every row that has columns A
  and B, and let me update them". Systems hold the *behaviour*; entities and components hold only
  *data*.

That separation is the whole trick. Because the data is packed tightly in memory with no objects or
pointers, a system can hand thousands of rows to many CPU cores at once, and a network layer can copy
just the columns that changed.

### The components this prototype uses

| Component | Attached to | Fields | Plain meaning |
|---|---|---|---|
| `SwarmAgent` | blue cubes | `Phase`, `Radius`, `Speed` | the recipe for this cube's circular path |
| `LocalTransform` | every cube | `Position`, `Rotation`, `Scale` | where it is (Unity's standard transform component) |
| `LocalToWorld` | every cube | a 4×4 matrix | the same position packed the way the GPU wants it. Unity's transform system fills this in from `LocalTransform` each frame |
| `SpikeGhostState` | orange cubes | `ServerTick`, `Position`, `Index` | the **replicated** data. Fields marked `[GhostField]` are the ones the server sends to clients |
| `SubSceneMarker` | 3 invisible entities | `Value` | proof that the level file ("SubScene") loaded — nothing more |
| `NetworkId`, `NetworkStreamInGame` | the *connection* entity | id, flag | Netcode's own bookkeeping: "this connection exists" and "this connection wants game data" |

Note what is **not** here: there is no `Player`, `Flag`, or `Team`. That is on purpose. The game's real
data model lives in `Assets/FlagRush/Domain` as abstract contracts (see
[domain-model.md](domain-model.md)); this prototype only tests the engine plumbing.

### The systems, and which world they run in

Systems are tagged with the world(s) they belong to. Same code, different rooms.

| System | Runs in | What it does every frame |
|---|---|---|
| `GoInGameSystem` | client **and** server | finds any new connection and stamps it `NetworkStreamInGame` — "yes, send me game state". Without this, nothing replicates |
| `SpikeGhostPrefabSystem` | client **and** server, once at startup | builds the *template* for an orange cube in code and registers it with Netcode. Both worlds must build the identical template or the client cannot decode what the server sends |
| `SpikeServerSystem` | server only | once a connection is in-game, creates 8 orange cubes from the template; then every tick moves them around a ring and writes the tick number into each one |
| `SwarmSystem` + `SwarmMoveJob` | client only | schedules a **Burst-compiled job** that moves all 150 blue cubes in parallel across worker threads, and records which threads actually did the work |
| `SpikeClientStatsSystem` | client only | reads the connection state, current tick, ghost count and round-trip time; also copies each orange cube's replicated `Position` into its `LocalTransform` so it can be drawn |
| `SubSceneProbeSystem` | client **and** server | counts `SubSceneMarker` entities (expects 3) |
| `InstancedRenderSystem` | client only (presentation) | collects every cube's `LocalToWorld` matrix and draws them all in two GPU calls (one blue, one orange) |

---

## 4. One frame, step by step

Here is what happens ~60 times a second, in order.

**In ServerWorld**
1. Netcode receives anything the client sent (nothing yet — there are no inputs in this prototype).
2. `GoInGameSystem` stamps new connections.
3. `SpikeServerSystem` advances the 8 orange cubes one step around the ring and stamps the current
   server tick into `SpikeGhostState.ServerTick`.
4. Netcode's **snapshot** system looks at every ghost, compares it with what it last sent, and writes
   the *changes* into a compact packet. Position is "quantised" (rounded to 1/100th of a unit) so it
   takes fewer bits. The packet goes into the IPC mail slot.

**In ClientWorld**
1. Netcode reads the mail slot, decodes the snapshot, and updates the client's copies of the 8 orange
   cubes. If a ghost is new, it instantiates it from the template built at startup.
2. `SwarmSystem` schedules `SwarmMoveJob`. Burst has compiled that job to native WebAssembly; the job
   system splits the 150 cubes into chunks and hands them to worker threads. When it finishes, the
   system counts how many distinct threads touched the data (the panel showed 6).
3. `SpikeClientStatsSystem` copies replicated positions into transforms and refreshes the numbers on
   the panel.
4. Unity's transform system converts every `LocalTransform` into a `LocalToWorld` matrix.
5. `InstancedRenderSystem` hands those matrices to the GPU: one draw call for all blue cubes, one for
   all orange.

**Then** the HUD `MonoBehaviour` draws the panel from the shared `SpikeStats` numbers.

The panel line `client: serverTick=320 ... newestReplicatedTick=315` is this loop made visible: the
server is at tick 320, and the newest orange-cube data the client has decoded was written at tick 315.
That 2–5 tick gap is the cost of packing, sending and unpacking — the same lag a real client sees.

---

## 5. Why each experiment mattered (and what "PASS" means)

**(a) Burst + worker threads.** Burst is Unity's compiler that turns C# jobs into fast native code.
On the web this has to become WebAssembly, and worker threads only exist if the page is served with
two special HTTP headers (COOP/COEP) that unlock `SharedArrayBuffer`. The job carries a tiny trap: a
call that Burst *deletes* when it compiles the job. If that call ever runs, we know the slow managed
version ran instead. PASS = the trap never fired *and* more than one thread touched the data. This is
what makes "many background tasks in the browser" possible.

**(b) Netcode over IPC.** PASS = the client reached the *in-game* state, received all 8 ghosts, and
the tick numbers inside them keep going up. This proves the entire replication pipeline
(templates, snapshots, quantisation, interpolation bookkeeping) runs in a browser with the server
world hosted in-process.

**(c) SubScene.** Unity levels for ECS are authored as "SubScenes" and pre-baked into entity files
at build time; at runtime they stream in. Community reports said this was flaky on web. PASS = the
three marker entities showed up in both worlds, loaded from `StreamingAssets/EntityScenes/...`.

**Rendering.** Unity's normal ECS renderer ("Entities Graphics") does not support the web at all, so
the prototype draws cubes itself with `Graphics.RenderMeshInstanced` — one draw per colour. This is
simpler than the official path and works everywhere, so it will stay.

---

## 6. How this connects to the real game

- The **Domain** layer (`Assets/FlagRush/Domain`) defines *what the game's data means*: ids, aspects
  like `ICarryable`/`ICarrier`, relationships as `(Kind, Subject, Object)` rows, and a pure
  `Step(tick, inputs) → events` simulation. It has no Unity in it and is fully unit-tested.
- This **Spike** proves *where that data can live*: as ECS components, replicated by Netcode, computed
  by Burst jobs, drawn by the instanced renderer — in a browser.
- The next iteration marries the two: a `Flag` becomes an entity with `ICarryable`-shaped components,
  "avatar 7 carries flag 12" becomes a `Carries` relationship row that is replicated as a ghost field,
  and the capture-the-flag rules become a server system stepping the Domain contract each tick.

---

## 7. Glossary

| Word | Meaning here |
|---|---|
| **Entity** | an ID number that components attach to; a row |
| **Component** | a small plain-data struct attached to an entity; a column |
| **System** | code that runs each frame over all entities with a given set of components |
| **World** | a separate container of entities + systems. ServerWorld and ClientWorld never share entities |
| **Tick** | the server's fixed-rate heartbeat (60/s). All simulation is described "at tick N" |
| **Ghost** | an entity whose marked fields the server copies to clients every tick |
| **Snapshot** | one tick's packet of ghost changes |
| **Quantisation** | rounding a float to a fixed step so it needs fewer bits on the wire |
| **IPC** | the in-memory mail slot between two worlds in one process; stands in for a real socket |
| **Burst** | Unity's compiler that turns job code into fast native (or WebAssembly) code |
| **Job** | a unit of work the engine can spread across CPU worker threads |
| **SubScene** | an ECS level file, baked at build time, streamed in at runtime |
| **COOP/COEP** | HTTP headers a web page needs before the browser allows multithreading |

## 8. Where things live

```
Assets/FlagRush/Spike/
  SpikeBootstrap.cs          creates the two worlds; forces IPC-only networking
  SpikeComponents.cs         the components in the table above
  GoInGameSystem.cs          "send me game data" stamp
  SpikeGhostPrefabSystem.cs  builds the orange-cube template in both worlds
  SpikeServerSystem.cs       server: spawns + moves the 8 ghosts each tick
  SwarmSystem.cs             client: the Burst job that moves the 150 blue cubes
  SpikeClientStatsSystem.cs  client: connection/tick/ghost/RTT readout
  SubSceneProbeSystem.cs     counts baked entities
  InstancedRenderSystem.cs   draws everything (no Entities Graphics)
  SpikeHud.cs                the grey panel
  SpikeStats.cs              the shared numbers the panel reads
  Tests/SpikeGateTests.cs    the same PASS/FAIL checks as an automated test
  Editor/                    headless scene setup + Web/Windows build scripts
Tools/serve.py               local web server with the COOP/COEP headers
docs/m1-web-spike.md         measured results and the gotchas we hit
docs/domain-model.md         the abstract game data model this will be built on
```
