using System.Collections.Generic;
using Shared;
using Shared.Runtime;
using UnityEngine;

namespace Client.Visuals
{
    public class ClientObjectRegistry : MonoBehaviour
    {
        public static ClientObjectRegistry Instance { get; private set; }

        private readonly Dictionary<ulong, EnergyRuntime> energyNodes = new();
        private readonly Dictionary<ulong, TowerRuntime> towers = new();
        private bool subscribedToGameEvents;

        public IReadOnlyDictionary<ulong, EnergyRuntime> EnergyNodes => energyNodes;
        public IReadOnlyDictionary<ulong, TowerRuntime> Towers => towers;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (!subscribedToGameEvents)
            {
                GameEvents.EnergySpawned += OnEnergySpawned;
                GameEvents.EnergyDespawned += OnEnergyDespawned;
                GameEvents.TowerSpawned += OnTowerSpawned;
                GameEvents.TowerDespawned += OnTowerDespawned;
                subscribedToGameEvents = true;
            }
        }

        private void OnDestroy()
        {
            if (subscribedToGameEvents)
            {
                GameEvents.EnergySpawned -= OnEnergySpawned;
                GameEvents.EnergyDespawned -= OnEnergyDespawned;
                GameEvents.TowerSpawned -= OnTowerSpawned;
                GameEvents.TowerDespawned -= OnTowerDespawned;
                subscribedToGameEvents = false;
            }

            if (Instance == this)
                Instance = null;
        }

        private void OnEnergySpawned(EnergyRuntime energy)
        {
            if (energy == null || !energy.IsSpawned) return;
            energyNodes[energy.NetworkObjectId] = energy;
        }

        private void OnEnergyDespawned(EnergyRuntime energy)
        {
            if (energy == null) return;
            energyNodes.Remove(energy.NetworkObjectId);
        }

        private void OnTowerSpawned(TowerRuntime tower)
        {
            if (tower == null || !tower.IsSpawned) return;
            towers[tower.NetworkObjectId] = tower;
        }

        private void OnTowerDespawned(TowerRuntime tower)
        {
            if (tower == null) return;
            towers.Remove(tower.NetworkObjectId);
        }
    }
}
