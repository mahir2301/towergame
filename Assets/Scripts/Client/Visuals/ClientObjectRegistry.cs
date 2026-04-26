using System.Collections.Generic;
using Shared;
using Shared.Runtime;
using Shared.Runtime.Placeables;
using Shared.Utilities;
using UnityEngine;

namespace Client.Visuals
{
    public class ClientObjectRegistry : MonoBehaviour
    {
        public static ClientObjectRegistry Instance { get; private set; }

        private readonly Dictionary<ulong, EnergyRuntime> energyNodes = new();
        private readonly Dictionary<ulong, TowerRuntime> towers = new();
        private readonly SubscriptionGroup subscriptions = new();

        public IReadOnlyDictionary<ulong, EnergyRuntime> EnergyNodes => energyNodes;
        public IReadOnlyDictionary<ulong, TowerRuntime> Towers => towers;

        private void Awake()
        {
            if (!RuntimeNet.ShouldRunNetworkedClientSystems())
            {
                enabled = false;
                return;
            }

            if (!SingletonUtility.TryAssign(Instance, this, value => Instance = value))
                return;
        }

        private void OnEnable()
        {
            subscriptions.Add(() => GameEvents.PlaceableSpawned += OnPlaceableSpawned,
                () => GameEvents.PlaceableSpawned -= OnPlaceableSpawned);
            subscriptions.Add(() => GameEvents.PlaceableDespawned += OnPlaceableDespawned,
                () => GameEvents.PlaceableDespawned -= OnPlaceableDespawned);
        }

        private void OnDisable()
        {
            subscriptions.UnbindAll();
            energyNodes.Clear();
            towers.Clear();
        }

        private void OnDestroy()
        {
            SingletonUtility.ClearIfCurrent(Instance, this, () => Instance = null);
        }

        private void OnPlaceableSpawned(PlaceableBehavior placeable)
        {
            if (placeable is EnergyRuntime energy)
            {
                if (energy.IsSpawned)
                    energyNodes[energy.NetworkObjectId] = energy;
                return;
            }

            if (placeable is TowerRuntime tower && tower.IsSpawned)
                towers[tower.NetworkObjectId] = tower;
        }

        private void OnPlaceableDespawned(PlaceableBehavior placeable)
        {
            if (placeable is EnergyRuntime energy)
            {
                energyNodes.Remove(energy.NetworkObjectId);
                return;
            }

            if (placeable is TowerRuntime tower)
                towers.Remove(tower.NetworkObjectId);
        }
    }
}
