using Shared.Data;
using Shared.Grid;
using Shared.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Server.Managers
{
    public class ServerSpawnManager : NetworkBehaviour
    {
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

        private void HandlePlaceRequested(TowerType config, Vector2Int gridPos)
        {
            if (!IsServer) return;
            TryPlaceTowerRuntime(gridPos, config, out _);
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
            instance = null;
            if (!IsServer)
                return false;

            if (config?.Prefab == null)
                return false;

            var prefab = config.Prefab.GetComponent<TowerRuntime>();
            if (prefab == null)
                return false;

            if (!gridManager.TryRegisterOccupancy(GridObjectKind.Tower, gridPos, config.Size,
                    config.CanBePlacedOnWater, config.Id, out var record))
                return false;

            instance = Object.Instantiate(prefab, GridManager.TowerWorldPos(gridPos, config), Quaternion.identity);
            instance.Initialize(config, gridPos);
            gridManager.BindRuntimeObject(record.Id, instance.gameObject);

            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                netObj.Spawn();
                gridManager.BindRuntimeNetId(record.Id, netObj.NetworkObjectId);
            }

            return true;
        }
    }
}
