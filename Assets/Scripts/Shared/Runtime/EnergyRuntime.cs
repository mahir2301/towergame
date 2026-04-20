using Shared.Data;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Runtime
{
    public class EnergyRuntime : NetworkBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private EnergyType config;
        [SerializeField] private int maxCapacity;

        private readonly NetworkVariable<int> currentCapacity = new();

        private Vector2Int gridPosition;

        public EnergyType Config => config;
        public Vector2Int GridPosition { get => gridPosition; set => gridPosition = value; }
        public int MaxCapacity => maxCapacity;
        public int CurrentCapacity => currentCapacity.Value;
        public int EnergyRange => config != null ? config.EnergyRange : 0;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                currentCapacity.Value = maxCapacity;
                ServerEvents.RaiseEnergySpawned(this);
            }

            GameEvents.RaiseEnergySpawned(this);
        }

        public override void OnNetworkDespawn()
        {
            GameEvents.RaiseEnergyDespawned(this);

            if (IsServer)
                ServerEvents.RaiseEnergyDespawned(this);

            base.OnNetworkDespawn();
        }

        public void Initialize(EnergyType energyConfig, int capacity, Vector2Int gridPos)
        {
            config = energyConfig;
            maxCapacity = capacity;
            gridPosition = gridPos;
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
            return classType != null && config != null && classType.CanConnectTo(config);
        }
    }
}
