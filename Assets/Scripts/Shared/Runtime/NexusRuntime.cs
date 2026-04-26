using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Runtime
{
    public class NexusRuntime : Placeables.PlaceableBehavior
    {
        public const int ExclusionZone = 8;
        public const int NexusSize = 3;

        [Header("Configuration")]
        [SerializeField] private float maxHealth = 1000f;
        [SerializeField] private Vector2Int size = new(NexusSize, NexusSize);

        private readonly NetworkVariable<float> currentHealth = new();

        public Vector2Int Size => size;
        public float CurrentHealth => currentHealth.Value;

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
    }
}
