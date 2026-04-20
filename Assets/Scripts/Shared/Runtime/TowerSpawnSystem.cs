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

        private void Awake()
        {
            if (!SingletonUtility.TryAssign(Instance, this, value => Instance = value))
                return;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            SingletonUtility.ClearIfCurrent(Instance, this, () => Instance = null);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestPlaceTowerServerRpc(string towerConfigId, Vector2Int gridPos, RpcParams rpcParams = default)
        {
            if (!RuntimeNet.IsServer)
                return;

            var senderClientId = rpcParams.Receive.SenderClientId;

            var config = GameRegistry.Instance?.GetTowerType(towerConfigId);
            if (config == null)
            {
                RuntimeLog.Placement.Warning(RuntimeLog.Code.PlacementInvalidTower,
                    $"Rejected request from client {senderClientId}: invalid tower id '{towerConfigId}'.");
                SendPlacementResultClientRpc(senderClientId, towerConfigId, gridPos, PlacementResult.InvalidTowerType);
                return;
            }

            if (!ServerEvents.TryRaisePlaceTowerRequested(senderClientId, config, gridPos, out var requestResult))
            {
                RuntimeLog.Placement.Error(RuntimeLog.Code.PlacementNoHandler,
                    $"No valid server placement handler result for client {senderClientId}.");
                SendPlacementResultClientRpc(senderClientId, towerConfigId, gridPos, PlacementResult.MissingDependencies);
                return;
            }

            SendPlacementResultClientRpc(senderClientId, towerConfigId, gridPos, requestResult);
        }

        [Rpc(SendTo.Everyone)]
        private void SendPlacementResultClientRpc(ulong requesterClientId, string towerConfigId, Vector2Int gridPos,
            PlacementResult result)
        {
            if (!RuntimeNet.ShouldRunNetworkedClientSystems())
                return;

            if (!RuntimeNet.IsLocalClient(requesterClientId))
                return;

            ClientEvents.RaisePlacementResultReceived(towerConfigId, gridPos, result);
        }
    }
}
