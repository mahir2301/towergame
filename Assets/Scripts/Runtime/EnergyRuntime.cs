using Data;
using Managers;
using Unity.Netcode;
using UnityEngine;

namespace Runtime
{
    public class EnergyRuntime : NetworkBehaviour, IPlaceable
    {
        [SerializeField]
        private EnergyType config;
        [SerializeField]
        private Vector2Int gridPosition;
        [SerializeField]
        private int maxCapacity;

        private readonly NetworkVariable<int> currentCapacity = new();

        public EnergyType Config => config;
        public Vector2Int GridPosition
        {
            get => gridPosition;
            set => gridPosition = value;
        }
        public Vector2Int Size => new(1, 1);
        public int MaxCapacity => maxCapacity;
        public int CurrentCapacity => currentCapacity.Value;
        public NetworkList<ulong> ConnectedTowerIds { get; private set; }
        public NetworkList<ulong> ConnectedPlayerIds { get; private set; }

        private void Awake()
        {
            ConnectedTowerIds = new NetworkList<ulong>();
            ConnectedPlayerIds = new NetworkList<ulong>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                EnergyNetworkManager.Instance?.RegisterEnergyRuntime(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (IsServer)
            {
                EnergyNetworkManager.Instance?.UnregisterEnergyRuntime(this);
            }
        }

        public void Initialize(Vector2Int gridPos)
        {
            gridPosition = gridPos;
        }

        public void Initialize(EnergyType energyConfig, int capacity, Vector2Int gridPos)
        {
            config = energyConfig;
            maxCapacity = capacity;
            gridPosition = gridPos;

            if (IsServer)
            {
                currentCapacity.Value = capacity;
            }
        }

        public bool TryConnectTower(ulong towerNetId, int energyCost)
        {
            if (!IsServer || !HasCapacity(energyCost))
            {
                return false;
            }

            currentCapacity.Value -= energyCost;
            ConnectedTowerIds.Add(towerNetId);
            return true;
        }

        public bool TryConnectPlayer(ulong playerNetId, int energyCost)
        {
            if (!IsServer || !HasCapacity(energyCost))
            {
                return false;
            }

            currentCapacity.Value -= energyCost;
            ConnectedPlayerIds.Add(playerNetId);
            return true;
        }

        public void DisconnectTower(ulong towerNetId, int energyCost)
        {
            if (!IsServer)
            {
                return;
            }

            ConnectedTowerIds.Remove(towerNetId);
            currentCapacity.Value = Mathf.Min(currentCapacity.Value + energyCost, maxCapacity);
        }

        public void DisconnectPlayer(ulong playerNetId, int energyCost)
        {
            if (!IsServer)
            {
                return;
            }

            ConnectedPlayerIds.Remove(playerNetId);
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