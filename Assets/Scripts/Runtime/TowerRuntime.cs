using Data;
using Managers;
using Unity.Netcode;
using UnityEngine;
using UI;
using Visuals;

namespace Runtime
{
    public class TowerRuntime : NetworkBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TowerType config;

        [Header("Runtime State")]
        [SerializeField] private Vector2Int gridPosition;

        private NetworkVariable<float> currentHealth = new();
        private NetworkVariable<bool> isPowered = new();
        private ulong connectedEnergyId = ulong.MaxValue;
        private ulong connectedViaAntennaId = ulong.MaxValue;
        private RangeIndicator rangeIndicator;

        public TowerType Config => config;
        public Vector2Int GridPosition { get => gridPosition; set => gridPosition = value; }
        public Vector2Int Size => config != null ? config.Size : Vector2Int.one;
        public bool CanBePlacedOnWater => config != null && config.CanBePlacedOnWater;
        public float CurrentHealth => currentHealth.Value;
        public bool IsPowered => isPowered.Value;
        public ulong ConnectedEnergyId => connectedEnergyId;
        public ulong ConnectedViaAntennaId => connectedViaAntennaId;

        private void Start()
        {
            CreateAntennaRangeIndicator();
            isPowered.OnValueChanged += OnPoweredChanged;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                if (config != null)
                    currentHealth.Value = config.Stats.maxHealth;
                EnergyNetworkManager.Instance?.TryConnectTowerToEnergy(this);
            }

            if (!IsServer && GridManager.Instance != null)
                GridManager.Instance.RegisterOccupiedCells(gridPosition, Size, gameObject);
            WorldOverlayManager.Instance?.RegisterTower(this);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            isPowered.OnValueChanged -= OnPoweredChanged;
            WorldOverlayManager.Instance?.UnregisterTower(this);
        }

        public void Initialize(TowerType towerConfig, Vector2Int gridPos)
        {
            config = towerConfig;
            gridPosition = gridPos;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void TakeDamageServerRpc(float amount)
        {
            currentHealth.Value = Mathf.Max(0, currentHealth.Value - amount);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RepairServerRpc(float amount)
        {
            if (config != null)
                currentHealth.Value = Mathf.Min(config.Stats.maxHealth, currentHealth.Value + amount);
        }

        public void SetPowered(bool powered)
        {
            if (IsServer)
                isPowered.Value = powered;
        }

        public void SetConnection(ulong energyId, ulong antennaId)
        {
            connectedEnergyId = energyId;
            connectedViaAntennaId = antennaId;
            SetPowered(energyId != ulong.MaxValue);
        }

        public void ClearConnection()
        {
            connectedEnergyId = ulong.MaxValue;
            connectedViaAntennaId = ulong.MaxValue;
            SetPowered(false);
        }

        private void CreateAntennaRangeIndicator()
        {
            if (config == null || !config.IsAntenna) return;

            var indicatorGo = new GameObject("AntennaRangeIndicator");
            indicatorGo.transform.SetParent(transform);
            indicatorGo.transform.localPosition = Vector3.zero;

            rangeIndicator = indicatorGo.AddComponent<RangeIndicator>();
            rangeIndicator.ShowAntenna(config.Stats.antennaRange);
        }

        private void OnPoweredChanged(bool wasPowered, bool isNowPowered)
        {
            if (rangeIndicator == null || config == null || !config.IsAntenna) return;
            rangeIndicator.Show(config.Stats.antennaRange, isNowPowered);
        }
    }
}
