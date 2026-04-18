using Shared.Data;
using Shared.Grid;
using Shared;
using Shared.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Server.Managers
{
    public class ServerSpawnManager : NetworkBehaviour
    {
        private const string LogPrefix = "[Placement]";

        public static ServerSpawnManager Instance { get; private set; }

        [SerializeField] private GridManager gridManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            TowerSpawnSystem.OnServerPlaceRequested += HandlePlaceRequested;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            TowerSpawnSystem.OnServerPlaceRequested -= HandlePlaceRequested;

            if (Instance == this)
                Instance = null;
        }

        private void HandlePlaceRequested(ulong requesterClientId, TowerType config, Vector2Int gridPos)
        {
            if (!IsServer) return;

            TryPlaceTowerRuntime(gridPos, config, out _, out var result);
            if (result == PlacementResult.Success)
            {
                Debug.Log($"{LogPrefix} Client {requesterClientId} placed '{config.Id}' at {gridPos}.");
                return;
            }

            Debug.LogWarning($"{LogPrefix} Rejected client {requesterClientId} placement for '{config.Id}' at {gridPos}: {result}.");
        }

        public bool TryPlaceEnergyRuntime(Vector2Int gridPos, EnergyType config, int maxCapacity,
            out EnergyRuntime instance)
        {
            instance = null;
            if (!IsServer)
                return false;

            if (config?.Prefab == null)
                return false;

            var prefab = config.Prefab.GetComponent<EnergyRuntime>();
            if (prefab == null)
                return false;

            if (!gridManager.TryRegisterOccupancy(GridObjectKind.EnergySource, gridPos, Vector2Int.one, false,
                    config.Id, out var record))
                return false;

            instance = Object.Instantiate(prefab, gridManager.GridToWorld(gridPos), Quaternion.identity);
            instance.Initialize(config, maxCapacity, gridPos);
            gridManager.BindRuntimeObject(record.Id, instance.gameObject);

            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                netObj.Spawn();
                gridManager.BindRuntimeNetId(record.Id, netObj.NetworkObjectId);
            }

            return true;
        }

        public bool TryPlaceTowerRuntime(Vector2Int gridPos, TowerType config, out TowerRuntime instance)
        {
            return TryPlaceTowerRuntime(gridPos, config, out instance, out _);
        }

        public bool TryPlaceTowerRuntime(Vector2Int gridPos, TowerType config, out TowerRuntime instance,
            out PlacementResult result)
        {
            instance = null;
            if (!IsServer)
            {
                result = PlacementResult.ServerUnavailable;
                return false;
            }

            if (gridManager == null || EnergyNetworkManager.Instance == null || PhaseManager.Instance == null)
            {
                result = PlacementResult.MissingDependencies;
                return false;
            }

            if (PhaseManager.Instance.CurrentPhase != GamePhase.Building)
            {
                result = PlacementResult.OutOfBuildPhase;
                return false;
            }

            if (config?.Prefab == null)
            {
                result = PlacementResult.InvalidTowerType;
                return false;
            }

            if (config.ClassType == null)
            {
                result = PlacementResult.InvalidTowerType;
                return false;
            }

            if (!gridManager.IsValidPosition(gridPos))
            {
                result = PlacementResult.InvalidGridPosition;
                return false;
            }

            var prefab = config.Prefab.GetComponent<TowerRuntime>();
            if (prefab == null)
            {
                result = PlacementResult.InvalidPrefab;
                return false;
            }

            if (!gridManager.TryRegisterOccupancy(GridObjectKind.Tower, gridPos, config.Size,
                    config.CanBePlacedOnWater, config.Id, out var record))
            {
                result = PlacementResult.CellBlocked;
                return false;
            }

            instance = Object.Instantiate(prefab, GridManager.TowerWorldPos(gridPos, config), Quaternion.identity);
            instance.Initialize(config, gridPos);
            gridManager.BindRuntimeObject(record.Id, instance.gameObject);

            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                netObj.Spawn();
                gridManager.BindRuntimeNetId(record.Id, netObj.NetworkObjectId);
            }

            result = PlacementResult.Success;
            return true;
        }

        public bool HasRequiredReferences(out string issue)
        {
            if (gridManager == null)
            {
                issue = "gridManager is not assigned.";
                return false;
            }

            issue = null;
            return true;
        }
    }
}
