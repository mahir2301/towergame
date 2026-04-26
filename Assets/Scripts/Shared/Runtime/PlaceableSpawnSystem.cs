using Shared.Data;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Runtime
{
    public class PlaceableSpawnSystem : NetworkBehaviour
    {
        public static PlaceableSpawnSystem Instance { get; private set; }

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
        public void RequestPlaceableServerRpc(string placeableTypeId, Vector2Int gridPos, RpcParams rpcParams = default)
        {
            if (!RuntimeNet.IsServer)
                return;

            var senderClientId = rpcParams.Receive.SenderClientId;
            var type = GameRegistry.Instance?.GetPlaceableType(placeableTypeId);
            if (type == null)
            {
                SendPlacementResponseClientRpc(senderClientId,
                    PlacementResponse.Create(placeableTypeId, gridPos, false, PlacementCodes.InvalidType));
                return;
            }

            if (!ServerEvents.TryRaisePlaceablePlacementRequested(senderClientId, type, gridPos, out var response))
            {
                response = PlacementResponse.Create(placeableTypeId, gridPos, false, PlacementCodes.MissingDependencies);
            }

            SendPlacementResponseClientRpc(senderClientId, response);
        }

        [Rpc(SendTo.Everyone)]
        private void SendPlacementResponseClientRpc(ulong requesterClientId, PlacementResponse response)
        {
            if (!RuntimeNet.ShouldRunNetworkedClientSystems())
                return;

            if (!RuntimeNet.IsLocalClient(requesterClientId))
                return;

            ClientEvents.RaisePlacementResponseReceived(response);
        }
    }
}
