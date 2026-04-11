using Data;
using Managers;
using Unity.Netcode;
using UnityEngine;
using Visuals;

namespace Runtime
{
    public class TowerRuntime : NetworkBehaviour, IPlaceable
    {
        [Header("Configuration")]
        [SerializeField]
        private string configId;
        [SerializeField]
        private Vector3 placementOffset;

        [Header("State")]
        [SerializeField]
        private Vector2Int gridPosition;
        [SerializeField]
        private Vector2Int size = new(1, 1);

        private NetworkVariable<float> currentHealth = new();
        private NetworkVariable<bool> isPowered = new();

        private TowerType cachedConfig;
        private ulong connectedEnergyId = ulong.MaxValue;
        private ulong connectedViaAntennaId = ulong.MaxValue;
        private RangeIndicator rangeIndicator;

        public ulong ConnectedEnergyId => connectedEnergyId;
        public ulong ConnectedViaAntennaId => connectedViaAntennaId;

        public string ConfigId => configId;
        public Vector2Int GridPosition
        {
            get => gridPosition;
            set => gridPosition = value;
        }
        public Vector2Int Size => size;
        public Vector3 PlacementOffset => placementOffset;
        public bool CanBePlacedOnWater => cachedConfig?.CanBePlacedOnWater ?? false;
        public float CurrentHealth => currentHealth.Value;
        public bool IsPowered => isPowered.Value;

        private void Start()
        {
            CreateAntennaRangeIndicator();
            isPowered.OnValueChanged += OnPoweredChanged;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer)
            {
                var gridManagers = FindObjectsByType<Managers.GridManager>(FindObjectsSortMode.None);
                foreach (var gm in gridManagers)
                {
                    gm.RegisterOccupiedCells(gridPosition, size, gameObject);
                }
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            isPowered.OnValueChanged -= OnPoweredChanged;
        }

        public void Initialize(Vector2Int gridPos)
        {
            gridPosition = gridPos;
        }

        public void Initialize(string towerConfigId, Vector2Int gridPos, Vector2Int towerSize, Vector3 worldPosition)
        {
            configId = towerConfigId;
            gridPosition = gridPos;
            size = towerSize;
            cachedConfig = GetConfig();

            if (IsServer && cachedConfig != null)
            {
                currentHealth.Value = cachedConfig.Stats.maxHealth;
                EnergyNetworkManager.Instance?.TryConnectTowerToEnergy(this);
            }

            transform.position = worldPosition;
        }

        public TowerType GetConfig()
        {
            if (cachedConfig == null && !string.IsNullOrEmpty(configId))
            {
                cachedConfig = GameRegistry.Instance?.GetTowerType(configId);
            }

            return cachedConfig;
        }
        
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void TakeDamageServerRpc(float amount)
        {
            currentHealth.Value = Mathf.Max(0, currentHealth.Value - amount);
        }
        
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RepairServerRpc(float amount)
        {
            currentHealth.Value = Mathf.Min(cachedConfig.Stats.maxHealth, currentHealth.Value + amount);
        }

        public void SetPowered(bool powered)
        {
            if (IsServer)
            {
                isPowered.Value = powered;
            }
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
            var config = GetConfig();
            if (config == null || config.AntennaRange <= 0)
            {
                return;
            }

            var indicatorGo = new GameObject("AntennaRangeIndicator");
            indicatorGo.transform.SetParent(transform);
            indicatorGo.transform.localPosition = Vector3.zero;

            rangeIndicator = indicatorGo.AddComponent<RangeIndicator>();
            rangeIndicator.ShowAntenna(config.AntennaRange);
        }

        private void OnPoweredChanged(bool wasPowered, bool isNowPowered)
        {
            if (rangeIndicator != null)
            {
                var config = GetConfig();
                if (config != null && config.AntennaRange > 0)
                {
                    rangeIndicator.Show(config.AntennaRange, isNowPowered);
                }
            }
        }
    }
}