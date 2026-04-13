using Game.Shared.Data;
using Game.Shared.Grid;
using Unity.Netcode;
using UnityEngine;

namespace Game.Shared.Runtime
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
            var config = GameRegistry.Instance?.GetTowerType(towerConfigId);
            if (config == null) return;

            gridManager.TryPlaceTowerRuntime(gridPos, config, out _);
        }
    }
}
