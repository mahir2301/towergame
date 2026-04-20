using Shared.Entities;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace Server.Managers
{
    public class ServerEntityBootstrap : MonoBehaviour
    {
        [SerializeField] private Vector3 initialPlayerSpawn = new(0f, 1f, 0f);
        [SerializeField] private float spawnSpacing = 2f;

        private NetworkManager networkManager;
        private bool serverCallbacksBound;
        private bool subscribedToServerStarted;

        private void Start()
        {
            if (!RuntimeNet.IsServer)
            {
                enabled = false;
                return;
            }

            if (!TryBindNetworkHooks())
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthMissingDependency,
                    "ServerEntityBootstrap requires NetworkManager.Singleton at startup.");
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            UnbindServerCallbacks();

            if (networkManager != null && subscribedToServerStarted)
            {
                networkManager.OnServerStarted -= HandleServerStarted;
                subscribedToServerStarted = false;
            }
        }

        private bool TryBindNetworkHooks()
        {
            networkManager = NetworkManager.Singleton;
            if (networkManager == null)
                return false;

            if (!subscribedToServerStarted)
            {
                networkManager.OnServerStarted += HandleServerStarted;
                subscribedToServerStarted = true;
            }

            if (!networkManager.IsServer || !networkManager.IsListening)
                return true;

            BindServerCallbacks();
            SpawnPlayersForConnectedClients();
            return true;
        }

        private void HandleServerStarted()
        {
            BindServerCallbacks();
            SpawnPlayersForConnectedClients();
        }

        private void BindServerCallbacks()
        {
            if (networkManager == null || serverCallbacksBound)
                return;

            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            serverCallbacksBound = true;
        }

        private void UnbindServerCallbacks()
        {
            if (networkManager == null || !serverCallbacksBound)
                return;

            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            serverCallbacksBound = false;
        }

        private void SpawnPlayersForConnectedClients()
        {
            if (networkManager == null)
                return;

            var clients = networkManager.ConnectedClientsIds;
            for (var i = 0; i < clients.Count; i++)
            {
                HandleClientConnected(clients[i]);
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            var spawnPos = initialPlayerSpawn + Vector3.right * (spawnSpacing * clientId);
            if (EntityManager.TrySpawnPlayerForClient(clientId, spawnPos, out _))
                return;

            RuntimeLog.Entity.Error(RuntimeLog.Code.EntitySpawnFailed,
                $"Failed to spawn player entity for client {clientId}.");
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            EntityManager.TryDespawnPlayerForClient(clientId);
        }
    }
}
