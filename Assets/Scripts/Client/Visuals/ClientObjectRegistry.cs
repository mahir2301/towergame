using System.Collections.Generic;
using Shared;
using Shared.Runtime;
using Shared.Utilities;
using UnityEngine;

namespace Client.Visuals
{
    public class ClientObjectRegistry : MonoBehaviour
    {
        public static ClientObjectRegistry Instance { get; private set; }

        private readonly Dictionary<ulong, EnergyRuntime> energyNodes = new();
        private readonly Dictionary<ulong, TowerRuntime> towers = new();
        private readonly SubscriptionGroup subscriptions = new();

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
        }

        private void OnEnable()
        {
            subscriptions.Add(() => GameEvents.EnergySpawned += OnEnergySpawned,
                () => GameEvents.EnergySpawned -= OnEnergySpawned);
            subscriptions.Add(() => GameEvents.EnergyDespawned += OnEnergyDespawned,
                () => GameEvents.EnergyDespawned -= OnEnergyDespawned);
            subscriptions.Add(() => GameEvents.TowerSpawned += OnTowerSpawned,
                () => GameEvents.TowerSpawned -= OnTowerSpawned);
            subscriptions.Add(() => GameEvents.TowerDespawned += OnTowerDespawned,
                () => GameEvents.TowerDespawned -= OnTowerDespawned);
        }

        private void OnDisable()
        {
            subscriptions.UnbindAll();
            energyNodes.Clear();
            towers.Clear();
        }

        private void OnDestroy()
        {
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
