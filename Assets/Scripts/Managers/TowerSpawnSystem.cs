using Data;
using Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class TowerSpawnSystem : NetworkBehaviour
    {
        public static TowerSpawnSystem Instance { get; private set; }

        [SerializeField]
        private GridManager gridManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestPlaceTowerServerRpc(string towerConfigId, Vector2Int gridPos)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[TowerSpawn] Not server");
                return;
            }

            if (gridManager == null)
            {
                Debug.LogError("[TowerSpawn] GridManager reference is null! Assign it in the Inspector.");
                return;
            }

            var config = GameRegistry.Instance?.GetTowerType(towerConfigId);
            if (config == null)
            {
                Debug.LogWarning($"[TowerSpawn] Unknown tower type: {towerConfigId}");
                return;
            }

            var canPlaceOnWater = config.CanBePlacedOnWater;
            Debug.Log($"[TowerSpawn] Received request for {towerConfigId} at {gridPos} (water={canPlaceOnWater})");

            if (!gridManager.IsCellAvailable(gridPos, config.Size, canPlaceOnWater))
            {
                Debug.LogWarning($"[TowerSpawn] Cell not available at {gridPos} (size={config.Size}, water={canPlaceOnWater})");
                return;
            }

            if (!gridManager.TryPlaceTowerRuntime(gridPos, config, out var tower))
            {
                Debug.LogWarning($"[TowerSpawn] TryPlaceTowerRuntime failed for {towerConfigId} at {gridPos}");
                return;
            }

            Debug.Log($"[TowerSpawn] Placed {towerConfigId} at {gridPos}");
        }
    }
}