using System;
using Shared.Data;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Runtime
{
    public class TowerSpawnSystem : NetworkBehaviour
    {
        private const string LogPrefix = "[Placement]";

        public static TowerSpawnSystem Instance { get; private set; }

        public static event Action<ulong, TowerType, Vector2Int> OnServerPlaceRequested;

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
        public void RequestPlaceTowerServerRpc(string towerConfigId, Vector2Int gridPos, RpcParams rpcParams = default)
        {
            if (!IsServer) return;

            var senderClientId = rpcParams.Receive.SenderClientId;

            var config = GameRegistry.Instance?.GetTowerType(towerConfigId);
            if (config == null)
            {
                Debug.LogWarning($"{LogPrefix} Rejected request from client {senderClientId}: invalid tower id '{towerConfigId}'.");
                return;
            }

            if (OnServerPlaceRequested == null)
            {
                Debug.LogError($"{LogPrefix} No server placement handler registered for client {senderClientId} request.");
                return;
            }

            OnServerPlaceRequested.Invoke(senderClientId, config, gridPos);
        }
    }
}
