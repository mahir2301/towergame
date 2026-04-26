using Shared.Data;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Runtime
{
    public class TowerRuntime : Placeables.PlaceableBehavior
    {
        [Header("Configuration")]
        [SerializeField] private ClassType classType;
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private int energyCost;

        private readonly NetworkVariable<float> currentHealth = new();
        private readonly NetworkVariable<bool> isPowered = new();
        private readonly NetworkVariable<ulong> connectedEnergyId = new(ulong.MaxValue);
        private readonly NetworkVariable<ulong> connectedViaAntennaId = new(ulong.MaxValue);

        public ClassType ClassType => classType;
        public int EnergyCost => energyCost;
        public Vector2Int Size => Type != null ? Type.Size : Vector2Int.one;
        public float CurrentHealth => currentHealth.Value;
        public bool IsPowered => isPowered.Value;
        public ulong ConnectedEnergyId => connectedEnergyId.Value;
        public ulong ConnectedViaAntennaId => connectedViaAntennaId.Value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                currentHealth.Value = Mathf.Max(0f, maxHealth);
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

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void TakeDamageServerRpc(float amount)
        {
            if (!RuntimeNet.IsServer)
                return;

            currentHealth.Value = Mathf.Max(0, currentHealth.Value - amount);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RepairServerRpc(float amount)
        {
            if (!IsServer)
                return;

            currentHealth.Value = Mathf.Min(maxHealth, currentHealth.Value + amount);
        }

        public void SetPowered(bool powered)
        {
            if (IsServer)
                isPowered.Value = powered;
        }

        public void SetConnection(ulong energyId, ulong antennaId)
        {
            if (!RuntimeNet.IsServer)
                return;

            connectedEnergyId.Value = energyId;
            connectedViaAntennaId.Value = antennaId;
            SetPowered(energyId != ulong.MaxValue);
        }

        public void ClearConnection()
        {
            if (!RuntimeNet.IsServer)
                return;

            connectedEnergyId.Value = ulong.MaxValue;
            connectedViaAntennaId.Value = ulong.MaxValue;
            SetPowered(false);
        }
    }
}
