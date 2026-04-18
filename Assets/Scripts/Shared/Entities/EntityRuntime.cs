using Shared.Data;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Entities
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class EntityRuntime : NetworkBehaviour
    {
        [SerializeField] private EntityKind defaultKind = EntityKind.Unknown;
        [SerializeField] private string defaultEntityTypeId;

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

            if (IsServer)
                EnsureServerMetadata();

            EntityManager.RegisterRuntime(this);
        }

        public override void OnNetworkDespawn()
        {
            EntityManager.UnregisterRuntime(this);
            base.OnNetworkDespawn();
        }

        public void ConfigureServerMetadata(string typeId, EntityKind entityKind, ulong ownerId)
        {
            if (!IsServer)
                return;

            entityTypeId.Value = typeId;
            kind.Value = entityKind;
            ownerClientId.Value = ownerId;
        }

        internal void AssignServerEntityId(EntityId id)
        {
            if (!IsServer || !id.IsValid)
                return;

            entityId.Value = id.Value;
        }

        private void EnsureServerMetadata()
        {
            if (entityTypeId.Value.Length == 0 && !string.IsNullOrWhiteSpace(defaultEntityTypeId))
                entityTypeId.Value = defaultEntityTypeId;

            var registry = GameRegistry.Instance;
            if (entityTypeId.Value.Length > 0 && registry != null)
            {
                var type = registry.GetEntityType(entityTypeId.Value.ToString());
                if (type != null)
                {
                    kind.Value = type.Kind;
                }
            }

            if (kind.Value == EntityKind.Unknown && defaultKind != EntityKind.Unknown)
                kind.Value = defaultKind;
        }
    }
}
