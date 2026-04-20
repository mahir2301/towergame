using Shared.Data;
using Shared.Grid;
using Shared;
using Shared.Runtime;
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
            subscriptions.Add(() => ServerEvents.PlaceTowerRequested += HandlePlaceRequested,
                () => ServerEvents.PlaceTowerRequested -= HandlePlaceRequested);
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

        private void HandlePlaceRequested(ulong requesterClientId, TowerType config, Vector2Int gridPos,
            System.Action<PlacementResult> respond)
        {
            if (!RuntimeNet.IsServer)
            {
                respond?.Invoke(PlacementResult.ServerUnavailable);
                return;
            }

            TryPlaceTowerRuntime(gridPos, config, out _, out var result);
            if (PlacementValidator.IsPlacementAllowed(result, allowOutOfEnergyRange: true))
            {
                RuntimeLog.Placement.Info(RuntimeLog.Code.PlacementAccepted,
                    $"Client {requesterClientId} placed '{config.Id}' at {gridPos} (result={result}).");
            }
            else
            {
                RuntimeLog.Placement.Warning(RuntimeLog.Code.PlacementRejected,
                    $"Rejected client {requesterClientId} placement for '{config.Id}' at {gridPos}: {result}.");
            }

            respond?.Invoke(result);
        }

        public bool TryPlaceEnergyRuntime(Vector2Int gridPos, EnergyType config, int maxCapacity,
            out EnergyRuntime instance)
        {
            instance = null;
            if (!RuntimeNet.IsServer)
                return false;

            if (config?.Prefab == null)
                return false;

            var prefab = config.Prefab.GetComponent<EnergyRuntime>();
            if (prefab == null)
                return false;

            if (!gridManager.TryRegisterOccupancy(GridObjectKind.EnergySource, gridPos, Vector2Int.one, false,
                    config.Id, out var record))
                return false;

            instance = Object.Instantiate(prefab, gridManager.GridToWorld(gridPos), Quaternion.identity);
            instance.Initialize(config, maxCapacity, gridPos);
            gridManager.BindRuntimeObject(record.Id, instance.gameObject);

            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                netObj.Spawn();
                gridManager.BindRuntimeNetId(record.Id, netObj.NetworkObjectId);
            }

            return true;
        }

        public bool TryPlaceTowerRuntime(Vector2Int gridPos, TowerType config, out TowerRuntime instance)
        {
            return TryPlaceTowerRuntime(gridPos, config, out instance, out _);
        }

        public bool TryPlaceTowerRuntime(Vector2Int gridPos, TowerType config, out TowerRuntime instance,
            out PlacementResult result)
        {
            instance = null;
            if (!RuntimeNet.IsServer)
            {
                result = PlacementResult.ServerUnavailable;
                return false;
            }

            if (gridManager == null || EnergyNetworkManager.Instance == null || PhaseManager.Instance == null)
            {
                result = PlacementResult.MissingDependencies;
                return false;
            }

            result = PlacementValidator.ValidatePlacement(gridPos, config, gridManager, PhaseManager.Instance,
                EnergyNetworkManager.Instance.IsPositionInRange);
            if (!PlacementValidator.IsPlacementAllowed(result, allowOutOfEnergyRange: true))
                return false;

            var prefab = config.Prefab.GetComponent<TowerRuntime>();
            if (prefab == null)
            {
                result = PlacementResult.InvalidPrefab;
                return false;
            }

            if (!gridManager.TryRegisterOccupancy(GridObjectKind.Tower, gridPos, config.Size,
                    config.CanBePlacedOnWater, config.Id, out var record))
            {
                result = PlacementResult.CellBlocked;
                return false;
            }

            instance = Object.Instantiate(prefab, GridManager.TowerWorldPos(gridPos, config), Quaternion.identity);
            instance.Initialize(config, gridPos);
            gridManager.BindRuntimeObject(record.Id, instance.gameObject);

            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                netObj.Spawn();
                gridManager.BindRuntimeNetId(record.Id, netObj.NetworkObjectId);
            }

            EnergyNetworkManager.Instance?.TryConnectTowerToEnergy(instance);

            result = PlacementResult.Success;
            return true;
        }

        public bool HasRequiredReferences(out string issue)
        {
            return ReferenceValidator.Validate(out issue,
                (gridManager, nameof(gridManager)));
        }
    }
}
