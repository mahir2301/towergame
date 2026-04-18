using System;
using Shared;
using Shared.Data;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Runtime
{
    public class TowerSpawnSystem : NetworkBehaviour
    {
        public static TowerSpawnSystem Instance { get; private set; }

        public static event Action<ulong, TowerType, Vector2Int, Action<PlacementResult>> OnServerPlaceRequested;
        public static event Action<string, Vector2Int, PlacementResult> PlacementResultReceived;

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
                RuntimeLog.Placement.Warning(RuntimeLog.Code.PlacementInvalidTower,
                    $"Rejected request from client {senderClientId}: invalid tower id '{towerConfigId}'.");
                SendPlacementResultClientRpc(senderClientId, towerConfigId, gridPos, PlacementResult.InvalidTowerType);
                return;
            }

            if (OnServerPlaceRequested == null)
            {
                RuntimeLog.Placement.Error(RuntimeLog.Code.PlacementNoHandler,
                    $"No server placement handler registered for client {senderClientId} request.");
                SendPlacementResultClientRpc(senderClientId, towerConfigId, gridPos, PlacementResult.MissingDependencies);
                return;
            }

            PlacementResult? requestResult = null;
            OnServerPlaceRequested.Invoke(senderClientId, config, gridPos, result => requestResult = result);

            if (!requestResult.HasValue)
            {
                RuntimeLog.Placement.Error(RuntimeLog.Code.PlacementNoHandler,
                    $"Server placement handler did not return a result for client {senderClientId}.");
                SendPlacementResultClientRpc(senderClientId, towerConfigId, gridPos, PlacementResult.MissingDependencies);
                return;
            }

            SendPlacementResultClientRpc(senderClientId, towerConfigId, gridPos, requestResult.Value);
        }

        [Rpc(SendTo.Everyone)]
        private void SendPlacementResultClientRpc(ulong requesterClientId, string towerConfigId, Vector2Int gridPos,
            PlacementResult result)
        {
            if (!IsClient)
                return;

            if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClientId != requesterClientId)
                return;

            GameEvents.RaisePlacementResultReceived(towerConfigId, gridPos, result);
            PlacementResultReceived?.Invoke(towerConfigId, gridPos, result);
        }
    }
}
