using Data;
using Managers;
using Unity.Netcode;
using UnityEngine;
using UI;
using Visuals;

namespace Runtime
{
    public class EnergyRuntime : NetworkBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private EnergyType config;
        [SerializeField] private Vector2Int gridPosition;
        [SerializeField] private int maxCapacity;

        private readonly NetworkVariable<int> currentCapacity = new();
        private RangeIndicator rangeIndicator;

        public EnergyType Config => config;
        public Vector2Int GridPosition { get => gridPosition; set => gridPosition = value; }
        public int MaxCapacity => maxCapacity;
        public int CurrentCapacity => currentCapacity.Value;
        public int EnergyRange => config != null ? config.EnergyRange : 0;

        private void Start()
        {
            CreateRangeIndicator();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                currentCapacity.Value = maxCapacity;
                EnergyNetworkManager.Instance?.RegisterEnergyRuntime(this);
            }

            WorldOverlayManager.Instance?.RegisterEnergy(this);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (IsServer)
                EnergyNetworkManager.Instance?.UnregisterEnergyRuntime(this);

            WorldOverlayManager.Instance?.UnregisterEnergy(this);
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
            if (!IsServer) return;
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
            if (EnergyRange <= 0) return;

            var indicatorGo = new GameObject("EnergyRangeIndicator");
            indicatorGo.transform.SetParent(transform);
            indicatorGo.transform.localPosition = Vector3.zero;

            rangeIndicator = indicatorGo.AddComponent<RangeIndicator>();
            rangeIndicator.ShowEnergy(EnergyRange);
        }
    }
}
