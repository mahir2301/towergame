using Data;
using Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class TowerSpawnSystem : NetworkBehaviour
    {
        public static TowerSpawnSystem Instance { get; private set; }

        [SerializeField] private GridManager gridManager;

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
            if (gridManager == null)
                return;

            var config = GameRegistry.Instance?.GetTowerType(towerConfigId);
            if (config == null)
                return;

            if (!gridManager.IsCellAvailable(gridPos, config.Size, config.CanBePlacedOnWater))
                return;

            gridManager.TryPlaceTowerRuntime(gridPos, config, out _);
        }
    }
}
