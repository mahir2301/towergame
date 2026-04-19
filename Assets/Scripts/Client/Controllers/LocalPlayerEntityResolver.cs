using Shared;
using Shared.Entities;
using Shared.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Client.Controllers
{
    public class LocalPlayerEntityResolver : MonoBehaviour
    {
        public PlayerRuntime CurrentPlayer { get; private set; }

        private void OnEnable()
        {
            GameEvents.EntitySpawned += HandleEntityChanged;
            GameEvents.EntityDespawned += HandleEntityChanged;
            GameEvents.EntityOwnerAssigned += HandleEntityOwnerAssigned;
            TryResolve();
        }

        private void OnDisable()
        {
            GameEvents.EntitySpawned -= HandleEntityChanged;
            GameEvents.EntityDespawned -= HandleEntityChanged;
            GameEvents.EntityOwnerAssigned -= HandleEntityOwnerAssigned;
        }

        private void HandleEntityChanged(EntityRuntime _)
        {
            TryResolve();
        }

        private void HandleEntityOwnerAssigned(EntityRuntime _, ulong __)
        {
            TryResolve();
        }

        private void TryResolve()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                CurrentPlayer = null;
                return;
            }

            if (!EntityManager.TryGetPlayerEntityForClient(networkManager.LocalClientId, out var entityRuntime)
                || entityRuntime == null)
            {
                CurrentPlayer = null;
                return;
            }

            CurrentPlayer = entityRuntime.GetComponent<PlayerRuntime>();
        }
    }
}
