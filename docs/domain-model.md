# FlagRush domain model (iteration 1: contracts only)

`Assets/FlagRush/Domain` is a plain-C# assembly (`noEngineReferences: true`). It contains **interfaces,
abstract bases, value-type ids and events** and nothing game-specific: there is no `Player`, `Flag` or
`Vehicle` class. Those are compositions the prototype decides on later, and they can be "scaled down"
into ECS components because every contract here is id-based and struct-friendly.

## The one rule

**Relationships are data, never object references.** "Avatar 7 carries flag 12" is a
`Relationship(Carries, 7, 12)` row in an `IRelationshipWriter`, not a field on either entity. Consequences:

- Any entity can be looked up by id or by aspect through `IEntityRegistry`, never held across ticks.
- Ownership, carrying, containment (slot = ordinal), membership and control are all the same shape,
  so stealing, dropping, trading and reconnecting are edge edits.
- The whole world state is `entities + edges`, which is exactly what a snapshot, a save file or a
  netcode ghost needs to serialize.

## Aspects (compose these; do not subclass)

| Aspect | Means | Later becomes |
|---|---|---|
| `IEntity` | has an `EntityId` | the ECS entity |
| `IPositioned` | has a `Point3` | `LocalTransform` |
| `IHasTeam` | belongs to a `TeamId` | `TeamComponent` |
| `IHasHealth` | can take damage; `IsAlive` derived | `Health` |
| `IOwnable` | can be owned (edge: `Owns`) | ownership buffer |
| `ICarryable` | can be picked up; has `CarryCost` (edge: `Carries`) | `Carryable` |
| `ICarrier` | can pick things up; has `CarryCapacity` | `Carrier` |
| `IContainer` | has `SlotCount` slots (edge: `Contains`, ordinal = slot) | inventory buffer |
| `IScorable` | `Xp`, `Score`, `Money` | `Progression` |

A CTF avatar is `ICarrier + IHasTeam + IHasHealth + IContainer`. A flag is `ICarryable + IHasTeam`.
A vehicle is `ICarrier + IHasHealth + IOwnable` (trunk = its `IContainer`). A production machine is
`IOwnable + IContainer + IPositioned` plus an `IWorkItem` that ticks.

## Simulation contract

```
ISimulation.Step(Tick, IReadOnlyList<IInput>) -> IReadOnlyList<IGameEvent>
```

- `IRuleSet` is a game mode (`Initialize`, `Apply`). It holds no state; everything lives in the
  `IRuleContext` (entities, relationships, seeded `IRandomSource`, `ILogSink`, `Emit`).
- Events are the **only** output. Presentation, replication, persistence and tests consume events.
- `ISnapshotable<T>` is the seam for client prediction / rollback.
- Deterministic by construction: same tick + same inputs + same seed = same events.

## Ports (what the engine plugs into)

| Port | Purpose |
|---|---|
| `IReplicationChannel : IInputSource` | the netcode seam: publish events, drain inputs. An in-process loopback is a valid channel (single-process "simulator" build). |
| `IPersistenceStore<TKey,TRecord>` / `IProfileRepository` | durability. Memory in tests, file on a dedicated server, cloud later. |
| `IClock`, `IRandomSource`, `ILogSink` | injected time, randomness, diagnostics. |
| `IWorkScheduler` / `IWorkItem` | background work that ticks (machines, bot brains, spatial index). No threading in the contract. |

## Identity across connections

`AuthId` (stable, client-held) vs `PlayerId` (per connection). `ISessionRegistry.Connect(auth, newPlayerId)`
resumes an existing binding, keeping the avatar. `IPlayerProfile` (keyed by `AuthId`) is what the
repository stores; it is `IScorable` and carries an `IInventorySnapshot` of `SlotEntry`s.

## How this maps to the posting

| Posting bullet | Contract that exists for it |
|---|---|
| 100-150 players, modern networking | `ISimulation` pure step + `IReplicationChannel` + id-only state (relevancy/snapshots need no object graph) |
| Inventory, money, XP survive disconnects (Rust) | `AuthId`, `ISessionRegistry`, `IPlayerProfile`, `IProfileRepository` |
| Production & crafting (DarkRP) | `IWorkItem`/`IWorkScheduler` + `IContainer` + `Owns` edges |
| Physical inventory: carried, stored, dropped, stolen | `ICarryable`/`ICarrier` + `Carries`/`Contains`/`Owns` edges |
| Vehicles: driving, damage, trunk storage | `ICarrier + IHasHealth + IContainer` + `Controls` edge |
| Oversimplifier | 9 aspects, 5 edge kinds, 1 step function |

## Tests

`Assets/FlagRush/Domain.Tests` holds in-memory fakes (the only concrete implementations this iteration)
and NUnit tests proving the contracts compose: carrying as data, aspect queries, inputs to events,
profile round-trip, reconnect resumes a session, work scheduling.
