using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Entities
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class EntityRuntime : NetworkBehaviour
    {
        private string pendingTypeId;
        private EntityKind pendingKind = EntityKind.Unknown;
        private ulong pendingOwnerClientId = ulong.MaxValue;
        private bool hasPendingMetadata;

        private readonly NetworkVariable<uint> entityId = new(EntityId.InvalidValue,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<EntityKind> kind = new(EntityKind.Unknown,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> ownerClientId = new(ulong.MaxValue,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString64Bytes> entityTypeId = new(default(FixedString64Bytes),
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public EntityId EntityId => new(entityId.Value);
        public EntityKind Kind => kind.Value;
        public ulong EntityOwnerClientId => ownerClientId.Value;
        public string EntityTypeId => entityTypeId.Value.ToString();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer && hasPendingMetadata)
            {
                entityTypeId.Value = pendingTypeId;
                kind.Value = pendingKind;
                ownerClientId.Value = pendingOwnerClientId;
                hasPendingMetadata = false;
            }

            if (!EntityManager.TryRegisterRuntime(this))
            {
                if (IsServer)
                {
                    if (NetworkObject != null && NetworkObject.IsSpawned)
                        NetworkObject.Despawn(true);
                    else
                        Destroy(gameObject);
                }

                enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            EntityManager.UnregisterRuntime(this);
            base.OnNetworkDespawn();
        }

        public void ConfigureServerMetadata(string typeId, EntityKind entityKind, ulong ownerId)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer)
                return;

            pendingTypeId = typeId;
            pendingKind = entityKind;
            pendingOwnerClientId = ownerId;
            hasPendingMetadata = true;

            if (!IsSpawned)
                return;

            entityTypeId.Value = typeId;
            kind.Value = entityKind;
            ownerClientId.Value = ownerId;
            hasPendingMetadata = false;
        }

        internal bool HasConfiguredMetadata(out string issue)
        {
            if (!EntityId.IsValid)
            {
                issue = "EntityId is invalid.";
                return false;
            }

            if (string.IsNullOrEmpty(EntityTypeId))
            {
                issue = "EntityTypeId is empty.";
                return false;
            }

            if (Kind == EntityKind.Unknown)
            {
                issue = "Kind is Unknown.";
                return false;
            }

            issue = null;
            return true;
        }

        internal bool HasConfiguredSpawnMetadata(out string issue)
        {
            var configuredTypeId = hasPendingMetadata ? pendingTypeId : EntityTypeId;
            var configuredKind = hasPendingMetadata ? pendingKind : Kind;

            if (string.IsNullOrEmpty(configuredTypeId))
            {
                issue = "EntityTypeId is empty.";
                return false;
            }

            if (configuredKind == EntityKind.Unknown)
            {
                issue = "Kind is Unknown.";
                return false;
            }

            issue = null;
            return true;
        }

        internal void AssignServerEntityId(EntityId id)
        {
            if (!IsServer || !id.IsValid)
                return;

            entityId.Value = id.Value;
        }
    }
}
