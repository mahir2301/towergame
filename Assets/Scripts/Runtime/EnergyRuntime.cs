using Data;
using Managers;
using Unity.Netcode;
using UnityEngine;
using Visuals;

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
        private RangeIndicator rangeIndicator;

        public EnergyType Config => config;
        public Vector2Int GridPosition
        {
            get => gridPosition;
            set => gridPosition = value;
        }
        public Vector2Int Size => new(1, 1);
        public bool CanBePlacedOnWater => false;
        public int MaxCapacity => maxCapacity;
        public int CurrentCapacity => currentCapacity.Value;
        public int EnergyRange => config != null ? config.EnergyRange : 0;
        public NetworkList<ulong> ConnectedTowerIds { get; private set; }
        public NetworkList<ulong> ConnectedPlayerIds { get; private set; }

        private void Awake()
        {
            ConnectedTowerIds = new NetworkList<ulong>();
            ConnectedPlayerIds = new NetworkList<ulong>();
        }

        private void Start()
        {
            CreateRangeIndicator();
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

        private void CreateRangeIndicator()
        {
            if (EnergyRange <= 0)
            {
                return;
            }

            var indicatorGo = new GameObject("EnergyRangeIndicator");
            indicatorGo.transform.SetParent(transform);
            indicatorGo.transform.localPosition = Vector3.zero;

            rangeIndicator = indicatorGo.AddComponent<RangeIndicator>();
            rangeIndicator.ShowEnergy(EnergyRange);
        }
    }
}