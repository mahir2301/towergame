using Shared.Data;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Runtime
{
    public class NexusRuntime : NetworkBehaviour
    {
        public const int ExclusionZone = 8;
        public const int NexusSize = 3;

        [Header("Configuration")]
        [SerializeField] private NexusType config;
        [SerializeField] private Vector2Int size = new(NexusSize, NexusSize);

        private readonly NetworkVariable<float> currentHealth = new();

        private Vector2Int gridPosition;

        public NexusType Config => config;
        public Vector2Int GridPosition { get => gridPosition; set => gridPosition = value; }
        public Vector2Int Size => size;
        public float CurrentHealth => currentHealth.Value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                if (config != null)
                    currentHealth.Value = config.MaxHealth;
                ServerEvents.RaiseNexusSpawned(this);
            }

            GameEvents.RaiseNexusSpawned(this);
        }

        public override void OnNetworkDespawn()
        {
            GameEvents.RaiseNexusDespawned(this);

            if (IsServer)
                ServerEvents.RaiseNexusDespawned(this);

            base.OnNetworkDespawn();
        }

        public void Initialize(NexusType nexusConfig, Vector2Int gridPos)
        {
            config = nexusConfig;
            gridPosition = gridPos;
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
            if (!IsServer || config == null) return;
            currentHealth.Value = Mathf.Min(config.MaxHealth, currentHealth.Value + amount);
        }
    }
}
