# Server-Triggered Client VFX Plan

## Goal

Build a scalable VFX pipeline where the **server authoritatively triggers effects** and **clients render them locally** (pooled, non-networked visual objects).

## Design Principles

- Server decides **when** and **where** a VFX event occurs.
- Clients decide **how** to render it (prefab, particles, local lifetime).
- No `NetworkObject` for cosmetic-only VFX.
- Registry-driven IDs (`GameRegistry`) instead of hard-coded prefab references.
- Deterministic payloads (`seed`, optional params) for consistent visuals across clients.

---

## Data Model

## 1) Add `VfxType` ScriptableObject

Suggested fields:

- `id` (string, unique)
- `displayName` (string)
- `prefab` (`GameObject`, particle root)
- `defaultLifetime` (float)
- `poolPrewarmCount` (int)
- `maxInstances` (int, optional safety cap)

Behavior:

- `VfxType` should implement `IRegistryType`.
- Validation requires non-empty ID, assigned prefab, positive lifetime.

## 2) Extend `GameRegistry`

Add:

- `List<VfxType> vfxTypes`
- lookup dictionary by `id`
- `GetVfxType(string id)`
- include in `HasId` checks
- include in `ValidateAllTypes` checks
- include in `Collect Assets`

---

## Network Contract

## 3) Add VFX event payload struct

Create `VfxSpawnEvent` (network serializable) with:

- `string VfxId`
- `Vector3 Position`
- `Vector3 Normal` (optional, use zero when not relevant)
- `float Scale`
- `float LifetimeOverride` (<=0 means use default)
- `uint Seed`

## 4) Server broadcast path

Add a shared network component (e.g. `VfxEventSystem`) with:

- server-only API: `RequestSpawnVfx(VfxSpawnEvent e)`
- server-to-clients RPC broadcasting VFX event

Rules:

- Server validates `VfxId` exists in `GameRegistry` before sending.
- Only server can send to clients.

---

## Client Rendering

## 5) Add `ClientVfxManager`

Responsibilities:

- Subscribe to incoming VFX events.
- Resolve `VfxType` via `GameRegistry`.
- Spawn from a per-`VfxId` object pool.
- Apply transform/normal/scale/seed.
- Auto-release after lifetime.

Pooling details:

- Pool key = `VfxId`
- Prewarm with `poolPrewarmCount`
- Enforce `maxInstances` to prevent runaway spam
- Release by timer and/or particle completion

---

## Integration Points

## 6) Trigger VFX from server gameplay events

Phase-in targets:

1. Projectile hit -> impact VFX
2. Tower placed -> placement puff/spark VFX
3. Enemy death (future) -> death burst VFX

Each trigger uses `VfxEventSystem.RequestSpawnVfx(...)`.

---

## Validation and Health Checks

## 7) Startup checks

Add health checks for:

- missing `ClientVfxManager` on client scene
- missing `VfxEventSystem` for network relay
- invalid `VfxType` entries in `GameRegistry`

## 8) Runtime checks

Log and skip safely when:

- unknown `VfxId`
- missing prefab
- pool exhausted (if capped)

Use stable log category/codes under `[Vfx]`.

---

## Phase Implementation Plan

## Phase A - Registry + Types

- Add `VfxType`
- Extend `GameRegistry` lists/lookups/validation
- Create initial VFX assets (impact/placement)

## Phase B - Transport

- Add `VfxSpawnEvent`
- Add `VfxEventSystem` RPC bridge (server -> all clients)

## Phase C - Client Renderer

- Add `ClientVfxManager` with pooling
- Hook to VFX event stream

## Phase D - Gameplay Hooks

- Wire projectile hit and placement events
- Add first-pass tuning (lifetimes, pool sizes)

## Phase E - Hardening

- Add health checks and strict validation
- Add per-VFX rate limits if needed

---

## Non-Goals

- Using entity/network objects for cosmetic-only VFX.
- Server-side particle simulation.
- Frame-perfect visual sync (not needed for cosmetic effects).

---

## Definition of Done

- Server can trigger VFX by ID and location.
- Clients render VFX via pooled local prefabs.
- Missing/invalid VFX IDs fail gracefully with clear logs.
- `GameRegistry` fully owns VFX type definitions.
- No cosmetic VFX uses `NetworkObject` replication.
