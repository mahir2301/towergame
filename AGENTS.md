# AGENTS

## Project Reality (verify against code, not design docs)
- This is a Unity 6 project pinned to `6000.3.11f1` (`ProjectSettings/ProjectVersion.txt`).
- `Architecture.md`/`Concept.md` are aspirational in places; trust `Assets/Scripts/**` for current behavior.
- The only enabled build scene is `Assets/Scenes/GameScene.unity` (`ProjectSettings/EditorBuildSettings.asset`).

## Code Boundaries That Matter
- Runtime code is split by asmdefs: `Shared` (core/runtime/data), `Server` (server managers), `Client` (UI/input/visuals).
- `Client` and `Server` both depend on `Shared`; keep cross-layer logic in `Shared` unless it is strictly presentation or server orchestration.

## Startup and Execution Flow
- `NetworkStarter` auto-calls `NetworkManager.Singleton.StartHost()` in `Start()`; Play Mode starts as host unless you change scene wiring.
- World generation is server-authoritative in `WorldGenerationManager.OnNetworkSpawn()`:
  - clear grid state
  - generate water (`GridWaterGenerator`)
  - spawn energy nodes (`GridEnergySourceGenerator`)
  - publish seed/settings via `WorldGenerationState` network variables
- Build/combat gating is controlled by `PhaseManager` (`GamePhase.Building` / `GamePhase.Combat`).

## Placement/Energy Gotchas
- Placement request path: `TowerPlacementController` -> `TowerSpawnSystem.RequestPlaceTowerServerRpc` -> `ServerSpawnManager.TryPlaceTowerRuntime`.
- Client blocks placement over UI and outside build phase; ghost color uses client-side range checks from `ClientObjectRegistry`.
- Server-side spawn path validates occupancy/prefab, then `EnergyNetworkManager` tries to connect the spawned tower to energy; tower can exist but be unpowered.

## Data/Asset Conventions
- `GameRegistry.Instance` uses `Resources.Load<GameRegistry>("GameRegistry")`; keep the asset at `Assets/Resources/GameRegistry.asset`.
- When adding new `TowerType`/`WeaponType`/`EnergyType`/`ClassType` assets, run `GameRegistry` context menu **Collect Assets** to rebuild registry lists.
- Unity serialization mode is force-text (`ProjectSettings/EditorSettings.asset`), so YAML asset diffs are expected.

## Verification Expectations
- There is no repo-local CI/workflow or task runner config to mirror; validate changes in Unity Editor (compile + Play Mode in `GameScene`).
- No test assemblies were found under `Assets/**/Tests`; do not claim automated test coverage unless you add it.

## Hygiene
- Do not edit `Library/`, `Logs/`, `obj/`, or `UserSettings/`.
- Treat `*.csproj` and `*.sln` as generated Unity artifacts (they are ignored in `.gitignore`) unless the user explicitly asks to touch them.
