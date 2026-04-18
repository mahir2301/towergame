# Entity Refactor Plan (Server Authoritative)

This document defines a full rewrite plan for gameplay entities in this project.

## Goals

- Build one unified **entity system** for `Player`, `Enemy`, and `Projectile`.
- Make entities **server authoritative** for simulation and outcomes.
- Allow clients to control **only their own player entity** through validated input commands.
- Remove scene-coupled player assumptions; player spawn/despawn is fully handled by server.
- Keep the implementation clean and integrated; do **not** preserve legacy compatibility.

## Non-Goals

- Partial adapters to old player/projectile runtime paths.
- Maintaining old event APIs for deprecated systems.
- Supporting mixed old/new authority models.

---

## Current-State Constraints (from code)

- Player currently exists in-scene in `Assets/Scenes/GameScene.unity` with:
  - `PlayerController`
  - `PlayerRuntime`
  - `PlayerWeaponController`
  - `NetworkObject`
- `NetworkManager` currently has `PlayerPrefab: null` and manual/in-scene behavior.
- Player movement is currently client-driven (`PlayerController` moves Rigidbody locally).
- Weapon/projectile logic is split between `PlayerRuntime` and `Projectile`.

This refactor removes these assumptions.

---

## Architecture Overview

### Core Principles

1. **Single entity abstraction** for all runtime actors.
2. **Server simulation authority** for transforms, combat, health, and projectiles.
3. **Client intent only**: clients submit input/commands; server validates and applies.
4. **Ownership enforcement**: command source client must own target player entity.
5. **No scene-default player object**.
6. **Registry-driven entities**: entity prefabs/types are resolved through `GameRegistry`, not ad-hoc serialized scene references.

### Entity Kinds

- `Player`
- `Enemy`
- `Projectile`

### Proposed Module Layout

- `Assets/Scripts/Shared/Entities/`
  - `EntityKind.cs`
  - `EntityId.cs`
  - `EntityRuntime.cs`
  - `EntityDefinition.cs` (if needed for prefab/config mapping)
  - `EntityManager.cs`
  - `EntitySimulationSystem.cs`
  - `EntityCommandTypes.cs`
  - `EntityCommandGateway.cs`
- `Assets/Scripts/Shared/Data/`
  - `EntityType.cs` (id, kind, prefab)
- `Assets/Scripts/Server/Entities/`
  - `ServerEntityBootstrap.cs`
  - `EnemySpawnSystem.cs` (stub + later implementation)
  - `ServerPlayerSpawnRules.cs` (optional split)
- `Assets/Scripts/Client/Entities/`
  - `LocalEntityResolver.cs`
  - `PlayerInputSender.cs`
  - `OwnedPlayerViewBinder.cs`
- `Assets/Scripts/Shared/Combat/`
  - `WeaponSystem.cs`
  - `ProjectileSystem.cs`

Use existing asmdef boundaries:

- `Shared`: entity contracts, runtime state, validation, command schemas.
- `Server`: spawning/orchestration/AI.
- `Client`: input and presentation binding only.

---

## Authority Model

### Server Authority

Server owns and decides:

- entity creation/destruction
- movement/rotation updates
- health and damage
- weapon cooldown progression
- projectile spawn/movement/collision/despawn
- ownership assignments

### Client Responsibility

Client only:

- reads local input
- sends command frames for owned player entity
- renders replicated state

### Ownership Rules

- Player entity has `OwnerClientId = connecting client id`.
- Enemy/projectile entities have `OwnerClientId = ulong.MaxValue` (or explicit server-owner semantics).
- All server command handlers must validate:
  - sender client id matches entity owner
  - command payload is valid

Reject and log invalid ownership attempts with stable log codes.

---

## Data and Networking Contracts

### Base Entity Runtime

Each entity should provide replicated fields (exact implementation may vary):

- `NetworkVariable<uint> EntityId`
- `NetworkVariable<EntityKind> Kind`
- `NetworkVariable<ulong> OwnerClientId`
- `NetworkVariable<FixedString64Bytes> EntityTypeId`
- `NetworkVariable<float> Health` (where applicable)
- transform sync via `NetworkTransform` or explicit variables (recommended: keep `NetworkTransform` for now)

`EntityTypeId` must reference a valid `GameRegistry` `EntityType` entry. Kind should be derived from registry type definition (server-side).

### GameRegistry Integration (Required)

- Add `EntityType` ScriptableObject with:
  - `Id`
  - `DisplayName`
  - `EntityKind Kind`
  - `EntityRuntime Prefab`
- Extend `GameRegistry` to collect and look up `EntityType` entries.
- Add lookup APIs:
  - `GetEntityType(string id)`
  - `GetEntityType(EntityKind kind)`
- Update `Collect Assets` to include entity types.
- Entity spawning systems must use registry lookups (id/kind) rather than hardcoded prefab links.

### Command Types

Define explicit command structs (versionable):

- `PlayerMoveCommand`
  - `uint EntityId`
  - `Vector2 MoveInput`
  - `Vector3 AimWorldPosition` (or yaw)
  - `double ClientTime`
  - `uint Sequence`
- `PlayerActionCommand`
  - `uint EntityId`
  - `ActionType` (`Fire`, `SwitchWeapon`, etc.)
  - action payload (weapon index, target point)
  - `uint Sequence`

### RPC Gateways

- Single ingress on player-owned path:
  - `SubmitMoveCommandServerRpc(PlayerMoveCommand cmd, RpcParams rpcParams)`
  - `SubmitActionCommandServerRpc(PlayerActionCommand cmd, RpcParams rpcParams)`
- Server validates ownership and payload, then queues for simulation tick.

### Event Surface

Replace old player-centric events with entity-centric events in `GameEvents`:

- `EntitySpawned(EntityRuntime entity)`
- `EntityDespawned(EntityRuntime entity)`
- `OwnedPlayerEntityReady(EntityRuntime entity)` (client-side)
- `EntityDamaged(uint entityId, float amount, uint sourceEntityId)` (optional)

Deprecate `PlayerRuntime.LocalPlayer` and its spawn/despawn static events.

---

## Phase Plan

## Phase 0 - Preflight and Hard Cutover Rules

### Deliverables

- Decide and document hard cutover: no coexistence path.
- Freeze old path additions (no new code in old player/projectile runtimes).

### Tasks

- Mark old classes as migration targets:
  - `PlayerRuntime`
  - `PlayerController`
  - `PlayerWeaponController`
  - `Projectile`
- Confirm `GameScene` edits are allowed (player object removed later).

### Exit Criteria

- Team agrees to full rewrite path and no backward compatibility.

---

## Phase 1 - Entity Foundation

### Deliverables

- `EntityKind`, `EntityId`, `EntityRuntime`, `EntityManager`.
- Global registry for active entities by id.
- Standardized logging for entity subsystem (new codes in `RuntimeLog`).
- Initial `GameRegistry` integration for entity definitions.

### Tasks

- Create entity core files under `Shared/Entities`.
- Add server-side entity id allocator in `EntityManager`.
- Add map structures:
  - `entityId -> EntityRuntime`
  - `clientId -> playerEntityId`
- Add safe lifecycle registration on spawn/despawn.
- Add `Shared/Data/EntityType` and include it in `GameRegistry` lookups + `Collect Assets`.
- In `EntityRuntime` registration, validate entity type id against `GameRegistry`; log clear error/warning codes when missing.

### Exit Criteria

- Any spawned entity receives id/kind/owner and appears in manager registry.
- Any spawned entity also has a valid `EntityTypeId` resolved via `GameRegistry`.

---

## Phase 2 - Server-Managed Player Lifecycle (No Scene Player)

### Deliverables

- `ServerEntityBootstrap` spawns player entity on client connect.
- Player despawn and ownership cleanup on disconnect.
- `GameScene` has **no default player object**.

### Tasks

- Add server callbacks:
  - `NetworkManager.OnClientConnectedCallback`
  - `NetworkManager.OnClientDisconnectCallback`
- Implement `SpawnPlayerForClient(clientId)` in `EntityManager`.
- Resolve player prefab through `GameRegistry.GetEntityType(EntityKind.Player)`.
- Spawn entity prefab with ownership for that client.
- Remove in-scene player object and related serialized references from `GameScene`.
- Ensure host mode path works (local client also receives spawned player entity).

### Exit Criteria

- Starting host/client creates player entities only via server callbacks.
- No player object is present in scene by default.

---

## Phase 3 - Input Command Pipeline and Server Movement

### Deliverables

- Client input sender that targets only owned player entity.
- Server command gateway with ownership validation.
- Server simulation updates player movement/rotation.

### Tasks

- Implement `LocalEntityResolver` to discover local owned player entity.
- Implement `PlayerInputSender` to collect input and send commands.
- Implement command queue per player entity on server.
- Move movement simulation from client controller to server simulation tick.
- Disable/remove local authoritative Rigidbody movement path.

### Validation Rules

- Reject command when:
  - sender does not own target entity
  - command has invalid payload
- Add stable log codes for rejection reasons.

### Exit Criteria

- Client can move only own entity.
- Spoofed entity id commands are rejected with logs.

---

## Phase 4 - Weapon and Combat Command Refactor

### Deliverables

- Weapon switch/fire handled through command pipeline.
- Server-only weapon cooldown and energy consumption logic.

### Tasks

- Move weapon command handling into `WeaponSystem`.
- Validate phase/combat gating server-side (never trust client).
- Remove direct old RPC usage that bypasses entity ownership model.

### Exit Criteria

- Firing/switching works for owning player only.
- Cooldowns and energy checks are authoritative on server.

---

## Phase 5 - Projectile as First-Class Entity

### Deliverables

- Replace old `Projectile` runtime path with `Projectile` entity kind.
- Projectiles are spawned, simulated, and despawned through entity systems.

### Tasks

- Create projectile entity prefab/runtime.
- Resolve projectile prefab through `GameRegistry.GetEntityType(EntityKind.Projectile)`.
- Implement `ProjectileSystem` tick:
  - movement
  - max lifetime/range
  - collision queries
  - server-side hit resolution
- Emit entity events for projectile spawn/hit/despawn.
- Remove old projectile class usage in weapon flow.

### Exit Criteria

- Every projectile is an entity managed by `EntityManager`.
- No legacy projectile spawn path remains.

---

## Phase 6 - Client Binding and Presentation Integration

### Deliverables

- Camera/UI/animation bind to local owned player entity at runtime.
- Local scripts no longer assume scene references to static player object.

### Tasks

- Add `OwnedPlayerViewBinder`:
  - bind camera follow target to owned player entity transform
  - bind local HUD/weapon presentation to owned entity state
- Update affected client systems:
  - `PlayerWeaponController` rewrite to entity binding
  - any UI expecting `PlayerRuntime.LocalPlayer`

### Exit Criteria

- Local player view works after dynamic spawn.
- No hard-coded scene player references remain.

---

## Phase 7 - Remove Legacy Runtime Paths

### Deliverables

- Delete old player/projectile authority paths and obsolete events.

### Tasks

- Remove or fully rewrite:
  - `PlayerRuntime` old ownership assumptions
  - `PlayerController` local authority movement logic
  - legacy projectile script and usages
- Remove deprecated `GameEvents` entries if replaced.
- Clean scene bindings and null serialized references.

### Exit Criteria

- Build compiles with no legacy player/projectile code path in use.

---

## Phase 8 - Validation and Hardening

### Multiplayer Verification Matrix

- Host + one client:
  - each controls only own player
  - cannot control other player via tampered command
- Disconnect/reconnect:
  - player entity despawns/respawns correctly
  - ownership maps stay correct
- Combat:
  - weapon commands apply correctly
  - projectile entities spawn/hit/despawn correctly
- Phase gating:
  - server enforces build/combat restrictions

### Stability Checks

- lifecycle unsubscription safety in spawn/despawn events
- no singleton race assumptions for local player binding
- logging coverage with stable reason codes

---

## Implementation Notes for LLMs

1. **Do not preserve old API compatibility** unless explicitly requested later.
2. Favor straightforward composition over inheritance-heavy hierarchies.
3. Keep all authority checks server-side even if client checks exist.
4. Use existing logging conventions via `RuntimeLog` with new entity codes.
5. Keep per-phase PRs small enough to validate in Unity Play Mode.
6. Do not edit Unity generated folders (`Library/`, `Logs/`, `obj/`, `UserSettings/`).
7. Update scene YAML intentionally and verify references after removing in-scene player.

---

## Suggested New Log Codes

Add these (or equivalent) to `RuntimeLog.Code`:

- `EN-001` entity spawned
- `EN-002` entity despawned
- `EN-003` player assigned to client
- `EN-004` invalid ownership command rejected
- `EN-005` invalid command payload rejected
- `EN-006` missing entity for command
- `EN-007` projectile hit resolved
- `EN-008` disconnect cleanup complete
- `EN-009` missing entity type definition in registry

---

## Definition of Done

- No in-scene default player object exists.
- Player spawn/despawn is fully server-managed.
- Player and enemy are both entities in one system.
- Projectile is also an entity in the same system.
- Server is authoritative for simulation and combat outcomes.
- Client can control only its owned player entity.
- Legacy authority paths are removed, not merely bypassed.
