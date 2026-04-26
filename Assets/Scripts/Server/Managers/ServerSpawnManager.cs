using Shared.Data;
using Shared.Grid;
using Shared;
using Shared.Runtime;
using Shared.Runtime.Placeables;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace Server.Managers
{
    public class ServerSpawnManager : NetworkBehaviour
    {
        public static ServerSpawnManager Instance { get; private set; }

        [SerializeField] private GridManager gridManager;
        private readonly SubscriptionGroup subscriptions = new();

        private void Awake()
        {
            if (!RuntimeNet.IsServer)
            {
                enabled = false;
                return;
            }

            if (!SingletonUtility.TryAssign(Instance, this, value => Instance = value))
                return;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!RuntimeNet.IsServer)
                return;

            subscriptions.UnbindAll();
            subscriptions.Add(() => ServerEvents.PlaceablePlacementRequested += HandlePlaceablePlaceRequested,
                () => ServerEvents.PlaceablePlacementRequested -= HandlePlaceablePlaceRequested);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            subscriptions.UnbindAll();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            subscriptions.UnbindAll();

            SingletonUtility.ClearIfCurrent(Instance, this, () => Instance = null);
        }

        private void HandlePlaceablePlaceRequested(ulong requesterClientId, PlaceableType type, Vector2Int gridPos,
            System.Action<PlacementResponse> respond)
        {
            if (!RuntimeNet.IsServer)
            {
                respond?.Invoke(PlacementResponse.Create(type != null ? type.Id : string.Empty, gridPos, false,
                    PlacementCodes.ServerUnavailable));
                return;
            }

            TryPlacePlaceableRuntime(gridPos, type, out _, out var response);
            if (PlacementValidator.IsPlacementAllowed(response, allowOutOfRange: true))
            {
                RuntimeLog.Placement.Info(RuntimeLog.Code.PlacementAccepted,
                    $"Client {requesterClientId} placed '{type.Id}' at {gridPos} (result={response.Code}).");
            }
            else
            {
                RuntimeLog.Placement.Warning(RuntimeLog.Code.PlacementRejected,
                    $"Rejected client {requesterClientId} placement for '{type.Id}' at {gridPos}: {response.Code}.");
            }

            respond?.Invoke(response);
        }

        public bool TryPlacePlaceableRuntime(Vector2Int gridPos, PlaceableType type, out PlaceableBehavior instance,
            out PlacementResponse response, int energyCapacityOverride = -1)
        {
            instance = null;
            if (!RuntimeNet.IsServer)
            {
                response = PlacementResponse.Create(type != null ? type.Id : string.Empty, gridPos, false,
                    PlacementCodes.ServerUnavailable);
                return false;
            }

            if (gridManager == null || EnergyNetworkManager.Instance == null || PhaseManager.Instance == null)
            {
                response = PlacementResponse.Create(type != null ? type.Id : string.Empty, gridPos, false,
                    PlacementCodes.MissingDependencies);
                return false;
            }

            response = PlacementValidator.ValidatePlacement(gridPos, type, gridManager, PhaseManager.Instance,
                EnergyNetworkManager.Instance.IsPositionInRange);
            if (!PlacementValidator.IsPlacementAllowed(response, allowOutOfRange: true))
                return false;

            if (type?.Prefab == null)
            {
                response = PlacementResponse.Create(type != null ? type.Id : string.Empty, gridPos, false,
                    PlacementCodes.InvalidPrefab);
                return false;
            }

            var prefab = type.Prefab.GetComponent<PlaceableBehavior>();
            if (prefab == null)
            {
                response = PlacementResponse.Create(type.Id, gridPos, false, PlacementCodes.InvalidPrefab);
                return false;
            }

            if (!gridManager.TryRegisterOccupancy(type.Id, gridPos, type.Size, out var record))
            {
                response = PlacementResponse.Create(type.Id, gridPos, false, PlacementCodes.CellBlocked);
                return false;
            }

            instance = Object.Instantiate(prefab, GridManager.PlaceableWorldPos(gridPos, type), Quaternion.identity);
            instance.Initialize(type, gridPos);
            if (energyCapacityOverride >= 0 && instance is EnergyRuntime energy)
                energy.SetMaxCapacity(energyCapacityOverride);
            instance.OnPlaced();
            gridManager.BindRuntimeObject(record.Id, instance.gameObject);

            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                netObj.Spawn();
                gridManager.BindRuntimeNetId(record.Id, netObj.NetworkObjectId);
            }

            response = PlacementResponse.Create(type.Id, gridPos, true, PlacementCodes.Success);
            return true;
        }

        public bool HasRequiredReferences(out string issue)
        {
            return ReferenceValidator.Validate(out issue,
                (gridManager, nameof(gridManager)));
        }
    }
}
