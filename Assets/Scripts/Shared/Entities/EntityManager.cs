using System.Collections.Generic;
using Shared;
using Shared.Data;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Entities
{
    public static class EntityManager
    {
        private static readonly Dictionary<uint, EntityRuntime> entitiesById = new();
        private static readonly Dictionary<ulong, uint> playerEntityByClient = new();

        private static uint nextEntityId = 1;

        public static IReadOnlyDictionary<uint, EntityRuntime> ActiveEntities => entitiesById;
        public static IReadOnlyDictionary<ulong, uint> PlayerEntitiesByClient => playerEntityByClient;

        public static bool TryGetEntity(EntityId entityId, out EntityRuntime runtime)
        {
            return entitiesById.TryGetValue(entityId.Value, out runtime);
        }

        public static bool TryGetPlayerEntityId(ulong clientId, out EntityId entityId)
        {
            if (playerEntityByClient.TryGetValue(clientId, out var rawId))
            {
                entityId = new EntityId(rawId);
                return true;
            }

            entityId = EntityId.Invalid;
            return false;
        }

        public static bool TryGetPlayerEntityForClient(ulong clientId, out EntityRuntime runtime)
        {
            runtime = null;
            if (!TryGetPlayerEntityId(clientId, out var entityId))
                return false;

            return TryGetEntity(entityId, out runtime);
        }

        public static bool TrySpawnPlayerForClient(ulong clientId, Vector3 position, out EntityRuntime runtime)
        {
            runtime = null;

            if (TryGetPlayerEntityForClient(clientId, out runtime))
            {
                RuntimeLog.Entity.Error(RuntimeLog.Code.EntitySpawnFailed,
                    $"Cannot spawn player entity for client {clientId}: an entity is already registered (id={runtime.EntityId}).");
                return false;
            }

            var playerTypeId = GameRegistry.Instance?.RequiredPlayerEntityTypeId;
            if (string.IsNullOrWhiteSpace(playerTypeId))
                return false;

            return TrySpawnByTypeId(playerTypeId, position, Quaternion.identity, clientId, out runtime);
        }

        public static bool TryDespawnPlayerForClient(ulong clientId)
        {
            if (!TryGetPlayerEntityForClient(clientId, out var runtime) || runtime == null)
                return false;

            var netObj = runtime.GetComponent<NetworkObject>();
            if (netObj == null)
                return false;

            if (netObj.IsSpawned)
                netObj.Despawn(true);
            else
                Object.Destroy(runtime.gameObject);

            RuntimeLog.Entity.Info(RuntimeLog.Code.EntityDisconnectCleanup,
                $"Completed player entity cleanup for client {clientId}.");
            return true;
        }

        public static void GetEntitiesWithTag(TagType tag, List<EntityRuntime> results)
        {
            if (results == null || tag == null)
                return;

            results.Clear();
            foreach (var kvp in entitiesById)
            {
                var runtime = kvp.Value;
                if (runtime == null)
                    continue;

                var type = GameRegistry.Instance?.GetEntityType(runtime.EntityTypeId);
                if (type != null && type.HasTag(tag))
                    results.Add(runtime);
            }
        }

        public static bool TryGetEntityType(EntityRuntime runtime, out EntityType type)
        {
            type = null;
            if (runtime == null || string.IsNullOrEmpty(runtime.EntityTypeId))
                return false;

            var registry = GameRegistry.Instance;
            if (registry == null)
                return false;

            type = registry.GetEntityType(runtime.EntityTypeId);
            return type != null;
        }

        public static bool TrySpawnByTypeId(string typeId, Vector3 position, Quaternion rotation, ulong ownerClientId,
            out EntityRuntime runtime)
        {
            runtime = null;

            var registry = GameRegistry.Instance;
            if (registry == null)
            {
                RuntimeLog.Entity.Error(RuntimeLog.Code.EntitySpawnFailed,
                    $"Cannot spawn entity '{typeId}' because GameRegistry is unavailable.");
                return false;
            }

            var type = registry.GetEntityType(typeId);
            if (type == null)
            {
                RuntimeLog.Entity.Error(RuntimeLog.Code.EntityMissingTypeDefinition,
                    $"Cannot spawn entity. EntityType '{typeId}' was not found in GameRegistry.");
                return false;
            }

            return TrySpawn(type, position, rotation, ownerClientId, out runtime);
        }

        internal static bool TryRegisterRuntime(EntityRuntime runtime)
        {
            if (runtime == null)
                return false;

            if (runtime.IsServer && !runtime.EntityId.IsValid)
                runtime.AssignServerEntityId(AllocateEntityId());

            if (!TryValidateRuntimeForRegistration(runtime, out var type, out var issue, out var code))
            {
                RuntimeLog.Entity.Error(code, issue);
                return false;
            }

            if (entitiesById.TryGetValue(runtime.EntityId.Value, out var existing) && existing != runtime)
            {
                RuntimeLog.Entity.Error(RuntimeLog.Code.EntityInvalidCommandPayload,
                    $"Rejected registration for entity id={runtime.EntityId}: duplicate id already registered by '{existing.name}'.");
                return false;
            }

            entitiesById[runtime.EntityId.Value] = runtime;

            if (runtime.EntityOwnerClientId != ulong.MaxValue)
                GameEvents.RaiseEntityOwnerAssigned(runtime, runtime.EntityOwnerClientId);

            var playerTypeId = GameRegistry.Instance?.RequiredPlayerEntityTypeId;
            if (!string.IsNullOrWhiteSpace(playerTypeId)
                && runtime.EntityTypeId == playerTypeId
                && runtime.EntityOwnerClientId != ulong.MaxValue)
            {
                playerEntityByClient[runtime.EntityOwnerClientId] = runtime.EntityId.Value;
                RuntimeLog.Entity.Info(RuntimeLog.Code.EntityPlayerAssigned,
                    $"Assigned player entity {runtime.EntityId} to client {runtime.EntityOwnerClientId}.");
            }

            RuntimeLog.Entity.Info(RuntimeLog.Code.EntitySpawned,
                $"Registered entity id={runtime.EntityId} type={runtime.EntityTypeId} owner={runtime.EntityOwnerClientId}.");
            GameEvents.RaiseEntitySpawned(runtime);
            return true;
        }

        internal static void UnregisterRuntime(EntityRuntime runtime)
        {
            if (runtime == null || !runtime.EntityId.IsValid)
                return;

            if (entitiesById.TryGetValue(runtime.EntityId.Value, out var existing) && existing == runtime)
                entitiesById.Remove(runtime.EntityId.Value);

            var playerTypeId = GameRegistry.Instance?.RequiredPlayerEntityTypeId;
            if (!string.IsNullOrWhiteSpace(playerTypeId)
                && runtime.EntityTypeId == playerTypeId
                && runtime.EntityOwnerClientId != ulong.MaxValue
                && playerEntityByClient.TryGetValue(runtime.EntityOwnerClientId, out var mappedId)
                && mappedId == runtime.EntityId.Value)
            {
                playerEntityByClient.Remove(runtime.EntityOwnerClientId);
            }

            RuntimeLog.Entity.Info(RuntimeLog.Code.EntityDespawned,
                $"Unregistered entity id={runtime.EntityId} type={runtime.EntityTypeId} owner={runtime.EntityOwnerClientId}.");
            GameEvents.RaiseEntityDespawned(runtime);
        }

        private static EntityId AllocateEntityId()
        {
            if (nextEntityId == EntityId.InvalidValue)
                nextEntityId = 1;

            var id = nextEntityId;
            nextEntityId++;
            return new EntityId(id);
        }

        private static bool TrySpawn(EntityType type, Vector3 position, Quaternion rotation, ulong ownerClientId,
            out EntityRuntime runtime)
        {
            runtime = null;
            if (type == null)
                return false;

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || !NetworkManager.Singleton.IsListening)
            {
                RuntimeLog.Entity.Error(RuntimeLog.Code.EntitySpawnFailed,
                    $"Cannot spawn entity '{type.Id}' because server networking is not active.");
                return false;
            }

            if (type.Prefab == null)
            {
                RuntimeLog.Entity.Error(RuntimeLog.Code.EntitySpawnFailed,
                    $"Cannot spawn entity '{type.Id}' because its prefab is missing.");
                return false;
            }

            runtime = Object.Instantiate(type.Prefab, position, rotation);
            runtime.ConfigureServerMetadata(type.Id, ownerClientId);

            if (!runtime.HasConfiguredSpawnMetadata(out var metadataIssue))
            {
                RuntimeLog.Entity.Error(RuntimeLog.Code.EntitySpawnFailed,
                    $"Cannot spawn entity '{type.Id}' because runtime metadata is invalid: {metadataIssue}");
                Object.Destroy(runtime.gameObject);
                runtime = null;
                return false;
            }

            var networkObject = runtime.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                RuntimeLog.Entity.Error(RuntimeLog.Code.EntitySpawnFailed,
                    $"Cannot spawn entity '{type.Id}' because prefab lacks NetworkObject.");
                Object.Destroy(runtime.gameObject);
                runtime = null;
                return false;
            }

            if (ownerClientId == ulong.MaxValue)
                networkObject.Spawn();
            else
                networkObject.SpawnWithOwnership(ownerClientId);

            return true;
        }

        private static bool TryValidateRuntimeForRegistration(EntityRuntime runtime, out EntityType type,
            out string issue, out string code)
        {
            type = null;
            issue = null;
            code = null;

            if (!runtime.HasConfiguredMetadata(out var metadataIssue))
            {
                issue = $"Rejected registration for '{runtime.name}': {metadataIssue}";
                code = metadataIssue.Contains("EntityTypeId")
                    ? RuntimeLog.Code.EntityMissingTypeId
                    : RuntimeLog.Code.EntityMissingForCommand;
                return false;
            }

            if (!TryGetEntityType(runtime, out type))
            {
                issue = $"Rejected registration for entity id={runtime.EntityId}: missing EntityType '{runtime.EntityTypeId}'.";
                code = RuntimeLog.Code.EntityMissingTypeDefinition;
                return false;
            }

            return true;
        }
    }
}
