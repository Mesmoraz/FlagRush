# Level 4a — Match loop: design

Status: approved in conversation 2026-10-27; this is the written spec for review.
Roadmap entry: [docs/roadmap.md — Level 4](../../roadmap.md#level-4--match-capture-the-flag-and-state-that-survives-a-disconnect).
Builds on: [Level 3 — Player](2026-10-25-level3-player-design.md) (merged).

## Intent

**What this proves (posting line):** the game loop. Two teams, carryable flags, capture rules, hitscan
combat and a scoreboard, all server-authoritative, with 149 bots playing the same rules through the
same input path as the human. The rules are pure functions written in the Domain model's vocabulary,
and a test-only `IRuleSet` adapter shows they satisfy the Domain contract.

**Decisions taken while designing**

| Question | Decision |
|---|---|
| How Level 4 is split | 4a match loop (this spec) → 4b profiles / XP / disconnect button → 4c relevancy readout; each its own spec |
| Stopping a flag carrier | Hitscan + health; no lag compensation in 4a (named gap) |
| Arena | Code-defined: two bases and 8 axis-aligned boxes as data; no SubScene |
| Architecture | ECS-native Burst systems calling pure rule functions (Domain vocabulary); `CaptureTheFlagRuleSet : IRuleSet` adapter in tests only |

**Out of scope:** profiles, XP, `AuthId`, disconnect button (4b); relevancy / ghost culling readout
(4c); lag compensation; authored arena or SubScene; sound; touch input; per-player scoreboard table.

## 1. Rules

All durations are in simulation ticks at the project's 60 Hz simulation rate; seconds are given for
reading.

**Level.** `?level=4` is Match. `DemoConfig.MaxLevel` becomes 4; `DemoConfig.IsMatchLevel => Level >= 4`.
Levels 1–3 are unchanged; below level 4 the flag and `MatchState` ghosts do not exist.

**Teams.** `Team` is a byte: 0 = Red, 1 = Blue. Every newly spawned player (human or bot) joins the team
with fewer current members; ties go to Red. Team colour tints the player cube; the owned player keeps
the Level 3 translucent server-position cube and additionally gets a white outline ring so it stays
findable among 75 team-mates.

**Arena.** The existing ±30 m square (`ArenaBounds.Default`). Coordinates are XZ; obstacles are 2D
footprints that block everything (they are taller than players).

| Item | Value |
|---|---|
| Red base / flag stand | centre (−25, 0), base radius 3 m |
| Blue base / flag stand | centre (+25, 0), base radius 3 m |
| Boxes (centre, half-extents) | (±12, 0) half (1, 5); (0, ±10) half (4, 1); (±20, ±16) half (2, 2) — 8 boxes, symmetric about both axes |
| Spawn zone | two flank strips behind each base, off the flag stand (Ruling R17): Blue x ∈ [26, 29], Red x ∈ [−29, −26]; |z| ∈ [12, 20], 40 slots per strip (even slots +z, odd −z), golden-ratio x spread like Level 3's `BotPoint` |

**Flags.** One per team.

| Rule | Value |
|---|---|
| Pick up | alive enemy within 1.2 m of a flag that is home or dropped → it is carried by that player |
| Carrier speed | sprint disabled while carrying |
| Drop | carrier dies, disconnects or is despawned → flag dropped at the carrier's position |
| Touch own dropped flag | alive team-mate within 1.2 m → flag returns home immediately |
| Auto-return | dropped for 600 ticks (10 s) → home |
| Capture | carrier is inside own base radius **and** own flag is home → +1 team score, enemy flag home |

**Combat.**

| Rule | Value |
|---|---|
| Fire | held input; server allows one shot per 15 ticks (4 shots/s) |
| Shot | 2D hitscan from the shooter's position along `Aim`, range 40 m, player hit radius `PlayerTuning.Radius` (0.6 m); boxes block; team-mates are ignored (no friendly fire) |
| Damage | 100 health, 34 per hit → 3 hits kill |
| Hit flash | target's `LastHitTick` set; client flashes it for 0.15 s |
| Death | drop flag, `RespawnTick = now + 300` (5 s; was 180, changed with Ruling R17, see spawn-zones-report.md); dead players get zero input and cannot fire, pick up or be hit |
| Respawn | at `RespawnTick`: full health, teleported into own spawn zone |

**Round.** First team to 3 captures, or 18 000 ticks (5 min) → phase `Ended` for 300 ticks (5 s), then a
reset: scores 0, flags home, every player alive at full health and back in its spawn zone, round clock
restarted. A tied timeout is a draw.

## 2. Components and systems

New code lives in `Assets/FlagRush/Demo/Match/` (assembly `FlagRush.Demo`), following the Level 3
patterns: runtime ghost prefabs, attribute-free pure helpers inlined into Burst `ISystem`s, static
knobs in `Sandbox`.

**Pure rules** (no `[BurstCompile]` on the helpers; Unity.Mathematics / Collections only):

| Unit | Purpose |
|---|---|
| `ArenaLayout` | blittable value: boxes (fixed list), base centres, base radius, spawn zones. `ArenaLayout.Match` = the table above; `ArenaLayout.Empty` = no boxes (Level 3) |
| `Obstacles.Slide(ref float3 position, ref float2 velocity, float radius, in ArenaLayout)` | pushes a circle out of any box and removes the velocity component into it. Called from `PlayerMotion.Step`, which gains an `in ArenaLayout` parameter, so the client predicts box collisions exactly as the server simulates them |
| `Hitscan.Cast(float2 origin, float2 dir, float range, in ArenaLayout, NativeArray<float2> positions, NativeArray<byte> teams, NativeArray<bool> hittable, byte shooterTeam, int shooterIndex) → int` | index of the nearest hittable enemy whose circle the ray reaches before any box, else −1 |
| `FlagRules.Step(...)` | pickup, drop, own-flag return, auto-return, capture for both flags given player positions / teams / alive flags; emits `MatchEvent`s |
| `MatchRules` | round clock, win check, reset decision |
| `MatchEvent` | `Kind` (`FlagTaken`, `FlagDropped`, `FlagReturned`, `FlagCaptured`, `PlayerHit`, `PlayerKilled`, `RoundEnded`, `RoundReset`), `Tick`, `Actor`, `Target`, `Team`. Rules append to a `NativeList<MatchEvent>`; systems apply them; tests assert on them |

The Domain model's `Carries` relationship is represented by the flag's carrier field: one edge per flag,
replicated as data, never an object reference.

**Replicated and server state:**

| Unit | Where | Fields |
|---|---|---|
| `PlayerInput` (extended) | both | + `float2 Aim` (unit length or zero), + `bool Fire` |
| `Combatant` (ghost component on the player prefab) | both | `[GhostField] byte Team`, `[GhostField] byte Health`, `[GhostField] uint LastHitTick`, `[GhostField] uint RespawnTick` (0 = alive) |
| `CombatantServer` | server only | `uint NextFireTick` |
| `MatchStats` (ghost component, `SendToOwner`) | both | `Kills`, `Deaths`, `Captures`, `ShotsFired` (`ushort`, wrapping) |
| `FlagGhost` (new runtime prefab, 2 instances, interpolated) | both | `Team`, `State` (Home / Carried / Dropped), `float3 Position`, `int CarrierGhostId` (0 = none), `uint DroppedTick` |
| `MatchState` (ghost singleton) | both | `RedScore`, `BlueScore`, `Phase` (Playing / Ended), `uint PhaseEndTick`, `uint RoundStartTick` |

**Systems and order, per tick:**

1. Client `PlayerInputGatherSystem` adds `Aim` (mouse cursor ray onto the y = 0 plane relative to the
   owned player; gamepad right stick; zero if neither) and `Fire` (left mouse / right trigger).
2. Server `BotBrainSystem` (extended, see §3) writes bot `PlayerInput`.
3. `PlayerMovementSystem` (predicted, both worlds): zero input while `RespawnTick != 0`; sprint ignored
   while carrying; `PlayerMotion.Step` with `ArenaLayout.Match` at level 4, `ArenaLayout.Empty` below.
4. Server, after `PlayerSeparationSystem` (separation also slides out of boxes):
   `HitscanSystem` → `FlagSystem` → `MatchPhaseSystem` → `RespawnSystem`. Each applies its rule
   function's events to the ghosts and to `MatchStats`.
5. Client presentation: team tints and hit flash in `InstancedRenderSystem`; flags and flag stands
   drawn instanced; boxes drawn once; a cosmetic local tracer on fire (the server decides hits);
   scoreboard and readouts in `DemoHud`.

**Spawning.** `PlayerSpawnSystem` assigns `Team` and a spawn-zone position at level 4. A
`MatchSpawnSystem` (server) creates the two flags and `MatchState` at level ≥ 4 and destroys them
below. Bot reconcile keeps Level 3's ≤ 50-per-frame rule; a removed bot that carries a flag drops it.

**Prediction.** `Combatant` replicates to the owner, so the owner stops predicting movement while dead.
A respawn teleport is a server correction the owner rolls back to; the misprediction readout spikes
once, which the readout's meaning text calls out. Shots are not predicted.

**Domain proof (tests only).** `CaptureTheFlagRuleSet : IRuleSet` in `FlagRush.Demo.Tests` drives
`FlagRules` and `MatchRules` through Domain fakes (`IRuleContext`, `IEntityRegistryWriter`,
`IRelationshipWriter` with a `Carries` edge per carried flag) and replays a scripted capture as
`IInput`s, ending in a `FlagCaptured` event. If the `Domain.Tests` fakes are not public, minimal fakes
live in `Demo.Tests`.

## 3. Bots

`BotBrain` keeps its seeded per-index state and gains a role and a state machine. Still server-only,
still writes `PlayerInput`.

| Item | Rule |
|---|---|
| Role | `BotIndex % 3 == 0` → Defender, otherwise Attacker |
| State, checked each tick in order | **Return** if carrying → own base. **Hunt** if own flag is carried → its carrier; Defenders also hunt the nearest enemy inside their half. **GoForFlag** otherwise → enemy flag position (Attackers); Defenders with no target hold within 6 m of their own stand |
| Steering | straight line to the target; if the segment hits a box, steer to that box's nearest corner pushed out by 1.5 m |
| Shooting | nearest alive enemy within 25 m with line of sight (`Hitscan` against boxes) → `Aim` toward it rotated by a seeded random ±6°, `Fire = true` |
| Sprint | when not carrying and target farther than 15 m |

Nearest-enemy search is all-pairs (≈ 22 500 checks per tick at 150 players) in Burst.

## 4. HUD and readouts

- **Scoreboard** (top centre, level 4): Red score — round clock — Blue score; an end-of-round banner
  ("Red wins 3–1", "Draw") during the 5 s `Ended` phase.
- **Panel rows added at level 4**, each with a one-line meaning in the existing style:

| Readout | Meaning |
|---|---|
| Flags | each flag's state (home / carried by you / carried / dropped, seconds to auto-return) |
| You | kills / deaths / captures |
| Shots resolved on server | shots per second across all players; every hit is decided by the server |
| Fire → server confirm | ms from your fire press to the snapshot whose `ShotsFired` includes that shot: the round trip a hit takes to become real |

The Level 3 compact panel, H-key collapse and camera band framing stay as they are.

## 5. Edge cases

- Carrier disconnects, is despawned, or the bot slider removes it → flag dropped at its position.
- A player joining mid-round joins the smaller team and spawns in its zone.
- Round reset revives dead players immediately (clears `RespawnTick`).
- Both flags carried → neither side can capture until its own flag is home.
- A flag dropped inside a box footprint is pushed to the box edge so it stays reachable.
- Level changes below 4 → flags and `MatchState` destroyed; players keep Level 3 behaviour.

## 6. Testing

**Pure NUnit** (PlayMode-filtered, per the Level 3 testing ruling):
`Obstacles.Slide` (push-out, velocity removal, corner), `Hitscan.Cast` (box blocks, team-mate ignored,
dead ignored, range, nearest wins), every `FlagRules` row in §1 including auto-return at exactly 600
ticks and no capture while own flag is away, `MatchRules` (win at 3, timeout, draw, reset), bot state
selection and box-corner steering, and the `CaptureTheFlagRuleSet` adapter's scripted capture.

**PlayMode gate `MatchGateTests`:** level 4 spawns 2 flags and one `MatchState`; scripted input walks
the human to the enemy flag and home → server score becomes 1 and a `FlagCaptured` was recorded;
scripted fire at a stationary bot (bot brain disabled by a test hook) kills it in 3 hits, it drops and
respawns after `RespawnTicks`; level override below 4 despawns the match ghosts. All existing tests (46)
stay green and `Logs/Editor.log` has no "Burst error".

**Browser** (WebGL2 build, `?level=4&bots=149`): a full round plays to its end; frame time ≤ 16.7 ms;
bandwidth and frame time recorded in `docs/m1-web-spike.md`.

## Definition of done

- At `?level=4` with 149 bots, a round plays through captures, kills, flag drops and returns to a win
  or timeout, then resets.
- Browser frame time ≤ 16.7 ms (60 fps) with 150 players.
- All tests in §6 pass; Levels 1–3 behave as before.
- README, roadmap (Level 4 row "4a live"), how-the-prototype-works (Match section) and the index page's
  level links updated.
