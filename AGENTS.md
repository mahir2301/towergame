# AGENTS

## Project Reality
- Unity 6 pinned to `6000.3.11f1` (`ProjectSettings/ProjectVersion.txt`).
- Enabled build scenes: `Assets/Scenes/MainMenuScene.unity` and `Assets/Scenes/GameScene.unity`.
- Treat `Concept.md` as aspirational; trust `Assets/Scripts/**`.

## Layer Boundaries
- asmdefs: `Shared` (core/runtime/data), `Server` (server orchestration), `Client` (presentation/input/UI).
- Keep cross-layer contracts in `Shared`; keep client/server-specific orchestration out of `Shared` unless it is a strict contract.

## Event Architecture (authoritative convention)
- `GameEvents`: shared cross-layer runtime signals.
- `ClientEvents`: client-only runtime signals (`PlacementResultReceived`, `LocalPlayerChanged`).
- `ServerEvents`: server-only runtime signals (`PlaceTowerRequested`, `TowerSpawned/Despawned`, `EnergySpawned/Despawned`).
- Do not add new static events on feature classes (`TowerRuntime`, `EnergyRuntime`, `TowerSpawnSystem`, etc.); route through one of the three event hubs.

## Startup and Flow
- Session startup is driven by `MainMenuScene` (`MainMenuController`); host/client selection happens before `GameScene` loads.
- No dedicated/headless server runtime path; networking model is host + clients only.
- `RuntimeBootstrap` owns readiness (`Initializing`/`Ready`) and emits `StateChanged`; avoid polling `IsReady` in Update when an event subscription can be used.
- World generation is server-authoritative and event-driven:
  - `WorldGenerationManager` waits for `WorldGenerationState.ServerSpawned`.
  - then generates terrain + energy and publishes seed/settings once.

## Runtime Side Guards (authoritative)
- Use `RuntimeNet.ShouldRunMenuSystems()` for pre-session/menu logic (`MainMenuScene`, menu UI wiring, host/join controls).
- Use `RuntimeNet.ShouldRunNetworkedClientSystems()` for gameplay client logic that must only run once connected (`GameScene` visuals/input/UI).
- Use `RuntimeNet.IsServer` for authoritative server writes/spawns/state mutation.
- Prefer `RuntimeNet` helpers over direct `NetworkManager.Singleton.Is*` checks unless accessing a Netcode API that has no helper equivalent.
- For server-only and gameplay-client-only `MonoBehaviour`s, disable early in `Awake`/`Start` when guard fails (`enabled = false`) to avoid side leaks.

## Registry Is Source of Truth
- `GameRegistry.Instance` loads from `Resources/GameRegistry` (`Assets/Resources/GameRegistry.asset`).
- Content lookup must use registry ids/lookups (`GetTowerType`, `GetEnergyType`, `GetWeaponType`, `GetEntityType`).
- World generation energy types are resolved from registry (`energyTypeIds` override list, otherwise all registry energy types).
- After adding/changing content assets, run `GameRegistry` context menu **Collect Assets**, then validate with **Validate Registry**.

## Placement Pipeline
- Client: `TowerPlacementController` validates + sends `TowerSpawnSystem.RequestPlaceTowerServerRpc`.
- Server: `TowerSpawnSystem` dispatches placement request via `ServerEvents.PlaceTowerRequested`; `ServerSpawnManager` handles placement.
- Result returns to requesting client via `ClientEvents.PlacementResultReceived`.
- `OutOfEnergyRange` is allowed (soft-fail visual/feedback state), not hard rejection.

## Scene Wiring Gotchas
- `NetworkManager` lives in `MainMenuScene` and must stay at scene root (do not nest).
- Keep required shared networked systems present and referenced (`GridManager`, `PhaseManager`, `TowerSpawnSystem`, `WorldGenerationState`, `ServerSpawnManager`, `WorldGenerationManager`).

## Verification
- No CI/task runner/tests in repo; verify in Unity Editor (compile + Play Mode through `MainMenuScene` into `GameScene`).
- Validate both host flow and client-join flow for networking changes.

## Hygiene
- Never edit `Library/`, `Logs/`, `obj/`, `UserSettings/`.
- Treat `*.csproj` and `*.sln` as generated Unity artifacts.
