# Level 3 Player Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** At `?level=3` the visitor drives a client-predicted player ghost with WASD among up to 149 server-driven bots, with a prediction on/off toggle and readouts that show what prediction is doing.

**Architecture:** Netcode for Entities built-in prediction: `PlayerInput : IInputComponentData` on a runtime-built `OwnerPredicted` ghost, movement in `PredictedSimulationSystemGroup` through one pure `PlayerMotion.Step`. Bots are unowned player ghosts whose `PlayerInput` is written by a server brain, so humans and bots share one movement path. Everything new is gated on `DemoConfig.Level >= 3`; levels 1–2 are untouched.

**Tech Stack:** Unity 6000.6.2f1, Entities 6.6, Netcode for Entities 6.6 (`com.unity.netcode@63048e3f1c24`), Burst, Input System 1.20, NUnit via Unity Test Framework, IMGUI HUD.

**Spec:** [docs/superpowers/specs/2026-10-25-level3-player-design.md](../specs/2026-10-25-level3-player-design.md)

## Global Constraints

- All new runtime code in `Assets/FlagRush/Demo/Player/`, namespace `FlagRush.Demo`, assembly `FlagRush.Demo`.
- Tests in `Assets/FlagRush/Demo/Tests/` (assembly `FlagRush.Demo.Tests`, namespace `FlagRush.Demo.Tests`). Pure tests are `[Test]` (run in EditMode); world tests are `[UnityTest]` (PlayMode).
- Ghost prefabs are built at runtime with `GhostPrefabCreation.ConvertToGhostPrefab`, never baked (same as `ProbeGhostPrefabSystem`).
- Player prefab: `SupportedGhostModes = GhostModeMask.All`, `DefaultGhostMode = GhostMode.OwnerPredicted`.
- Arena: square ±30 m on XZ. Player radius 0.6 m. Player visual scale (1, 1.6, 1).
- Bots: `Sandbox.Bots` range 0–149; URL/arg knob `bots`, default 31.
- Level 3 startup defaults when not given in URL/args: agents 0, ghosts 0, bots 31.
- Camera offset from target: (0, 18, −14), looking at target.
- Prediction switch transition: `TransitionDurationSeconds = 0.25f`.
- Levels 1–2 must render and behave exactly as before; `DemoGateTests` passes unchanged.
- Unity batch commands need the Editor closed on this project (project lock).
- Test commands:
  - EditMode: `unity test C:\Projects\Portfolio\FlagRush --editor-version 6000.6.2f1 --mode EditMode`
  - PlayMode: `unity test C:\Projects\Portfolio\FlagRush --editor-version 6000.6.2f1 --mode PlayMode`
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Reconnect at level 3** (Kill connection button, tick-rate change): the old owned ghost must despawn and exactly one new owned ghost must appear; camera and readouts retarget. Test added in Task 3.
2. **Bots slider down to 0 and back up:** reconcile removes only bot ghosts (`BotIndex >= 0`), never a human's. Test added in Task 5.
3. **Diagonal input (W+D) and stick overshoot:** `Move` length is clamped to ≤ 1, so diagonals are not faster. Test added in Task 4.
4. **Prediction checkbox flipped again during a running transition:** Netcode blocks re-conversion until the transition finishes; the toggle system must keep retrying until the ghost's mode matches the checkbox. Test added in Task 6.
5. **Level 1/2 after level 3 in the same Editor session (test order):** `OverrideLevel(null)` restores sandbox defaults; no player ghosts are spawned or drawn. Test added in Task 2.

---

### Task 1: Pure movement step

**Files:**
- Create: `Assets/FlagRush/Demo/Player/PlayerMotion.cs`
- Test: `Assets/FlagRush/Demo/Tests/PlayerMotionTests.cs`

**Interfaces:**
- Produces:
  - `public struct ArenaBounds { public float2 Min; public float2 Max; public static ArenaBounds Default => new() { Min = -30f, Max = 30f }; }`
  - `public static class PlayerTuning { public const float MaxSpeed = 6f; public const float SprintSpeed = 10f; public const float Acceleration = 40f; public const float Friction = 30f; public const float Radius = 0.6f; public const float Height = 1.6f; }`
  - `public struct PlayerInput : IInputComponentData { public float2 Move; public bool Sprint; }` (in `PlayerComponents.cs`, created here because Step consumes it)
  - `public static class PlayerMotion { public static void Step(ref float3 position, ref float2 velocity, in PlayerInput input, float dt, in ArenaBounds arena); }` — `[BurstCompile]`-safe (no managed types). Y of position is left unchanged.

- [ ] **Step 1: Write the failing tests** in `PlayerMotionTests` (`[Test]`, helper `Run(PlayerInput, int ticks, float dt = 1f/60f)` returning `(float3 pos, float2 vel)` from origin):
  - `AcceleratesTowardInput`: Move (1,0), 5 ticks → `vel.x > 0`, `pos.x > 0`, `pos.z == 0`.
  - `CapsAtMaxSpeed`: Move (1,0), 120 ticks → `math.length(vel)` within 1e-4 of `PlayerTuning.MaxSpeed`.
  - `SprintRaisesCap`: Move (1,0), Sprint, 120 ticks → length within 1e-4 of `PlayerTuning.SprintSpeed`.
  - `ZeroInputComesToRest`: 120 ticks at Move (1,0), then 60 ticks zero input → `vel == float2.zero`.
  - `ClampsToArenaAndZeroesVelocityIntoWall`: Move (1,0), 600 ticks → `pos.x == 30f - PlayerTuning.Radius`, `vel.x == 0`.
  - `IsDeterministic`: same seeded sequence of 300 random inputs (fixed `Unity.Mathematics.Random(42)`) run twice → positions bit-identical (`Assert.AreEqual` on floats, no tolerance).
- [ ] **Step 2: Run EditMode tests** — expect compile failure (`PlayerMotion` not defined).
- [ ] **Step 3: Implement** `PlayerComponents.cs` (only `PlayerInput` for now) and `PlayerMotion.Step`: target velocity = `Move * (Sprint ? SprintSpeed : MaxSpeed)`; move velocity toward target by `Acceleration * dt` when `Move` non-zero, else toward zero by `Friction * dt` (never overshoot); integrate; clamp XZ to `[Min + Radius, Max - Radius]` and zero the velocity component that pointed into the wall.
- [ ] **Step 4: Run EditMode tests** — all six PASS; existing Domain tests still PASS.
- [ ] **Step 5: Commit** — `feat(player): pure movement step with arena clamp`

---

### Task 2: Level 3 config, sandbox knobs, test hook

**Files:**
- Modify: `Assets/FlagRush/Demo/DemoConfig.cs` (MaxLevel, `Bots`, `OverrideLevel`)
- Modify: `Assets/FlagRush/Demo/Sandbox.cs` (new fields, `ApplyLevelDefaults`)
- Test: `Assets/FlagRush/Demo/Tests/LevelConfigTests.cs`

**Interfaces:**
- Produces:
  - `DemoConfig.MaxLevel = 3`; `DemoConfig.DefaultBots = 31`; `public static int Bots` (knob `bots`, clamp 0..`Sandbox.MaxBots`).
  - `DemoConfig.IsPlayerLevel => Level >= 3`.
  - `public static void DemoConfig.OverrideLevel(int? level)` — sets the cached level (null = re-read URL/args on next access) and calls `Sandbox.ApplyLevelDefaults()`.
  - `Sandbox.MaxBots = 149`; `public static int Bots`; `public static bool PredictionEnabled = true`.
  - `public static void Sandbox.ApplyLevelDefaults()` — at level ≥ 3: Agents/Ghosts = 0 unless the URL/arg supplied them, Bots = `DemoConfig.Bots`; below 3: Agents/Ghosts = `DemoConfig.Agents/Ghosts`, Bots = 0. Always sets `PredictionEnabled = true`.
  - `DemoConfig` needs to know whether a knob was supplied: add `static bool Supplied(string key)` (true when `FromUrl ?? FromArgs` is non-null); `Agents`/`Ghosts` fall back to 0 at level ≥ 3 via `ApplyLevelDefaults`, not by changing their own defaults.
  - `Sandbox` static initializers call `ApplyLevelDefaults()` once (replace the current `= DemoConfig.Agents` / `= DemoConfig.Ghosts` initializers with a static constructor).

- [ ] **Step 1: Write the failing tests** (`[Test]`, `[TearDown]` calls `DemoConfig.OverrideLevel(null)`):
  - `Level3DefaultsClearAgentsAndGhostsAndSeedBots`: `OverrideLevel(3)` → `Sandbox.Agents == 0`, `Sandbox.Ghosts == 0`, `Sandbox.Bots == 31`, `DemoConfig.IsPlayerLevel`.
  - `OverrideNullRestoresLevel1Defaults` (Review Focus 5): `OverrideLevel(3)`, then `OverrideLevel(null)` → `DemoConfig.Level == 1`, `Sandbox.Agents == DemoConfig.DefaultAgents`, `Sandbox.Ghosts == DemoConfig.DefaultGhosts`, `Sandbox.Bots == 0`, `!DemoConfig.IsPlayerLevel`.
  - `PredictionResetsToOn`: set `Sandbox.PredictionEnabled = false`, `OverrideLevel(3)` → true.
- [ ] **Step 2: Run EditMode** — expect compile failure.
- [ ] **Step 3: Implement** the interfaces above.
- [ ] **Step 4: Run EditMode** — PASS. Run PlayMode — `DemoGateTests` both PASS unchanged.
- [ ] **Step 5: Commit** — `feat(player): level 3 config, bots knob, level override for tests`

---

### Task 3: Player ghost, spawn per connection, predicted movement

The riskiest task (runtime `OwnerPredicted` prefab with an input buffer, no baking). Do it before anything visual.

**Files:**
- Modify: `Assets/FlagRush/Demo/Player/PlayerComponents.cs`
- Create: `Assets/FlagRush/Demo/Player/PlayerGhostPrefabSystem.cs`
- Create: `Assets/FlagRush/Demo/Player/PlayerSpawnSystem.cs`
- Create: `Assets/FlagRush/Demo/Player/PlayerMovementSystem.cs`
- Modify: `Assets/FlagRush/Demo/DemoStats.cs` (player counters)
- Test: `Assets/FlagRush/Demo/Tests/PlayerGateTests.cs`

**Interfaces:**
- Consumes: `PlayerInput`, `PlayerMotion.Step`, `ArenaBounds.Default` (Task 1); `DemoConfig.IsPlayerLevel`, `OverrideLevel` (Task 2).
- Produces:
  - `[GhostComponent] public struct PlayerGhost : IComponentData { [GhostField(Quantization = 1000, Smoothing = SmoothingAction.InterpolateAndExtrapolate)] public float3 Position; [GhostField(Quantization = 1000)] public float2 Velocity; [GhostField] public int BotIndex; }` — `BotIndex = -1` for humans.
  - `[GhostComponent(OwnerSendType = SendToOwnerType.All)] public struct ServerPosition : IComponentData { [GhostField(Quantization = 1000)] public float3 Value; }`
  - `public struct PlayerGhostPrefab : IComponentData { public Entity Value; }`
  - `PlayerGhostPrefabSystem` (Client|Server simulation, `InitializationSystemGroup`, `[CreateAfter(typeof(DefaultVariantSystemGroup))]`): prefab has `PlayerGhost`, `ServerPosition`, `PlayerInput`, `GhostOwner`, `LocalTransform`, `LocalToWorld`; `Importance = 2000`, `OptimizationMode = Dynamic`. Created at every level. The input buffer is added by `ConvertToGhostPrefab` when the prefab contains an `IInputComponentData` — if it is not, add `InputBufferData<PlayerInput>` explicitly and note it in the commit.
  - `PlayerSpawnSystem` (Server, `SimulationSystemGroup`, `[UpdateAfter(typeof(GoInGameSystem))]`, early-out unless `DemoConfig.IsPlayerLevel`): for each `NetworkStreamInGame` connection whose `CommandTarget.targetEntity == Entity.Null`, instantiate the prefab, set `GhostOwner.NetworkId`, `BotIndex = -1`, spawn position = `PlayerSpawn.Point(networkId)` on a 10 m ring, set the connection's `CommandTarget`, and append the ghost to the connection's `LinkedEntityGroup` (so disconnect destroys it).
  - `public static class PlayerSpawn { public static float3 Point(int slot); }` — ring of 16 points radius 10 m, y = 0.
  - `PlayerMovementSystem` (Client|Server simulation, `PredictedSimulationSystemGroup`, Burst `ISystem`): for every `PlayerGhost` with `Simulate`, call `PlayerMotion.Step` with `SystemAPI.Time.DeltaTime`, write `PlayerGhost.Position/Velocity` and `LocalTransform.Position`.
  - `DemoStats.PlayersOnServer`, `DemoStats.PlayersOnClient`, `DemoStats.OwnedPlayerEntity` (`Entity`, client), `DemoStats.OwnedPlayerPredicted` (bool) — written by a small `PlayerStatsSystem` in `PlayerSpawnSystem.cs` (client) and by `PlayerSpawnSystem` (server count).
  - Test helpers in `PlayerGateTests`: `static World ClientWorld()` / `ServerWorld()` (find via `World.All` and `IsClient()`/`IsServer()`). In this task the input test writes `PlayerInput` directly on the owned client ghost every frame; Task 4 replaces that with `PlayerInputGatherSystem.TestOverride`.

- [ ] **Step 1: Write the failing PlayMode tests** (`[UnitySetUp]` → `OverrideLevel(3)`, `Sandbox.Bots = 0`, load `SampleScene`, wait until `DemoStats.ClientInGame` (30 s); `[UnityTearDown]` → `OverrideLevel(null)`, `Sandbox.SetLink(0,0,0)`):
  - `OneOwnedPredictedGhostPerConnection`: within 10 s → `DemoStats.PlayersOnServer == 1`, `PlayersOnClient == 1`, client owned entity has `PredictedGhost` and `GhostOwnerIsLocal` enabled.
  - `ScriptedInputMovesGhostOnServer`: record server `PlayerGhost.Position.x`; write `PlayerInput { Move = new float2(1, 0) }` on the owned client ghost every frame for 60 server ticks → server `Position.x` increased by > 2 m.
  - `ReconnectReplacesOwnedGhost` (Review Focus 1): note owned entity's `GhostInstance.ghostId`; `Sandbox.KillConnectionRequested = true`; wait for reconnect (20 s) → `PlayersOnServer == 1`, `PlayersOnClient == 1`, owned ghost id differs.
- [ ] **Step 2: Run PlayMode** — expect compile failure / FAIL.
- [ ] **Step 3: Implement** the components, prefab system, spawn helper, spawn system, movement system and stats.
- [ ] **Step 4: Run PlayMode** — the three new tests PASS; `DemoGateTests` PASS (no player ghosts at level 1).
- [ ] **Step 5: Commit** — `feat(player): owner-predicted player ghost, spawned per connection`

---

### Task 4: Keyboard / gamepad input

**Files:**
- Modify: `Assets/FlagRush/Demo/FlagRush.Demo.asmdef` (add reference `Unity.InputSystem`)
- Create: `Assets/FlagRush/Demo/Player/PlayerInputMapping.cs`
- Create: `Assets/FlagRush/Demo/Player/PlayerInputGatherSystem.cs`
- Test: `Assets/FlagRush/Demo/Tests/PlayerInputMappingTests.cs`; modify `PlayerGateTests.cs` to use `TestOverride`

**Interfaces:**
- Consumes: `PlayerInput` (Task 1), owned ghost via `GhostOwnerIsLocal` (Task 3).
- Produces:
  - `public static class PlayerInputMapping { public static PlayerInput FromAxes(float2 raw, float3 cameraForward, float3 cameraRight, bool sprint); }` — flattens forward/right to XZ and normalizes them, `Move = raw.y * fwd.xz + raw.x * right.xz`, clamps `length(Move) <= 1`. Zero-length camera vectors fall back to world axes (+Z forward, +X right).
  - `PlayerInputGatherSystem` (Client simulation, `GhostInputSystemGroup`, managed `SystemBase`): reads `Keyboard.current` (WASD + arrows → raw axes, Left/Right Shift → sprint) and `Gamepad.current` (left stick, south button → sprint; stick wins if its magnitude > 0.1), gets the camera from `Camera.main` (world axes if null), writes the owned ghost's `PlayerInput`. `public static PlayerInput? TestOverride` — when set, written instead of device input.

- [ ] **Step 1: Write the failing tests** (`[Test]`):
  - `WMovesAlongCameraForwardFlattened`: raw (0,1), forward = normalize(0,-0.77,0.64), right = (1,0,0) → Move ≈ (0,1) within 1e-4.
  - `DiagonalIsClampedToUnitLength` (Review Focus 3): raw (1,1), world axes → `length(Move)` within 1e-4 of 1.
  - `StickOvershootClamped`: raw (1.2, 0) → `length(Move) <= 1 + 1e-5`.
  - `ZeroCameraFallsBackToWorldAxes`: forward = 0, right = 0, raw (1,0) → Move = (1,0).
  - `SprintPassesThrough`: sprint true → `Sprint == true`.
- [ ] **Step 2: Run EditMode** — FAIL (not defined).
- [ ] **Step 3: Implement** `PlayerInputMapping` and `PlayerInputGatherSystem`; switch `PlayerGateTests.ScriptedInputMovesGhostOnServer` to set `PlayerInputGatherSystem.TestOverride` (cleared in teardown).
- [ ] **Step 4: Run EditMode and PlayMode** — PASS.
- [ ] **Step 5: Commit** — `feat(player): keyboard and gamepad input, camera-relative`

---

### Task 5: Bots, separation, ServerPosition

**Files:**
- Create: `Assets/FlagRush/Demo/Player/BotBrain.cs` (pure) and `BotBrainSystem.cs`
- Create: `Assets/FlagRush/Demo/Player/PlayerSeparation.cs` (pure) and `PlayerSeparationSystem.cs`
- Modify: `Assets/FlagRush/Demo/Player/PlayerSpawnSystem.cs` (bot reconcile)
- Test: `Assets/FlagRush/Demo/Tests/BotBrainTests.cs`, `PlayerSeparationTests.cs`; add to `PlayerGateTests.cs`

**Interfaces:**
- Consumes: `PlayerGhost`, `ServerPosition`, `PlayerInput`, `PlayerGhostPrefab`, `PlayerSpawn.Point` (Task 3); `Sandbox.Bots` (Task 2).
- Produces:
  - `public struct BotBrainState : IComponentData { public float2 Target; public uint NextRetargetTick; public Unity.Mathematics.Random Rng; public bool Sprint; }` — server-only, added at bot spawn, not a ghost component. `Rng` seeded with `(uint)(BotIndex * 7919 + 1)`.
  - `public static class BotBrain { public static PlayerInput Think(ref BotBrainState s, float2 position, uint tick, in ArenaBounds arena); }` — retarget when within 1 m of `Target` or `tick >= NextRetargetTick` (new target uniform inside arena shrunk by 2 m; next retarget in 180–360 ticks; `Sprint` true with probability 0.2); returns `Move = normalize(Target - position)` (zero when within 0.1 m).
  - `BotBrainSystem` (Server, `PredictedSimulationSystemGroup`, `[UpdateBefore(typeof(PlayerMovementSystem))]`, Burst): for `PlayerGhost.BotIndex >= 0`, writes `PlayerInput` from `BotBrain.Think` using `NetworkTime.ServerTick.TickIndexForValidTick`.
  - `public static class PlayerSeparation { public static void Resolve(NativeArray<float3> positions, float radius, in ArenaBounds arena); }` — spatial hash with 2 m cells; each overlapping pair is pushed apart equally along XZ to distance `2 * radius`; result re-clamped to arena; coincident points pushed apart along +X/−X by index order (deterministic).
  - `PlayerSeparationSystem` (Server, `PredictedSimulationSystemGroup`, `[UpdateAfter(typeof(PlayerMovementSystem))]`): gathers player positions, `Resolve`, writes back `PlayerGhost.Position`, `LocalTransform.Position` and `ServerPosition.Value`.
  - `PlayerSpawnSystem` bot reconcile: when `IsPlayerLevel`, add bot ghosts (no `GhostOwner.NetworkId`, `BotIndex = next free index`, `BotBrainState`, position `PlayerSpawn.Point(16 + index)` pushed out to a 20 m ring) or destroy the highest-`BotIndex` bots, at most 50 per frame, until count == `Sandbox.Bots`. Only entities with `BotIndex >= 0` are ever destroyed.
  - `DemoStats.BotsOnServer`.

- [ ] **Step 1: Write the failing tests:**
  - `BotBrainTests` (`[Test]`): `SameSeedSameInputs` (two states seeded alike, 600 ticks → identical `Move` sequence); `TargetsStayInsideShrunkArena` (1000 retargets → every target within ±28); `MovesTowardTarget` (`dot(Move, Target - pos) > 0`, `length(Move) == 1` within 1e-4).
  - `PlayerSeparationTests` (`[Test]`): `OverlappingPairEndsAtTwoRadii` (points 0.5 m apart → distance ≥ 1.2 − 1e-4); `CoincidentPointsSeparateDeterministically` (two at origin → x of first < x of second); `FarPointsUntouched` (5 m apart → unchanged); `ResultStaysInArena` (pair at the wall → both within bounds).
  - `PlayerGateTests` (`[UnityTest]`): `BotsReconcileWithoutTouchingHumans` (Review Focus 2) — `Sandbox.Bots = 20` → wait `PlayersOnClient == 21`; `Sandbox.Bots = 0` → wait `PlayersOnClient == 1` and the owned entity is the same `ghostId` as before; `Sandbox.Bots = 5` → `PlayersOnClient == 6`. `BotsMove` — with 5 bots, sum of bot server positions changes over 60 ticks.
- [ ] **Step 2: Run EditMode and PlayMode** — FAIL.
- [ ] **Step 3: Implement** the interfaces above.
- [ ] **Step 4: Run EditMode and PlayMode** — PASS, including all earlier tests.
- [ ] **Step 5: Commit** — `feat(player): bots on the same input path, server-side separation`

---

### Task 6: Prediction toggle and prediction readouts

**Files:**
- Create: `Assets/FlagRush/Demo/Player/PredictionToggleSystem.cs`
- Create: `Assets/FlagRush/Demo/Player/PredictionStatsSystem.cs`
- Create: `Assets/FlagRush/Demo/Player/PredictionHistory.cs` (pure ring buffer)
- Modify: `Assets/FlagRush/Demo/DemoStats.cs`
- Test: `Assets/FlagRush/Demo/Tests/PredictionHistoryTests.cs`; add to `PlayerGateTests.cs`

**Interfaces:**
- Consumes: owned ghost, `ServerPosition`, `PlayerGhost` (Task 3); `Sandbox.PredictionEnabled` (Task 2).
- Produces:
  - `PredictionToggleSystem` (Client simulation, managed `SystemBase`, runs only when `IsPlayerLevel`): every frame, if the owned ghost's mode (has `PredictedGhost`) differs from `Sandbox.PredictionEnabled` and it has no `SwitchPredictionSmoothing` in progress, enqueue a `ConvertPredictionEntry { TargetEntity = owned, TransitionDurationSeconds = 0.25f }` into `GhostPredictionSwitchingQueues.ConvertToPredictedQueue` / `ConvertToInterpolatedQueue`. Retrying every frame is what makes rapid toggling converge (Review Focus 4).
  - `public struct PredictionHistory { public PredictionHistory(int capacity = 64); public void Record(uint tick, float3 position); public bool TryGet(uint tick, out float3 position); }` — fixed ring indexed by `tick % capacity`, stores the tick to reject stale slots. Plain managed struct with arrays is fine (client-only, main thread).
  - `PredictionStatsSystem` (Client simulation; one part `[UpdateInGroup(typeof(PredictedSimulationSystemGroup), OrderFirst = true)]` counting iterations and detecting rollbacks, one part in `SimulationSystemGroup` after prediction publishing the per-second values). Definitions:
    - **Predicted ticks / frame** = number of `PredictedSimulationSystemGroup` iterations this frame.
    - **Rollback** = a frame whose first predicted tick is ≤ the previous frame's last predicted tick; publish rollbacks per second over a 1 s window.
    - **Command age** = `NetworkTime.ServerTick − NetworkSnapshotAck.LastReceivedSnapshotByLocal` in ticks, also shown in ms at the simulation tick rate. (Not `ServerCommandAge`: that is clock-sync feedback, verified in the installed package.)
    - **Misprediction** = distance between owned `ServerPosition.Value` and `PredictionHistory.TryGet(LastReceivedSnapshotByLocal)`; the history records the owned ghost's position at each final (non-partial) predicted tick. 0 when prediction is off.
  - `DemoStats.PredictedTicksPerFrame`, `RollbacksPerSecond`, `CommandAgeTicks`, `MispredictionMeters` (`OwnedPlayerPredicted` already exists from Task 3).

- [ ] **Step 1: Write the failing tests:**
  - `PredictionHistoryTests` (`[Test]`): `ReturnsRecordedPosition`; `OverwrittenSlotRejectsOldTick` (record tick 1, then tick 65 → `TryGet(1)` false); `UnknownTickFalse`.
  - `PlayerGateTests` (`[UnityTest]`):
    - `PredictionLeadsServerUnderLatency`: `Sandbox.SetLink(100, 0, 0)`; wait for RTT > 150 ms; set `TestOverride` Move (1,0); within 2 client frames the client owned `LocalTransform.Position.x` increased while `ServerPosition.Value.x` has not yet changed; `DemoStats.CommandAgeTicks >= 6`.
    - `TogglingPredictionSwitchesGhostMode`: `PredictionEnabled = false` → within 3 s owned ghost lacks `PredictedGhost`; `= true` → within 3 s has it.
    - `RapidToggleConvergesToCheckbox` (Review Focus 4): flip `PredictionEnabled` false/true/false on three consecutive frames → within 3 s the ghost is interpolated and stays so for 1 s.
- [ ] **Step 2: Run EditMode and PlayMode** — FAIL.
- [ ] **Step 3: Implement** the interfaces above.
- [ ] **Step 4: Run EditMode and PlayMode** — PASS.
- [ ] **Step 5: Commit** — `feat(player): prediction toggle and prediction readouts`

---

### Task 7: Camera, rendering, HUD

**Files:**
- Create: `Assets/FlagRush/Demo/Player/PlayerCameraFollow.cs`
- Modify: `Assets/FlagRush/Demo/Editor/DemoSceneSetup.cs` (add `PlayerCameraFollow` to the main camera; re-run setup so `SampleScene` is saved with it)
- Modify: `Assets/FlagRush/Demo/InstancedRenderSystem.cs`
- Modify: `Assets/FlagRush/Demo/DemoHud.cs`
- Modify: `Assets/WebGLTemplates/FlagRush/index.html`
- Test: add to `PlayerGateTests.cs`

**Interfaces:**
- Consumes: `DemoStats` player and prediction fields (Tasks 3, 5, 6); `Sandbox.Bots`, `Sandbox.PredictionEnabled`, `Sandbox.MaxBots` (Task 2).
- Produces:
  - `PlayerCameraFollow : MonoBehaviour` — disabled behaviour unless `DemoConfig.IsPlayerLevel`; in `LateUpdate` reads the owned ghost's `LocalToWorld` from the client world (`DemoStats.OwnedPlayerEntity`, checked with `EntityManager.Exists`), damps position toward `target + (0, 18, -14)` with `Vector3.SmoothDamp` (smoothTime 0.15 s) and `LookAt(target)`. Keeps the scene's camera pose when there is no owned ghost.
  - `InstancedRenderSystem`: three new materials/draws, players built from `PlayerGhost.Position` (client: from `LocalTransform` for predicted, which equals the rendered position) with scale (1, 1.6, 1) and y offset 0.8: owned = green (0.3, 1, 0.4), others = grey (0.78, 0.8, 0.82), outline at owned `ServerPosition.Value` = green alpha 0.35 (material copy with URP surface type transparent; fall back to opaque dimmer green if the base shader lacks a transparent mode — note in commit). Outline drawn only when prediction is on. Draw counts go into the existing `DemoStats.DrawCalls/DrawnInstances`.
  - `Assets/WebGLTemplates/FlagRush/index.html:37`: turn `<span class="soon">3 · Player</span>` into `<a href="?level=3" data-level="3">3 · Player</a>` (the page already marks the active tab from `data-level`).
  - `DemoHud`: title "level 3 · player" at level 3; the level-1 hint line also mentions `?level=3`; new `Section("PLAYER", "does it feel instant at 200 ms?")` with rows Command age, Predicted ticks / frame, Rollbacks / s, Misprediction, Players (server / client), each with a one-line meaning taken from the spec table; control panel adds **Bots** slider (0–149) and **Prediction** toggle when `IsPlayerLevel`; hint line "Click the game, then WASD. Try: 200 ms latency, uncheck Prediction, walk into a bot."

- [ ] **Step 1: Write the failing PlayMode tests:**
  - `Level3DrawsPlayersAndOutline`: level 3, bots 5 → within 10 s `DemoStats.DrawnInstances == 1 + 5 + 1` (owned + bots + outline; agents and ghosts are 0).
  - `OutlineHiddenWhenPredictionOff`: `PredictionEnabled = false` → `DrawnInstances == 1 + 5`.
  - `CameraFollowsOwnedGhost`: drive Move (1,0) for 2 s → `Camera.main.transform.position.x` increased by > 2.
- [ ] **Step 2: Run PlayMode** — FAIL.
- [ ] **Step 3: Implement** the interfaces above; open the Editor once to run the scene setup menu (or call `DemoSceneSetup` via `-executeMethod`) so `SampleScene.unity` carries the component; commit the scene change.
- [ ] **Step 4: Run PlayMode** — PASS, `DemoGateTests` PASS unchanged. Manually play `?level=3` in the Editor: WASD moves, camera follows, toggle works.
- [ ] **Step 5: Commit** — `feat(player): follow camera, player rendering, PLAYER HUD section`

---

### Task 8: Scale gate, web build, docs

**Files:**
- Test: add to `PlayerGateTests.cs`
- Modify: `docs/how-the-prototype-works.md`, `README.md`, `docs/roadmap.md`, `docs/m1-web-spike.md`

**Interfaces:**
- Consumes: everything above.

- [ ] **Step 1: Write the PlayMode test** `HundredFiftyPlayersReplicate`: `Sandbox.Bots = 149` → within 20 s `PlayersOnClient == 150` and `PlayersOnServer == 150`; log `DemoStats` frame time, bandwidth and draw calls in the `[DemoGate]` format.
- [ ] **Step 2: Run PlayMode** — PASS (implement nothing new unless it fails; a failure here means reconcile pacing or ghost importance needs fixing in Task 5's code).
- [ ] **Step 3: Build and measure on Web:** `unity build C:\Projects\Portfolio\FlagRush --editor-version 6000.6.2f1 --target WebGL --execute-method FlagRush.Demo.Editor.DemoBuilder.BuildWebGl2`, then `python Tools\serve.py Builds\Web 8080`, open `http://localhost:8080/?level=3&bots=149` in Chromium. Expected: 60 fps (16.7 ms frame), WASD works after clicking the canvas, toggle and readouts behave as the spec's "Try this". Record the measured frame time, bandwidth and draw calls in `docs/m1-web-spike.md` under a new "Level 3" heading.
- [ ] **Step 4: Docs:** `how-the-prototype-works.md` gains a "Player" section with three try-this experiments (200 ms walk; prediction off; walk into a bot); README status row 3 → done and the "Open the Sandbox tab" line gets a `?level=3` sibling; roadmap table row 3 marked "(live)".
- [ ] **Step 5: Commit** — `docs: Level 3 Player live; 150-player web measurement`
