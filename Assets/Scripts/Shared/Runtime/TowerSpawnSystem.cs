using System;
using Shared.Data;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Runtime
{
    public class TowerSpawnSystem : NetworkBehaviour
    {
        public static TowerSpawnSystem Instance { get; private set; }

        public static event Action<TowerType, Vector2Int> OnServerPlaceRequested;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (Instance == this)
                Instance = null;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestPlaceTowerServerRpc(string towerConfigId, Vector2Int gridPos)
        {
            if (!IsServer) return;

            var config = GameRegistry.Instance?.GetTowerType(towerConfigId);
            if (config == null) return;

            OnServerPlaceRequested?.Invoke(config, gridPos);
        }
    }
}
