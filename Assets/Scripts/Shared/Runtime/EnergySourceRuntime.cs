using Shared.Data;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Runtime
{
    public class EnergySourceRuntime : Placeables.PlaceableBehavior
    {
        [Header("Configuration")]
        [SerializeField] private int energyRange = 20;
        [SerializeField] private int maxCapacity;

        private readonly NetworkVariable<int> currentCapacity = new();

        public int MaxCapacity => maxCapacity;
        public int CurrentCapacity => currentCapacity.Value;
        public int EnergyRange => energyRange;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                currentCapacity.Value = maxCapacity;
                ServerEvents.RaisePlaceableSpawned(this);
            }

            GameEvents.RaisePlaceableSpawned(this);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                ServerEvents.RaisePlaceableDespawned(this);

            GameEvents.RaisePlaceableDespawned(this);

            base.OnNetworkDespawn();
        }

        public void SetMaxCapacity(int capacity)
        {
            if (!RuntimeNet.IsServer)
                return;

            maxCapacity = Mathf.Max(0, capacity);
        }

        public bool TryConnectTower(ulong towerNetId, int energyCost)
        {
            if (!IsServer || !HasCapacity(energyCost))
                return false;

            currentCapacity.Value -= energyCost;
            return true;
        }

        public void DisconnectTower(ulong towerNetId, int energyCost)
        {
            if (!RuntimeNet.IsServer)
                return;

            currentCapacity.Value = Mathf.Min(currentCapacity.Value + energyCost, maxCapacity);
        }

        public bool HasCapacity(int amount)
        {
            return currentCapacity.Value >= amount;
        }

        public bool CanConnectClass(ClassType classType)
        {
            return classType != null && Type != null && classType.CanConnectTo(Type);
        }
    }
}
