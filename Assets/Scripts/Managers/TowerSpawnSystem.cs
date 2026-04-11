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
                return;
            }

            var config = GameRegistry.Instance?.GetTowerType(towerConfigId);
            if (config == null)
            {
                Debug.LogWarning($"[TowerSpawn] Unknown tower type: {towerConfigId}");
                return;
            }

            var canPlaceOnWater = config.CanBePlacedOnWater;
            if (!gridManager.IsCellAvailable(gridPos, config.Size, canPlaceOnWater))
            {
                Debug.LogWarning($"[TowerSpawn] Cell not available at {gridPos}");
                return;
            }

            if (!gridManager.TryPlaceTowerRuntime(gridPos, config, out var tower))
            {
                Debug.LogWarning($"[TowerSpawn] Failed to place {towerConfigId} at {gridPos}");
                return;
            }

            Debug.Log($"[TowerSpawn] Placed {towerConfigId} at {gridPos}");
        }
    }
}