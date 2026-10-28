# Level 4a — Match Loop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** At `?level=4`, two teams of up to 150 players (149 bots + you) play capture the flag with server-side hitscan, a box arena, and a scoreboard, through the same predicted input path as Level 3.

**Architecture:** Pure, attribute-free rule functions in `Assets/FlagRush/Demo/Match/` (arena, obstacles, hitscan, combat, flags, round) are inlined into Burst `ISystem`s on the server; new ghost components and two runtime ghost prefabs (flag, match state) carry the result to the client; a test-only `CaptureTheFlagRuleSet : IRuleSet` adapter proves the rules satisfy the Domain contract.

**Tech Stack:** Unity 6000.6.2f1, Entities 6.6, Netcode for Entities 6.6, Unity.Mathematics / Collections, Input System 1.20, URP 17.6, NUnit (PlayMode-only test asmdef `FlagRush.Demo.Tests`).

**Spec:** `docs/superpowers/specs/2026-10-27-level4a-match-design.md` (read it; this plan argues from it).

## Global Constraints

- All rule durations are simulation ticks at 60 Hz. Values: fire interval 15, respawn 180, auto-return 600, round 18 000, ended phase 300, hit flash 9 ticks (0.15 s).
- Numbers from the spec, verbatim: health 100, damage 34 (3 hits kill), shot range 40 m, hit radius `PlayerTuning.Radius` (0.6 m), pickup/return radius 1.2 m, base radius 3 m, captures to win 3, bot fire range 25 m, bot aim error ±6°, bot defender hold radius 6 m, bot sprint when target > 15 m, box corner margin 1.5 m.
- Arena: Red base/stand (−25, 0), Blue (+25, 0); boxes (centre, half-extents) (±12, 0)(1, 5); (0, ±10)(4, 1); (±20, ±16)(2, 2); spawn zones Blue x ∈ [26, 29], Red x ∈ [−29, −26], z ∈ [−20, 20]. Team byte: 0 = Red, 1 = Blue.
- **Burst rule (BC1063):** never put `[BurstCompile]` on a static helper class or a helper whose signature takes `PlayerInput` or another struct containing `bool`. Helpers stay attribute-free and are inlined into `[BurstCompile]` `ISystem.OnUpdate`. Burst code never reads managed statics (`DemoConfig`, `Sandbox`); pass values through singleton components.
- **Every task, before commit:** run the full PlayMode suite and grep the log for Burst errors; both must be clean:
  - `unity test C:/Projects/Portfolio/FlagRush --editor-version 6000.6.2f1 --mode PlayMode` → `test-results.xml` `<test-run ... failed="0">`
  - `grep -ci "Burst error" Logs/Editor.log` → `0`
  - Focused runs use `--filter <TestClassName>`. Unity runs are serial (project lock). Never commit `test-results.xml`.
- Levels 1–3 behave exactly as before; all 46 existing tests stay green. Below level 4 the flag and `MatchState` ghosts do not exist.
- No new packages. Runtime ghost prefabs only (`GhostPrefabCreation.ConvertToGhostPrefab`), no SubScene or baking.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Leaving level 4 mid-round** (test override to 3 while dead or carrying): the player must come back alive and able to sprint — flags are destroyed, so `Carrying`/`RespawnTick` must be cleared. Test: Task 6 `LeavingMatchRevivesAndClearsCarrying`.
2. **Carrier vanishes** (disconnect, bot slider down, despawn): flag must drop where the carrier last was. Test: Task 3 `CarrierMissingDropsAtLastPosition`.
3. **Fire held with zero aim** (gamepad with no stick, cursor off the ground plane): no shot, no cooldown spent, no exception. Test: Task 2 `ZeroAimDoesNotFire`.
4. **Flag dropped inside a box or against the arena edge**: pushed to a reachable spot. Test: Task 3 `DropInsideBoxIsPushedOut`.
5. **Capture on the same tick the round times out**: the capture counts before the timeout is judged. Test: Task 3 `CaptureOnTimeoutTickCountsFirst`.

---

## File map

| File | Responsibility | Task |
|---|---|---|
| `Demo/Match/ArenaLayout.cs` | `Box`, `ArenaLayout` (Empty / Match), base and spawn points | 1 |
| `Demo/Match/Obstacles.cs` | circle push-out + slide, segment-vs-box, point push-out | 1 |
| `Demo/Match/ActiveArena.cs` | `ActiveArena` singleton + `ActiveArenaSystem` (both worlds) | 1 |
| `Demo/Player/PlayerMotion.cs`, `PlayerSeparation.cs`, `PlayerMovementSystem.cs`, `PlayerSeparationSystem.cs` | take the layout | 1 |
| `Demo/DemoConfig.cs` | `MaxLevel = 4`, `IsMatchLevel` | 1 |
| `Demo/Match/MatchTuning.cs` | all constants above | 2 |
| `Demo/Match/MatchComponents.cs` | `Combatant`, `CombatantServer`, `MatchStats`, `FlagGhost`, `MatchStateGhost`, enums | 2 |
| `Demo/Match/Hitscan.cs`, `Combat.cs` | ray vs players/boxes; fire gate, damage, kill | 2 |
| `Demo/Match/MatchEvent.cs`, `FlagRules.cs`, `MatchRules.cs`, `MatchTeams.cs` | flags, round, team pick | 3 |
| `Demo/Tests/CaptureTheFlagRuleSetTests.cs` (+ fakes in same folder) | Domain adapter | 4 |
| `Demo/Player/PlayerComponents.cs`, `PlayerGhostPrefabSystem.cs`, `PlayerSpawnSystem.cs` | input fields, combatant on prefab, team + spawn zone | 5 |
| `Demo/Match/MatchGhostPrefabSystem.cs`, `MatchSpawnSystem.cs` | flag + match-state prefabs; spawn/despawn | 5 |
| `Demo/Match/HitscanSystem.cs`, `MatchRulesSystem.cs`, `MatchServerStatsSystem.cs` | server match systems | 6 |
| `Demo/Player/BotBrain.cs`, `BotBrainSystem.cs` | match brain | 7 |
| `Demo/Player/PlayerInputMapping.cs`, `PlayerInputGatherSystem.cs`, `Demo/InstancedRenderSystem.cs`, `Demo/Match/MatchHudText.cs`, `Demo/Match/FireConfirmSystem.cs`, `Demo/DemoHud.cs`, `Demo/DemoStats.cs` | client input, rendering, HUD | 8 |
| docs, `Assets/WebGLTemplates/FlagRush/index.html` | docs, level link, browser numbers | 9 |

Tests: `Demo/Tests/ObstaclesTests.cs`, `HitscanTests.cs`, `FlagRulesTests.cs`, `MatchRulesTests.cs`, `CaptureTheFlagRuleSetTests.cs`, `MatchGateTests.cs`, `BotMatchBrainTests.cs`, `MatchHudTextTests.cs`, `MatchInputMappingTests.cs`; existing `PlayerMotionTests`, `PlayerSeparationTests`, `LevelConfigTests` updated.

---

### Task 1: Arena layout, obstacle collision, level 4 config

**Files:**
- Create: `Assets/FlagRush/Demo/Match/ArenaLayout.cs`, `Obstacles.cs`, `ActiveArena.cs`
- Modify: `Demo/Player/PlayerMotion.cs`, `PlayerSeparation.cs`, `PlayerMovementSystem.cs`, `PlayerSeparationSystem.cs`, `Demo/DemoConfig.cs`
- Test: `Demo/Tests/ObstaclesTests.cs`; update `PlayerMotionTests.cs`, `PlayerSeparationTests.cs`, `LevelConfigTests.cs`

**Interfaces:**
- Produces:
  - `public struct Box { public float2 Center; public float2 HalfExtents; }`
  - `public struct ArenaLayout { public FixedList512Bytes<Box> Boxes; public float2 RedBase; public float2 BlueBase; public float BaseRadius; public static ArenaLayout Empty { get; } public static ArenaLayout Match { get; } public float2 Base(byte team); public static float3 SpawnPoint(byte team, int index); }` — `SpawnPoint` uses 80 slots, index wrapped mod 80: `x = s * (26 + 3 * frac((i + 0.5) * 0.618034))` with `s = team == 0 ? -1 : 1`, `z = -20 + 40 * (i + 0.5) / 80`.
  - `public static class Obstacles { void Slide(ref float3 position, ref float2 velocity, float radius, in ArenaLayout layout); bool SegmentHit(float2 a, float2 b, in ArenaLayout layout, out float t, out int box); float2 PushOut(float2 p, float radius, in ArenaLayout layout); }` (`t` ∈ [0,1] along a→b, nearest box)
  - `PlayerMotion.Step(ref float3 position, ref float2 velocity, in PlayerInput input, float dt, in ArenaBounds arena, in ArenaLayout layout)` — calls `Obstacles.Slide` after the bounds clamp.
  - `PlayerSeparation.Resolve(NativeArray<float3> positions, float radius, in ArenaBounds arena, in ArenaLayout layout)` — push-out from boxes after separating.
  - `public struct ActiveArena : IComponentData { public ArenaLayout Layout; public bool Match; }` singleton in both worlds, written each frame by `ActiveArenaSystem : SystemBase` (Initialization group, Client|Server) from `DemoConfig.IsMatchLevel`.
  - `DemoConfig.MaxLevel = 4`; `DemoConfig.IsMatchLevel => Level >= 4`.

- [ ] **Step 1: Write failing tests** in `ObstaclesTests.cs`:

```csharp
[Test] public void MatchLayoutHasEightMirroredBoxes()   // Boxes.Length == 8; for each box, a box exists at (-cx, cz) and (cx, -cz) with equal half-extents
[Test] public void EmptyLayoutHasNoBoxes()              // ArenaLayout.Empty.Boxes.Length == 0
[Test] public void SlidePushesCircleOutOfBoxFace()      // pos (11.5, 0, 0), vel (5, 0), radius 0.6 vs box (12,0)(1,5) → pos.x == 11 - 0.6 (±1e-4), vel.x == 0, vel.y unchanged
[Test] public void SlideKeepsTangentialVelocity()       // pos (11.3, 0, 2), vel (3, 4) → vel == (0, 4)
[Test] public void SlideFromCornerPushesDiagonally()    // pos near corner (13.2, 0, 5.2) radius 0.6 → distance to (13, 5) == 0.6 (±1e-4)
[Test] public void SegmentHitReportsNearestBox()        // a (-20,0) b (20,0) → true, box index of (-12,0), t == (−13 − −20)/40 (±1e-4)
[Test] public void SegmentMissReturnsFalse()            // a (-20,7) b (20,7) → false
[Test] public void PushOutMovesPointToBoxEdge()         // PushOut((12, 0), 0, Match) lands on box (12,0)(1,5) boundary, not inside any box
[Test] public void SpawnPointsStayInZone()              // all 80 indices, both teams: |x| in [26,29], sign by team, z in [-20,20], not inside a box
```

Update existing tests: `PlayerMotionTests` and `PlayerSeparationTests` pass `ArenaLayout.Empty` (behaviour unchanged); add `PlayerMotionTests.StepWithMatchLayoutStopsAtBox` (walk +x from (8,0,0) for 2 s at full input → x ≤ 11 − 0.6 + 1e-4). `LevelConfigTests`: `OverrideLevel(4)` → `Level == 4`, `IsMatchLevel`, `IsPlayerLevel`; `OverrideLevel(3)` → `!IsMatchLevel`; level 5 clamps to 4.

- [ ] **Step 2: Run** `--filter ObstaclesTests` → FAIL (types missing).
- [ ] **Step 3: Implement** the files above. Circle-vs-AABB: closest point on box to centre; if distance < radius, move along (centre − closest) normalized to distance = radius (if centre is inside the box, push out through the nearest face); remove the velocity component along that normal when it points into the box. `PlayerMovementSystem` and `PlayerSeparationSystem` read `SystemAPI.GetSingleton<ActiveArena>().Layout` (require it for update).
- [ ] **Step 4: Run** the filters, then the full suite + Burst grep → all green, 0.
- [ ] **Step 5: Commit** `feat(match): arena layout, box collision, level 4 config`.

---

### Task 2: Match components, hitscan and combat rules

**Files:**
- Create: `Demo/Match/MatchTuning.cs`, `MatchComponents.cs`, `Hitscan.cs`, `Combat.cs`
- Test: `Demo/Tests/HitscanTests.cs`

**Interfaces:**
- Consumes: `ArenaLayout`, `Obstacles.SegmentHit` (Task 1).
- Produces:
  - `MatchTuning` constants (names): `FireIntervalTicks, RespawnTicks, AutoReturnTicks, RoundTicks, EndedTicks, HitFlashTicks, MaxHealth, Damage, ShotRange, PickupRadius, BaseRadius (3), CapturesToWin, BotFireRange, BotAimErrorDegrees, BotHoldRadius, BotSprintDistance, BotCornerMargin`.
  - `[GhostComponent] public struct Combatant : IComponentData { [GhostField] public byte Team; [GhostField] public byte Health; [GhostField] public uint LastHitTick; [GhostField] public uint RespawnTick; [GhostField] public bool Carrying; public bool Alive => RespawnTick == 0; }` (`Carrying` added beyond the spec so the owner predicts the no-sprint rule; ruling recorded in the as-built note.)
  - `public struct CombatantServer : IComponentData { public uint NextFireTick; }`
  - `[GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)] public struct MatchStats : IComponentData { [GhostField] public short Kills, Deaths, Captures; [GhostField] public ushort ShotsFired; }`
  - `public enum FlagState : byte { Home, Carried, Dropped }`; `public enum MatchPhase : byte { Playing, Ended }`
  - `[GhostComponent] public struct FlagGhost : IComponentData { [GhostField] public byte Team; [GhostField] public FlagState State; [GhostField(Quantization = 1000, Smoothing = SmoothingAction.InterpolateAndExtrapolate)] public float3 Position; [GhostField] public int CarrierGhostId; [GhostField] public uint DroppedTick; }`
  - `[GhostComponent] public struct MatchStateGhost : IComponentData { [GhostField] public short RedScore, BlueScore; [GhostField] public MatchPhase Phase; [GhostField] public uint PhaseEndTick; [GhostField] public uint RoundStartTick; }`
  - `public static class Hitscan { public static int Cast(float2 origin, float2 dir, float range, in ArenaLayout layout, NativeArray<float2> positions, NativeArray<byte> teams, NativeArray<bool> hittable, byte shooterTeam, int shooterIndex); }` → index or −1; returns −1 when `dir` is zero.
  - `public static class Combat { public static bool TryFire(ref uint nextFireTick, uint tick); public static bool ApplyHit(ref Combatant target, uint tick); }` — `TryFire`: if `tick >= nextFireTick` set `nextFireTick = tick + 15` and return true. `ApplyHit`: health −34 floored at 0, `LastHitTick = tick`; at 0 sets `RespawnTick = tick + 180` (use 1 if that wraps to 0) and returns true (killed).

- [ ] **Step 1: Failing tests** `HitscanTests.cs`:

```csharp
[Test] public void HitsNearestEnemyOnRay()        // shooter 0 at (-24,0) team 0; enemies at (-20,0) and (-18,0) team 1 → 1 (the -20 one's index)
[Test] public void BoxBlocksShot()                // shooter (-20,0), enemy (-5,0), box (-12,0) between → -1
[Test] public void TeammatesAreIgnored()          // teammate at (-22,0) before enemy at (-20,0) → enemy index
[Test] public void DeadAreNotHittable()           // hittable[target] = false → -1
[Test] public void RangeLimitIs40m()              // enemy centre at 40.7 m → -1; at 40.5 m (edge within 40 m) → hit
[Test] public void ZeroAimDoesNotFire()           // dir (0,0) → -1 (Review Focus 3); Combat.TryFire untouched by caller
[Test] public void ShooterNeverHitsSelf()         // only the shooter on the ray → -1
[Test] public void FireGateAllowsFourPerSecond()  // TryFire at ticks 0..59 every tick → exactly 4 true (0,15,30,45)
[Test] public void ThreeHitsKill()                // ApplyHit ×2 → false, Health 32; third → true, Health 0, RespawnTick == tick + 180, LastHitTick == tick
```

- [ ] **Step 2: Run** `--filter HitscanTests` → FAIL.
- [ ] **Step 3: Implement.** Ray vs circle: project the centre on the ray; hit when the perpendicular distance ≤ 0.6 and the ray's entry point into the circle (`along − sqrt(0.36 − perp²)`) lies in [0, 40]; blocked if `Obstacles.SegmentHit(origin, entry point)` is true. Nearest entry wins.
- [ ] **Step 4: Run** filter, full suite, Burst grep → green, 0.
- [ ] **Step 5: Commit** `feat(match): hitscan and combat rules`.

---

### Task 3: Flag and round rules

**Files:**
- Create: `Demo/Match/MatchEvent.cs`, `FlagRules.cs`, `MatchRules.cs`, `MatchTeams.cs`
- Test: `Demo/Tests/FlagRulesTests.cs`, `Demo/Tests/MatchRulesTests.cs`

**Interfaces:**
- Consumes: `ArenaLayout`, `Obstacles.PushOut`, `FlagState`, `MatchPhase`, `MatchTuning`.
- Produces:
  - `public enum MatchEventKind : byte { FlagTaken, FlagDropped, FlagReturned, FlagCaptured, PlayerHit, PlayerKilled, RoundEnded, RoundReset }`
  - `public struct MatchEvent { public MatchEventKind Kind; public uint Tick; public int Actor; public int Target; public byte Team; }` (Actor/Target are player ids — ghost ids on the server; Team is the flag's or scoring team)
  - `public struct PlayerView { public int Id; public float2 Position; public byte Team; public bool Alive; }` (Id ≠ 0)
  - `public struct FlagData { public byte Team; public FlagState State; public float2 Position; public int CarrierId; public uint DroppedTick; public static FlagData AtHome(byte team, in ArenaLayout layout); }`
  - `public static class FlagRules { public static void Step(ref FlagData red, ref FlagData blue, NativeArray<PlayerView> players, in ArenaLayout layout, uint tick, NativeList<MatchEvent> events); }` — per flag, in this order: (1) Carried and carrier missing or dead → Dropped at `PushOut(position, 0.3)`, `FlagDropped`; (2) Carried → Position = carrier position; (3) Dropped and `tick − DroppedTick ≥ 600` → Home, `FlagReturned` (Actor 0); (4) Dropped and an alive team-mate within 1.2 m → Home, `FlagReturned` (Actor = that player); (5) Home/Dropped and an alive enemy within 1.2 m → Carried, `FlagTaken`; then captures: a carrier inside its own base radius while its own flag is Home → `FlagCaptured` (Team = scoring team, Actor = carrier), carried flag → Home. Nearest player wins ties (lowest Id on equal distance).
  - `public struct MatchData { public short RedScore, BlueScore; public MatchPhase Phase; public uint PhaseEndTick; public uint RoundStartTick; }`
  - `public static class MatchRules { public static MatchData NewRound(uint tick); public static void Step(ref MatchData m, NativeArray<MatchEvent> thisTick, uint tick, NativeList<MatchEvent> events); }` — Playing: add 1 per `FlagCaptured` to its team, then if a score ≥ 3 or `tick − RoundStartTick ≥ 18000` → Ended, `PhaseEndTick = tick + 300`, `RoundEnded` (Team = winner, 255 = draw). Ended and `tick ≥ PhaseEndTick` → `NewRound(tick)`, `RoundReset`.
  - `public static class MatchTeams { public static byte Pick(int redCount, int blueCount); }` → 0 on tie or Red smaller, else 1.

- [ ] **Step 1: Failing tests.** `FlagRulesTests.cs` (each builds two `FlagData.AtHome` and a `NativeArray<PlayerView>`):

```csharp
[Test] public void EnemyTouchTakesFlag()                 // Blue player at (-24.0,0) → red.State Carried, CarrierId = id, one FlagTaken
[Test] public void TeammateCannotTakeOwnHomeFlag()       // Red player at (-25,0) → red stays Home, no events
[Test] public void CarriedFlagFollowsCarrier()           // carrier moves to (0,3) → red.Position == (0,3)
[Test] public void CarrierDeathDrops()                   // carrier Alive=false → Dropped at its position, DroppedTick = tick, FlagDropped
[Test] public void CarrierMissingDropsAtLastPosition()   // carrier id not in array → Dropped at last Position (Review Focus 2)
[Test] public void DropInsideBoxIsPushedOut()            // carrier last at (12,0) inside box → dropped position outside every box (Review Focus 4)
[Test] public void TeammateTouchReturnsDroppedFlag()     // Red player within 1.2 m of dropped red flag → Home, FlagReturned Actor = id
[Test] public void AutoReturnAtExactly600Ticks()         // dropped at 100: tick 699 still Dropped; tick 700 Home, FlagReturned Actor 0
[Test] public void CaptureNeedsOwnFlagHome()             // Blue carrier of red flag inside Blue base, blue flag Carried → no capture
[Test] public void CaptureScoresAndResets()              // same with blue flag Home → FlagCaptured Team 1 Actor carrier, red Home
```

`MatchRulesTests.cs`:

```csharp
[Test] public void CaptureAddsScore()                    // FlagCaptured Team 0 → RedScore 1
[Test] public void ThirdCaptureEndsRound()               // RedScore 2 + capture → Ended, PhaseEndTick tick+300, RoundEnded Team 0
[Test] public void TimeoutEndsRoundAsDraw()              // 1-1 at RoundStartTick+18000 → Ended, RoundEnded Team 255
[Test] public void TimeoutLeaderWins()                   // 2-1 at timeout → RoundEnded Team 0
[Test] public void CaptureOnTimeoutTickCountsFirst()     // 2-2, capture by Blue on the timeout tick → BlueScore 3, Team 1 (Review Focus 5)
[Test] public void EndedPhaseResetsAfter300Ticks()       // tick PhaseEndTick-1 still Ended; PhaseEndTick → scores 0, Playing, RoundStartTick = tick, RoundReset
[Test] public void CapturesIgnoredWhileEnded()           // FlagCaptured during Ended → score unchanged
[Test] public void TeamPickBalances()                    // Pick(0,0)=0, Pick(1,0)=1, Pick(3,2)=1, Pick(2,3)=0
```

- [ ] **Step 2: Run** `--filter FlagRulesTests` and `--filter MatchRulesTests` → FAIL.
- [ ] **Step 3: Implement** in the files above (one `Step` per class, helpers private).
- [ ] **Step 4: Run** filters, full suite, Burst grep → green, 0.
- [ ] **Step 5: Commit** `feat(match): flag and round rules`.

---

### Task 4: Domain contract adapter (tests only)

**Files:**
- Create: `Demo/Tests/CaptureTheFlagRuleSetTests.cs`, `Demo/Tests/DomainFakes.cs`
- Modify: `Demo/Tests/FlagRush.Demo.Tests.asmdef` (add reference `FlagRush.Domain`)

`FlagRush.Domain.Tests` is Editor-only, so its fakes cannot be referenced from this PlayMode asmdef; minimal fakes live in `DomainFakes.cs` (as the spec allows).

**Interfaces:**
- Consumes: `FlagRules.Step`, `MatchRules.Step`, `FlagData`, `PlayerView`, `MatchEvent`, `ArenaLayout.Match`; Domain `IRuleSet`, `IRuleContext`, `IInput`, `IGameEvent`/`GameEvent`, `IEntityRegistryWriter`, `IRelationshipWriter`, `Relationship`, `RelationshipKind.Carries`, `EntityId`, `PlayerId`, `Tick`, `ICarrier`, `ICarryable`, `IHasTeam`.
- Produces (test assembly only):
  - `sealed class CaptureTheFlagRuleSet : IRuleSet` — `Name = "CaptureTheFlag"`; `Initialize` adds two flag entities (`ICarryable + IHasTeam`) and keeps `FlagData`/`MatchData` in the context's entities; `Apply` maps each `MoveTo` input to that avatar's position, builds `PlayerView`s, runs `FlagRules.Step` and `MatchRules.Step`, mirrors every carried flag as exactly one `Relationship(Carries, avatar, flag)` (removed on drop/return/capture), and emits each `MatchEvent` as `MatchGameEvent : GameEvent`.
  - `sealed class MoveTo : IInput { PlayerId Player; Tick Tick; float2 Position; }`
  - Fakes: `FakeRuleContext : IRuleContext`, `FakeEntities : IEntityRegistryWriter`, `FakeRelationships : IRelationshipWriter`, `FakeAvatar : ICarrier, IHasTeam`, `FakeFlag : ICarryable, IHasTeam` (implement only what the tests touch; unused members throw `NotSupportedException`).

- [ ] **Step 1: Failing test:**

```csharp
[Test] public void ScriptedCaptureThroughRuleSetContract()
// Blue avatar (PlayerId 2, EntityId 2) walks: MoveTo(-25,0) at tick 1 → Carries(2, redFlag) present, FlagTaken emitted;
// MoveTo(0,7) tick 2 → still carried; MoveTo(25,0) tick 3 → FlagCaptured (Team 1) emitted, Carries edge gone, Relationships.Count == 0.
[Test] public void RuleSetHoldsNoStateBetweenSteps()
// two CaptureTheFlagRuleSet instances fed the same context alternately produce the same events as one instance
```

- [ ] **Step 2: Run** `--filter CaptureTheFlagRuleSetTests` → FAIL.
- [ ] **Step 3: Implement** the adapter and fakes. All rule-set state lives in the context (a `MatchStateEntity : IEntity` registered in `FakeEntities` holds `FlagData` ×2 and `MatchData`).
- [ ] **Step 4: Run** filter, full suite, Burst grep → green, 0.
- [ ] **Step 5: Commit** `test(match): CaptureTheFlagRuleSet adapter proves the Domain contract`.

---

### Task 5: Match ghosts, teams and spawning

**Files:**
- Create: `Demo/Match/MatchGhostPrefabSystem.cs`, `Demo/Match/MatchSpawnSystem.cs`, `Demo/Tests/MatchGateTests.cs`
- Modify: `Demo/Player/PlayerComponents.cs`, `PlayerGhostPrefabSystem.cs`, `PlayerSpawnSystem.cs`, `Demo/DemoStats.cs`

**Interfaces:**
- Consumes: Task 2 components, `ArenaLayout.SpawnPoint`, `MatchTeams.Pick`, `MatchRules.NewRound`, `FlagData.AtHome`.
- Produces:
  - `PlayerInput` gains `public float2 Aim; public bool Fire;`.
  - Player prefab gains `Combatant` (Health 100) and `MatchStats`. Server adds `CombatantServer` at spawn.
  - `public struct MatchGhostPrefabs : IComponentData { public Entity Flag; public Entity State; }` singleton in both worlds; flag prefab `FlagGhost + LocalTransform(DontSerialize) + LocalToWorld`, Interpolated only, Importance 3000; state prefab `MatchStateGhost`, Interpolated, Importance 3000.
  - `MatchSpawnSystem` (server, Simulation group): level ≥ 4 → exactly two flags (`FlagData.AtHome` for 0 and 1) and one `MatchStateGhost` from `MatchRules.NewRound(tick)`; below 4 → destroy them **and** for every player set `Carrying = false`, `RespawnTick = 0`, `Health = 100`.
  - `PlayerSpawnSystem` at level ≥ 4: `team = MatchTeams.Pick(currentRed, currentBlue)` for every new human and bot; spawn at `ArenaLayout.SpawnPoint(team, n)` where `n` is a per-team running counter; at level 3 spawning is unchanged.
  - `DemoStats`: `FlagsOnServer`, `FlagsOnClient`, `MatchStateOnClient` (bool), `RedPlayersOnServer`, `BluePlayersOnServer` (written by `MatchSpawnSystem` / a client counting pass in the same file).

- [ ] **Step 1: Failing gate tests** `MatchGateTests.cs` (SetUp as `PlayerGateTests` but `OverrideLevel(4)`; TearDown waits for `FlagsOnServer == 0 && FlagsOnClient == 0` after `OverrideLevel(null)`):

```csharp
[UnityTest] public IEnumerator MatchGhostsSpawn()            // FlagsOnServer == 2, FlagsOnClient == 2, MatchStateOnClient within 10 s
[UnityTest] public IEnumerator TeamsAreBalanced()            // Bots = 5 → Red 3, Blue 3 (human counts); every server player inside its team's spawn zone
[UnityTest] public IEnumerator HumanSpawnsInOwnZone()        // human's server position satisfies the spawn-zone bounds for its Combatant.Team
[UnityTest] public IEnumerator LeavingMatchRevivesAndClearsCarrying()
// server: set the human's Combatant to { RespawnTick = 999999, Carrying = true, Health = 0 }; OverrideLevel(3);
// within 5 s: FlagsOnServer == 0, FlagsOnClient == 0, human Combatant.RespawnTick == 0, Carrying false, Health 100 (Review Focus 1)
```

- [ ] **Step 2: Run** `--filter MatchGateTests` → FAIL.
- [ ] **Step 3: Implement** the files above.
- [ ] **Step 4: Run** filter, full suite, Burst grep → green, 0.
- [ ] **Step 5: Commit** `feat(match): flag and match-state ghosts, teams, spawn zones`.

---

### Task 6: Server match systems

**Files:**
- Create: `Demo/Match/HitscanSystem.cs`, `MatchRulesSystem.cs`, `MatchServerStatsSystem.cs`
- Modify: `Demo/Player/PlayerMovementSystem.cs`, `Demo/DemoStats.cs`, `Demo/Tests/MatchGateTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1–5.
- Produces:
  - `PlayerMovementSystem`: input zeroed while `!Combatant.Alive`; `Sprint` forced false while `Combatant.Carrying` (both worlds, so prediction agrees).
  - `HitscanSystem` (server, `PredictedSimulationSystemGroup`, after `PlayerSeparationSystem`, Burst): skips unless `ActiveArena.Match` and phase Playing; gathers positions/teams/alive; for each alive player with `Fire` and non-zero `Aim` and `Combat.TryFire`: `ShotsFired++`, `Hitscan.Cast`, `Combat.ApplyHit`; kill → shooter `Kills++`, target `Deaths++`. Adds shots to singleton `MatchCounters { public uint ShotsTotal; }`.
  - `MatchRulesSystem` (server, same group, after `HitscanSystem`, Burst): builds `PlayerView`s with `GhostInstance.ghostId`; runs `FlagRules.Step` (only while Playing) then `MatchRules.Step`; writes `FlagGhost` (+ `LocalTransform`) and `MatchStateGhost`; sets `Combatant.Carrying` from `FlagData.CarrierId`; `Captures++` on the actor of `FlagCaptured`; respawns alive-due players (`tick ≥ RespawnTick`): Health 100, RespawnTick 0, teleport to `SpawnPoint(team, counter++)` (write `PlayerGhost.Position`, `Velocity = 0`, `LocalTransform`, `ServerPosition`); on `RoundReset`: every player revived and teleported, flags home, `MatchStats` kept (they are per-session, not per-round).
  - `MatchServerStatsSystem` (server, managed, after the rules): `DemoStats.ShotsPerSecond` from `MatchCounters` deltas over 1 s windows; `DemoStats.LastMatchEvents` (ring of the last 16 `MatchEvent`s, for tests and the HUD) fed through a `DynamicBuffer<MatchEventRecord>` on the `MatchCounters` singleton that `MatchRulesSystem`/`HitscanSystem` append to and this system drains.

- [ ] **Step 1: Failing gate tests** (append to `MatchGateTests.cs`):

```csharp
[UnityTest] public IEnumerator WalkingTheFlagHomeScores()
// Bots = 0. Test steers the human with PlayerInputGatherSystem.TestOverride toward waypoints by its Combatant.Team:
// Red human: (-22,7) → (22,7) → (25,0) → (22,7) → (-22,7) → (-25,0); Blue mirrors x. Waypoint reached at 0.8 m.
// Within 45 s: MatchStateGhost score of the human's team == 1 on the server and the client; LastMatchEvents contains FlagTaken then FlagCaptured.
[UnityTest] public IEnumerator ThreeShotsKillAStationaryBotWhichRespawns()
// Bots = 1; wait for it; remove its BotBrainState on the server (stationary); server-teleport human to (-24,0,0) and the bot to (-19,0,0)
// (team-mirrored: x sign by human's team); TestOverride = { Aim = (±1,0), Fire = true }.
// Within 3 s: bot RespawnTick != 0, bot Deaths == 1, human Kills == 1, human ShotsFired >= 3.
// Then Fire = false; within 180 ticks + 1 s: bot alive, Health 100, inside its spawn zone.
[UnityTest] public IEnumerator DeadPlayerCannotMove()
// server-set the human's RespawnTick to now + 600 and Health 0; TestOverride Move (1,0) for 30 server ticks → server position unchanged (±1e-3)
```

- [ ] **Step 2: Run** `--filter MatchGateTests` → the three new tests FAIL.
- [ ] **Step 3: Implement.** Order in `PredictedSimulationSystemGroup` (server): `BotBrainSystem` → `PlayerMovementSystem` → `PlayerSeparationSystem` → `HitscanSystem` → `MatchRulesSystem`. Rule functions run once per server tick (the server does not re-predict).
- [ ] **Step 4: Run** filter, full suite, Burst grep → green, 0.
- [ ] **Step 5: Commit** `feat(match): server hitscan, flags, round and respawn systems`.

---

### Task 7: Bot match brain

**Files:**
- Modify: `Demo/Player/BotBrain.cs`, `Demo/Player/BotBrainSystem.cs`
- Test: `Demo/Tests/BotMatchBrainTests.cs`

**Interfaces:**
- Consumes: `PlayerView`, `FlagData`, `ArenaLayout`, `Obstacles.SegmentHit`, `Hitscan.Cast`, `MatchTuning`.
- Produces:
  - `public enum BotMatchState : byte { Return, Hunt, GoForFlag, Hold }`
  - `public static BotMatchState BotBrain.ChooseState(int botIndex, in PlayerView self, in FlagData ownFlag, in FlagData enemyFlag, NativeArray<PlayerView> players, in ArenaLayout layout, out float2 target)` — rules per spec §3: Defender = `botIndex % 3 == 0`; Return if `enemyFlag.CarrierId == self.Id` → own base; Hunt if `ownFlag.State == Carried` → carrier position, or (Defender) nearest alive enemy with `x` in own half (`team 0: x < 0`); GoForFlag (Attacker) → `enemyFlag.Position`; Hold (Defender, nothing to hunt) → own stand, and `target` = current position when already within 6 m.
  - `public static float2 BotBrain.SteerToward(float2 position, float2 target, in ArenaLayout layout)` → unit direction; if `SegmentHit(position, target)` → toward the hit box's corner nearest `position`, pushed out by 1.5 m along the corner's diagonal.
  - `public static PlayerInput BotBrain.ThinkMatch(ref BotBrainState s, int botIndex, in PlayerView self, in FlagData ownFlag, in FlagData enemyFlag, NativeArray<PlayerView> players, in ArenaLayout layout)` — `Move = SteerToward(...)` (zero when Hold and within 6 m), `Sprint = !carrying && distance(target) > 15`, `Aim`/`Fire`: nearest alive enemy within 25 m with no box on the segment → aim rotated by `s.Rng.NextFloat(-6°, 6°)`, `Fire = true`; otherwise `Aim = 0`, `Fire = false`.
  - `BotBrainSystem`: when `ActiveArena.Match`, builds `PlayerView`s and reads both `FlagGhost`s, calls `ThinkMatch`; otherwise the Level 3 `Think` path is unchanged.

- [ ] **Step 1: Failing tests** `BotMatchBrainTests.cs`:

```csharp
[Test] public void CarrierReturnsToOwnBase()         // self carries enemy flag → Return, target == own base
[Test] public void OwnFlagTakenMeansHunt()           // own flag Carried by id 9 at (5,5) → Hunt, target (5,5)
[Test] public void AttackerGoesForEnemyFlag()        // index 1, nothing happening → GoForFlag, target == enemy flag position
[Test] public void DefenderHoldsNearStand()          // index 0 at own stand + (3,0) → Hold, Move == 0
[Test] public void DefenderHuntsIntruder()           // index 0, enemy alive at x=-10 (Red half) → Hunt that enemy
[Test] public void SteersAroundBox()                 // from (-20,0) to (0,0): direction points to a corner of box (-12,0)(1,5) ± margin, not straight +x
[Test] public void FiresAtVisibleEnemyWithinError()  // enemy 10 m away, clear line → Fire, angle(Aim, true dir) ≤ 6° + 1e-3
[Test] public void HoldsFireBehindBox()              // enemy behind box → Fire false, Aim zero
[Test] public void NoSprintWhileCarrying()           // carrying, target 40 m away → Sprint false
```

- [ ] **Step 2: Run** `--filter BotMatchBrainTests` → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** filter, full suite (Level 3 `BotsMove` still passes), Burst grep → green, 0.
- [ ] **Step 5: Commit** `feat(match): bot roles, states, steering and shooting`.

---

### Task 8: Client input, rendering and HUD

**Files:**
- Create: `Demo/Match/MatchHudText.cs`, `Demo/Match/FireConfirmSystem.cs`, `Demo/Tests/MatchHudTextTests.cs`, `Demo/Tests/MatchInputMappingTests.cs`
- Modify: `Demo/Player/PlayerInputMapping.cs`, `PlayerInputGatherSystem.cs`, `Demo/InstancedRenderSystem.cs`, `Demo/DemoHud.cs`, `Demo/DemoStats.cs`, `Demo/Tests/MatchGateTests.cs`

**Interfaces:**
- Consumes: all ghost components; `DemoStats.ClientInterpolationTick`; `Obstacles.SegmentHit` (tracer length).
- Produces:
  - `PlayerInputMapping.AimFromCursor(float3 rayOrigin, float3 rayDir, float2 playerXZ) → float2` (ray onto y = 0; zero if the ray is parallel/upward or the point is within 0.05 m of the player) and `PlayerInputMapping.AimFromStick(float2 stick, float3 camForward, float3 camRight) → float2` (camera-relative, zero below 0.2 magnitude). Gather: mouse left / right trigger = `Fire`; stick aim wins over cursor when non-zero.
  - Rendering at level 4: other players in team red `(0.9, 0.3, 0.3)` / blue `(0.3, 0.5, 1)`; owned player in its team colour plus a white ring (flat cube 1.8 × 0.05 × 1.8 at y 0.02); dead players not drawn; players hit within the last 9 interpolation ticks drawn white; boxes dark grey height 2; base discs translucent team colour 6 × 0.05 × 6; flags 0.3 × 2 × 0.3 team colour at `FlagGhost.Position`; a tracer (0.08 m wide, yellow, 0.05 s) from the owned player along `Aim` to 40 m or the first box, drawn when `Fire` is pressed and the local fire gate (`MatchTuning.FireIntervalTicks` on client ticks) allows. Below level 4 rendering is unchanged.
  - `MatchHudText` (pure): `Clock(uint ticksLeft) → "m:ss"`; `Banner(short red, short blue) → "Red wins 3–1" | "Blue wins 1–3" | "Draw 1–1"`; `FlagLine(in FlagGhost flag, int ownedGhostId, uint tick) → "home" | "carried by you" | "carried" | "dropped · 7 s"` (seconds to auto-return, rounded up).
  - `FireConfirmSystem` (client, managed): on the frame `Fire` goes true, record `(Time.realtimeSinceStartup, MatchStats.ShotsFired)`; when the owned `MatchStats.ShotsFired` differs (ushort wrap-safe), sample `DemoStats.FireConfirmMs`.
  - `DemoHud` at level 4: top-centre scoreboard `Red n — m:ss — n Blue`; end banner during `Ended`; panel rows `Flags`, `You` (k/d/c), `Shots resolved on server`, `Fire → server confirm`, each with the meaning text from spec §4; title "level 4 · match".

- [ ] **Step 1: Failing tests:**

```csharp
// MatchHudTextTests
[Test] public void ClockFormats()          // 18000 → "5:00", 59 → "0:01", 0 → "0:00"
[Test] public void BannerNamesWinnerOrDraw()// (3,1) "Red wins 3–1"; (1,3) "Blue wins 1–3"; (2,2) "Draw 2–2"
[Test] public void FlagLines()             // Home → "home"; Carried by owned id → "carried by you"; by other → "carried"; Dropped at 100, tick 280 → "dropped · 7 s"
// MatchInputMappingTests
[Test] public void CursorAimPointsAtGroundHit()  // origin (0,18,-14), dir toward (5,0,0), player (0,0) → ≈ normalize(5, 14)
[Test] public void CursorOnPlayerGivesZero()     // ground hit within 0.05 m of player → (0,0)
[Test] public void UpwardRayGivesZero()          // dir.y >= 0 → (0,0)
[Test] public void StickAimIsCameraRelative()    // stick (0,1), default camera → (0,1); below 0.2 → (0,0)
// MatchGateTests
[UnityTest] public IEnumerator MatchRenderDrawsArena()
// Bots = 5: within 10 s DemoStats.DrawnInstances == 6 players + 1 ring + 8 boxes + 2 bases + 2 flags (+1 outline when prediction on)
```

- [ ] **Step 2: Run** the three filters → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** filters, full suite (Level 3 `PlayerGateTests` render counts unchanged), Burst grep → green, 0.
- [ ] **Step 5: Commit** `feat(match): aim and fire input, arena rendering, scoreboard and readouts`.

---

### Task 9: Docs, level link, browser measurement

**Files:**
- Modify: `README.md`, `docs/roadmap.md` (Level 4 row "4a live"; 4b/4c noted), `docs/how-the-prototype-works.md` (Match section: rules table, readouts, "shots are server-decided, no lag compensation yet"), `docs/m1-web-spike.md` (Level 4 browser numbers), `Assets/WebGLTemplates/FlagRush/index.html` ("4 · Match" link + one-line blurb), spec "Implementation notes (as built)" section listing rulings (e.g. `Combatant.Carrying`, phase-Ended freezes flags and shots), plan "As built" note.

- [ ] **Step 1:** Build WebGL: `unity build C:/Projects/Portfolio/FlagRush --editor-version 6000.6.2f1 --target WebGL --execute-method FlagRush.Demo.Editor.DemoBuilder.BuildWebGl2`; serve `python Tools/serve.py Builds/Web 8080`.
- [ ] **Step 2:** In a browser at `http://localhost:8080/?level=4&bots=149`: let a round play to its banner and reset; record frame time (target ≤ 16.7 ms), bandwidth KB/s and B/snapshot, shots resolved/s, fire → confirm ms at LAN and 4G presets. (Executed by the controller; subagents have no browser.)
- [ ] **Step 3:** Write the docs above with the measured numbers; anything not verified is stated as not verified.
- [ ] **Step 4:** Full suite + Burst grep → green, 0.
- [ ] **Step 5: Commit** `docs: Level 4a match loop, browser measurements`.
