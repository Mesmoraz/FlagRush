# Roadmap: from "networking simulator" to a usable prototype

One build, one URL, a row of tabs. Each tab adds **one** capability on top of the previous one and
keeps the previous tab's readouts, so a visitor can climb the ladder and see the numbers change.
Every tab has the same shape:

1. **What this proves** — one sentence, mapped to a line of the job posting.
2. **Try this** — two or three things to click that produce a visible, numeric consequence.
3. **Readouts** — measurement → meaning, as on the current page.

Navigation is `?level=N` on the same page (tabs in the top bar); the build is shared, only the
scene setup and the control panel differ. Nothing below needs a second build or deploy target.

| Level | Tab | Adds | Proves (posting line) |
|---|---|---|---|
| 1 | **Simulator** (live now) | server + client worlds in one tab, Burst job, replication, readouts | "modern networking" runs in a browser |
| 2 | **Sandbox** | live controls: counts, tick rate, latency/jitter/loss, interpolation, pause/kill | you understand *why* the numbers move |
| 3 | **Player** | you control a predicted ghost with WASD; bots are other ghosts | client-side prediction, inputs, ownership |
| 4 | **Match** | teams, flags, capture rules, scoreboard, XP that survives a disconnect | the game loop; "survive disconnects (like Rust)" |
| 5 | **Online** | the same client connects to a real dedicated server over WebSocket | 100–150 real players is the same code path |
| 6 | **Systems** | physical inventory, production machine, vehicle — one small demo each | the remaining posting bullets |

---

## Level 2 — Sandbox: modify the variables live

**What it proves.** The reader can *cause* every effect the readouts describe. Latency up → snapshot
age up, interpolation hides it; ghosts up → bandwidth up linearly; tick rate down → bytes down, age
up. This is the "oversimplifier" pitch: few knobs, obvious consequences.

**Controls (right-hand panel, IMGUI, each with a one-line "expect:" hint).**

| Control | Range | Mechanism | Readout that reacts |
|---|---|---|---|
| Agents | 0 – 20,000 | client spawns/despawns swarm entities | job time, threads, frame time |
| Ghosts | 0 – 1,000 | server instantiates/destroys ghost prefab instances | bandwidth, snapshot size, draw calls |
| Network tick rate | 10 / 20 / 30 / 60 | `ClientServerTickRate.NetworkTickRate` (server singleton) | snapshots/s, bandwidth, snapshot age |
| Simulated latency | 0 – 300 ms | Unity Transport **simulator pipeline stage** on the IPC driver (`SimulatorUtility.Parameters`, modified at runtime) | round trip, snapshot age |
| Jitter | 0 – 100 ms | same stage | snapshot age variance, interpolation buffer |
| Packet loss | 0 – 30 % | same stage | packet loss %, ghosts still smooth (interpolation) vs stutter |
| Interpolation delay | 0 – 200 ms | `ClientTickRate.InterpolationTimeMS` | snapshot age (grows), smoothness under loss (improves) |
| Ghost send rate | 1 – 60 /s | `GhostPrefabCreation.Config.MaxSendRate` per prefab (rebuild prefab) | bandwidth vs visible choppiness |
| Pause server | toggle | stop `ProbeServerSystem` updates | client keeps interpolating, then freezes; age climbs |
| Kill connection | button | `NetworkStreamRequestDisconnect` | reconnect system heals it; ghosts respawn from snapshots |
| Presets | LAN / Wi-Fi / 4G / bad hotel | one click sets latency+jitter+loss | everything |

**Build notes.**
- Settings live in a `SandboxSettings` singleton component present in *both* worlds; the panel writes
  it, systems read it. No direct system-to-MonoBehaviour calls.
- The simulator stage must be registered when the IPC driver is created (`IpcOnlyDriverConstructor`
  → `NetworkSettings.WithSimulatorStageParameters`) and then changed at runtime via
  `NetworkDriver.ModifySimulatorStageParameters`. Verify both calls exist in Transport 6.6 first.
- Add per-object readouts: bytes per ghost per tick is already computed; show it next to the slider.
- Gate test: assert bandwidth scales ~linearly with ghost count and RTT tracks the latency slider.

**Definition of done.** Every slider changes at least one readout the way its "expect:" hint says;
a 20,000-agent / 500-ghost setting still holds 60 fps on a laptop; the tour doc gets a "Sandbox"
section with three worked experiments.

---

## Level 3 — Player: user input and prediction

**What it proves.** A human controls an entity through the same server-authoritative pipeline, and
client-side prediction makes it feel instant even at 200 ms simulated latency (reuse the Level 2
slider — that is the whole demo).

**Adds.**
- `PlayerInput : IInputComponentData` (move `float2`, sprint, fire) gathered from Input System
  (WASD/mouse, gamepad, later touch).
- A **player ghost prefab** with `GhostOwner`; the server spawns one per connection and the client
  owns it (`Predicted` mode). Movement runs in `PredictedSimulationSystemGroup`, kinematic against
  arena bounds; bots reuse the same movement system with a `BotBrain` feeding the same `PlayerInput`.
- Camera follows the owned ghost. Fire = hitscan ray on the server; hits flash the target.
- **Readouts:** command age (input → server → snapshot round trip in ticks), predicted ticks per
  frame, rollbacks per second, and a "server says you are here" ghost outline vs your predicted
  position (the misprediction distance in metres). A **prediction on/off toggle** so the visitor can
  feel the difference.

**Build notes.** Runtime ghost prefabs again (`GhostPrefabCreation` with `SupportedGhostModes =
All`, `DefaultGhostMode = OwnerPredicted`). Bots are server-only inputs, so they replicate exactly
like humans — that is the 150-player story: 149 bots + you, identical code.

**Definition of done.** WASD moves you; at 200 ms latency with prediction on you feel no lag; the
misprediction readout stays near zero on straight lines and spikes on collisions; the PlayMode gate
drives a scripted input and asserts the owned ghost moved on the server.

---

## Level 4 — Match: capture the flag and state that survives a disconnect

**What it proves.** The game loop, built on the Domain contracts: teams, flags (`ICarryable`),
players (`ICarrier`), `Carries` relationships replicated as ghost fields, a `CaptureTheFlagRuleSet`
stepping the pure `Step(tick, inputs) → events` contract on the server. And the Rust requirement:
XP/score/inventory keyed by `AuthId` survive a disconnect.

**Adds.**
- Arena SubScene (two bases, obstacles, flag stands) baked normally.
- `MatchState` ghost singleton (scores, clock); flag ghosts with `Carried by` owner field; capture,
  drop on death, auto-return.
- Bots with a three-state brain (go for flag / return / hunt) over a flow field or straight-line
  steering — deliberately simple.
- **Profiles:** `AuthId` in `localStorage`/PlayerPrefs, `ISessionRegistry` + `IProfileRepository`
  implemented server-side; a **"disconnect and come back" button** that drops the connection, waits,
  reconnects with the same `AuthId`, and shows XP intact. Same button with a *new* `AuthId` shows a
  fresh profile — the contrast is the demo.
- Readouts: scores, XP, "profile restored from session N", relevancy radius and ghosts culled by it
  (this is what makes 150 players affordable).

**Definition of done.** A full round vs 149 bots at 60 fps; capture works; XP survives the
disconnect button; relevancy readout shows fewer replicated ghosts than exist on the server.

---

## Level 5 — Online: a real dedicated server

**What it proves.** Everything above was the real network stack; swapping the in-process mail slot
for a socket changes nothing in gameplay code. Two browser tabs (or a friend) in the same match.

**Adds.** Headless Windows/Linux server build (`windows-server` module) registering **UDP +
WebSocket** drivers; the web client connects over WebSocket to `wss://…` (needs TLS on the host or a
reverse proxy); a "connect to" field with a default; server tick time and per-connection bandwidth
readouts on the server, mirrored to clients. Hosting: any small VPS or a home machine + tunnel to
start; document the cost.

**Definition of done.** The published page has an "Online" tab that joins a public server; the
Level 1–4 readouts still read true over a real link (loss and jitter are now real).

---

## Level 6 — Systems: the remaining posting bullets, one small demo each

- **Physical inventory:** items are entities in the world (`ICarryable` + `Owns`/`Contains` edges);
  pick up, drop, steal from a downed player; a trunk is just another `IContainer`.
- **Production & crafting:** a placeable machine (`IWorkItem` ticking on the server) that fills its
  hopper; collect, sell for money on the profile.
- **Vehicle:** a drivable ghost with seats (`Controls` edge), fuel, tyre damage, trunk storage —
  kinematic, no physics package.

Each is a tab with its own "try this" and readouts, reusing the sandbox latency slider so every
system is shown working under lag.

---

## Order and effort

| Level | Depends on | Rough size | Risk |
|---|---|---|---|
| 2 Sandbox | 1 | 2–3 days | simulator pipeline stage on IPC (verify API); ghost prefab rebuild for send rate |
| 3 Player | 2 (latency slider) | 3–4 days | prediction tuning; input on Web (pointer lock) |
| 4 Match | 3 | 1–2 weeks | scope creep — keep rules to capture/return/score |
| 5 Online | 4 | 3–5 days + hosting | TLS/WebSocket on the host; server build on Web-less machine |
| 6 Systems | 4 | 2–3 days each | none new |

Levels 2 and 3 are the next two iterations; both are pure additions to `Assets/FlagRush/Demo`
(which gets renamed to `Assets/FlagRush/Demo` when Level 2 lands — it stops being a spike).
