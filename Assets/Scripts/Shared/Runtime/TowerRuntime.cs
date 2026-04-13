using Shared.Data;
using System;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Runtime
{
    public class TowerRuntime : NetworkBehaviour
    {
        public static event Action<TowerRuntime> ServerSpawned;
        public static event Action<TowerRuntime> ServerDespawned;

        [Header("Configuration")]
        [SerializeField] private TowerType config;

        [Header("Runtime State")]
        [SerializeField] private Vector2Int gridPosition;

        private NetworkVariable<float> currentHealth = new();
        private NetworkVariable<bool> isPowered = new();
        private readonly NetworkVariable<ulong> connectedEnergyId = new(ulong.MaxValue);
        private readonly NetworkVariable<ulong> connectedViaAntennaId = new(ulong.MaxValue);

        public TowerType Config => config;
        public Vector2Int GridPosition { get => gridPosition; set => gridPosition = value; }
        public Vector2Int Size => config != null ? config.Size : Vector2Int.one;
        public bool CanBePlacedOnWater => config != null && config.CanBePlacedOnWater;
        public float CurrentHealth => currentHealth.Value;
        public bool IsPowered => isPowered.Value;
        public ulong ConnectedEnergyId => connectedEnergyId.Value;
        public ulong ConnectedViaAntennaId => connectedViaAntennaId.Value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                if (config != null)
                    currentHealth.Value = config.Stats.maxHealth;
                ServerSpawned?.Invoke(this);
            }

            Shared.GameEvents.RaiseTowerSpawned(this);
        }

        public override void OnDestroy()
        {
            Shared.GameEvents.RaiseTowerDespawned(this);

            if (IsServer)
                ServerDespawned?.Invoke(this);

            base.OnDestroy();

        }

        public void Initialize(TowerType towerConfig, Vector2Int gridPos)
        {
            config = towerConfig;
            gridPosition = gridPos;
        }

        [Rpc(SendTo.Server)]
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
            if (!IsServer)
                return;

            connectedEnergyId.Value = energyId;
            connectedViaAntennaId.Value = antennaId;
            SetPowered(energyId != ulong.MaxValue);
        }

        public void ClearConnection()
        {
            if (!IsServer)
                return;

            connectedEnergyId.Value = ulong.MaxValue;
            connectedViaAntennaId.Value = ulong.MaxValue;
            SetPowered(false);
        }

    }
}
