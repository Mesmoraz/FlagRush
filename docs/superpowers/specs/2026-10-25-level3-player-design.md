# Level 3 — Player: design

Status: approved in conversation 2026-10-25; this is the written spec for review.
Roadmap entry: [docs/roadmap.md — Level 3](../../roadmap.md#level-3--player-user-input-and-prediction).

## Intent

**What this proves (posting line):** client-side prediction, inputs and ownership. A human controls an
entity through the same server-authoritative pipeline as Levels 1–2, and prediction makes it feel
instant at 200 ms simulated latency. Bots drive the *same* input and movement path, which is the
150-player story: 149 bots + you, identical code.

**Decisions taken while designing**

| Question | Decision |
|---|---|
| Scope of this round | Level 3 only; Level 4 (Match) gets its own spec after this ships |
| Firing / hitscan | Deferred to Level 4 (needs lag compensation to be fair; matters for flag drops) |
| View | Top-down follow camera; no pointer lock, no mouse look |
| Prediction mechanism | Netcode for Entities built-in prediction (`IInputComponentData`, `OwnerPredicted` ghost, `PredictedSimulationSystemGroup`); on/off via prediction switching |

**Out of scope:** firing, teams, flags, profiles/`AuthId` (all Level 4); touch input; pointer lock;
physics package.

## 1. Components and data flow

All new code lives in `Assets/FlagRush/Demo/Player/` (same assembly, `FlagRush.Demo`) and follows the
existing Demo patterns: runtime-created ghost prefabs (`GhostPrefabCreation`, as in
`ProbeGhostPrefabSystem`) and static knobs in `Sandbox`.

| Unit | World | Purpose |
|---|---|---|
| `PlayerInput : IInputComponentData` | both | `float2 Move` (camera-relative, length ≤ 1), `bool Sprint`. No fire. |
| `PlayerGhost` (ghost component) | both | `[GhostField] float3 Position`, `[GhostField] float2 Velocity`, `[GhostField] int BotIndex` (−1 = human). Predicted for the owner. |
| `ServerPosition` (ghost component) | both | `[GhostField] float3 Value`, written **only** by the server after separation. Nothing on the client writes it, so on the client it always holds the last snapshot's authoritative position. Drives the outline and misprediction readout. |
| `PlayerGhostPrefab` | both | singleton holding the runtime prefab entity |
| `PlayerGhostPrefabSystem` | both, Initialization | builds the prefab: `PlayerGhost`, `ServerPosition`, `PlayerInput` (+ input buffer), `GhostOwner`, `LocalTransform`, `LocalToWorld`; `SupportedGhostModes = All`, `DefaultGhostMode = OwnerPredicted`, `OptimizationMode = Dynamic` |
| `PlayerSpawnSystem` | server | level ≥ 3 only. One owned ghost per in-game connection (`GhostOwner.NetworkId`, connection's `CommandTarget` → ghost, ghost added to the connection's `LinkedEntityGroup` so it despawns on disconnect). Reconciles bot ghosts (`BotIndex` 0..n−1, no owner) to `Sandbox.Bots`, ≤ 50 per frame. Spawn points on a ring inside the arena. |
| `PlayerInputGatherSystem` | client, `GhostInputSystemGroup` | Input System: WASD/arrows, left stick, Shift / gamepad south = sprint. Converts to world XZ using the camera's flattened forward/right. Writes the owned ghost's `PlayerInput`. |
| `BotBrainSystem` | server, before movement in `PredictedSimulationSystemGroup` | seeded per `BotIndex`: wander toward a random target point in the arena, pick a new one on arrival or every 3–6 s; occasional sprint. Writes `PlayerInput` on bot ghosts. |
| `PlayerMotion.Step(...)` | pure static, Burst-compatible | `(float3 pos, float2 vel, PlayerInput input, float dt, ArenaBounds arena) → (pos, vel)`: acceleration toward `Move * maxSpeed`, friction with zero input, sprint raises max speed, clamp to bounds (velocity component into the wall zeroed). Constants in a `PlayerTuning` struct. |
| `PlayerMovementSystem` | both, `PredictedSimulationSystemGroup`, Burst | for every `Simulate`-tagged player ghost: `PlayerMotion.Step`, then write `PlayerGhost` and `LocalTransform`. On the server this moves humans and bots alike. |
| `PlayerSeparationSystem` | server only, after movement | pushes overlapping players apart (circle radius 0.6 m, spatial hash on a 2 m grid), then writes `ServerPosition`. The client cannot predict this; that is deliberate — walking into bots is what makes misprediction visible. |

**Data flow per tick:** keys → `PlayerInput` → input command buffer → server → `BotBrain` (bots) →
`PlayerMovement` → `PlayerSeparation` → `ServerPosition` → snapshot → client rolls back to the
snapshot tick and re-simulates buffered inputs in `PredictedSimulationSystemGroup`.

**Arena:** `ArenaBounds` = a square of ±30 m on XZ, matching the current scene's floor. Stored as a
constant in `PlayerTuning` for Level 3; Level 4 replaces it with baked arena data.

## 2. What you see

**Camera.** `PlayerCameraFollow` (MonoBehaviour added to the main camera by `DemoSceneSetup`) is
active only at level ≥ 3. Each `LateUpdate` it reads the owned ghost's `LocalToWorld` from the client
world and damps toward `target + (0, 18, −14)` looking at the target (≈ 50° down). Levels 1–2 keep
today's fixed camera. Because the camera never yaws, camera-relative input means W = screen up.

**Rendering.** `InstancedRenderSystem` gains three queries, one instanced draw per material:

| Query | Look |
|---|---|
| owned player (`GhostOwnerIsLocal`) | bright green cube, scale (1, 1.6, 1) |
| other players (bots, other humans) | pale grey cube, scale (1, 1.6, 1) |
| server outline (owned player's `ServerPosition`) | 35 %-alpha green cube, same scale |

Player scale requires writing a scaled `LocalToWorld` matrix (or non-uniform `PostTransformMatrix`);
the render system builds the matrix from position directly for players rather than reading
`LocalToWorld`, which keeps it independent of the transform system's update order.

**Prediction toggle.** A HUD checkbox sets `Sandbox.PredictionEnabled`. `PredictionToggleSystem`
(client) detects a change and enqueues the owned ghost into `GhostPredictionSwitchingQueues`
(`ConvertToInterpolatedQueue` / `ConvertToPredictedQueue`). Inputs keep flowing either way; with
prediction off you see your own movement one RTT + interpolation delay late.

**PLAYER HUD section** (same row style: measurement → one-line meaning):

| Readout | Meaning | Source |
|---|---|---|
| Command age | how long your input takes to be acknowledged | client tick − last tick the server acknowledged for our command stream. **Verify** the Netcode 6.6 field; fallback: server writes the last input tick it consumed into a ghost field on the player |
| Predicted ticks / frame | how far ahead of the server the client simulates | count of `PredictedSimulationSystemGroup` iterations per frame (system at the group's start increments a counter) |
| Rollbacks / s | how often a new snapshot forced a re-simulation | frames whose first predicted tick is earlier than the previous frame's last predicted tick. **Verify** whether Netcode exposes this directly; this definition is the fallback |
| Misprediction | how wrong the client was about where you are | distance between `ServerPosition` and our predicted position recorded for that snapshot's tick (64-entry ring buffer keyed by tick on the client) |
| Players | server count / client count | as today's ghost count rows |

The Level 2 controls stay (latency/jitter/loss presets, tick rate, interpolation, freeze/kill). The
sandbox panel adds a **Bots** slider (0–149) and the **Prediction** checkbox.

**Try this (HUD hint text):** set latency to 200 ms and walk; uncheck Prediction and walk again; walk
into a bot and watch Misprediction spike.

## 3. Level wiring, tests, definition of done

**Level wiring**
- `DemoConfig.MaxLevel = 3`; `?level=3` / `--level=3`. New knob `bots` (default 31, clamp 0–149),
  read like `agents`/`ghosts`, seeds `Sandbox.Bots`.
- Gating: the player prefab is created in every world (cheap, keeps ghost type tables identical);
  `PlayerSpawnSystem`, `BotBrainSystem`, `PlayerCameraFollow`, `PredictionToggleSystem` and the
  PLAYER HUD section run only at `DemoConfig.Level >= 3`. Levels 1–2 render and behave exactly as
  today, so `DemoGateTests`' `DrawnInstances == agents + ghosts` assertion still holds.
- Level 3 startup defaults (when not given in the URL): agents 0, probe ghosts 0, bots 31.
- HUD title: "level 3 · player"; top-bar tabs for levels 1–3.
- `DemoConfig.OverrideLevel(int)` (test hook) resets the cached level, because PlayMode tests cannot
  pass `--level`.

**EditMode tests — `PlayerMotionTests`** (in `FlagRush.Demo.Tests`, pure, no worlds):
- accelerates toward the input direction
- caps at max speed; sprint raises the cap
- decelerates to rest with zero input
- clamps to arena bounds and zeroes velocity into the wall
- deterministic: identical input sequences give bit-identical positions

**PlayMode tests — `PlayerGateTests`** (level 3 via `OverrideLevel`):
1. Exactly one owned predicted ghost on the client; server has `1 + Sandbox.Bots` player ghosts.
2. Scripted `PlayerInput { Move = (1, 0) }` for 60 ticks → server `PlayerGhost.Position.x` increased
   by > 2 m.
3. At 100 ms latency with prediction on: the client's predicted position moves within 2 ticks of the
   input while `ServerPosition` still lags.
4. Prediction off → the owned ghost no longer has `PredictedGhost`; on again → it has it.
5. `Sandbox.Bots = 149` → 150 player ghosts replicate to the client; frame time logged.
- Existing `DemoGateTests` pass unchanged.

**Definition of done**
- WASD moves you in the browser build at `?level=3`.
- At 200 ms latency, prediction on feels immediate; prediction off shows obvious lag.
- Misprediction ≈ 0 m in open space, spikes when pushing into bots.
- 1 + 149 bots hold 60 fps in the WebGL2 build on the dev machine (record the number in
  `docs/m1-web-spike.md` style).
- `docs/how-the-prototype-works.md` gains a "Player" section with three try-this experiments;
  README status row 3 → done; roadmap Level 3 marked live.

## Risks and early checks

| Risk | Check first in the plan |
|---|---|
| Netcode 6.6 API names for prediction switching, command ack tick, rollback info | read the installed package source under `Library/PackageCache/com.unity.netcode@*` before writing those systems |
| Runtime prefab with `OwnerPredicted` + input buffer via `GhostPrefabCreation` (no baking) | spike it as the first implementation task; the PlayMode test 1 is its gate |
| Web input focus (canvas must have focus for keys) | the web template already focuses the canvas on click; add "click the game to control" to the HUD hint |
| 150 predicted-capable ghosts on Web | only the owned ghost is predicted; bots are interpolated on the client, so cost ≈ today's 150-ghost sandbox |
