using System.Collections.Generic;
using Shared;
using Shared.Data;
using Shared.Grid;
using Shared.Runtime;
using Shared.Runtime.Placeables;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace Server.Managers
{
    public class EnergyNetworkManager : NetworkBehaviour
    {
        public static EnergyNetworkManager Instance { get; private set; }

        private readonly List<EnergyRuntime> energyNodes = new();
        private readonly Dictionary<ulong, TowerRuntime> activeAntennas = new();
        private readonly Dictionary<ulong, ulong> towerToEnergy = new();
        private readonly SubscriptionGroup subscriptions = new();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!RuntimeNet.IsServer)
                return;

            BootstrapFromSpawnedRuntimes();
        }

        private void Awake()
        {
            if (!RuntimeNet.IsServer)
            {
                enabled = false;
                return;
            }

            if (!SingletonUtility.TryAssign(Instance, this, value => Instance = value))
                return;

            subscriptions.Add(() => ServerEvents.PlaceableSpawned += OnPlaceableSpawned,
                () => ServerEvents.PlaceableSpawned -= OnPlaceableSpawned);
            subscriptions.Add(() => ServerEvents.PlaceableDespawned += OnPlaceableDespawned,
                () => ServerEvents.PlaceableDespawned -= OnPlaceableDespawned);

            var networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                subscriptions.Add(() => networkManager.OnServerStarted += HandleServerStarted,
                    () => networkManager.OnServerStarted -= HandleServerStarted);

                if (networkManager.IsServer && networkManager.IsListening)
                    BootstrapFromSpawnedRuntimes();
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            subscriptions.UnbindAll();

            SingletonUtility.ClearIfCurrent(Instance, this, () => Instance = null);
        }

        private void RegisterEnergyRuntime(EnergyRuntime runtime)
        {
            if (runtime == null)
                return;

            if (energyNodes.Contains(runtime))
                return;

            energyNodes.Add(runtime);
        }

        private void UnregisterEnergyRuntime(EnergyRuntime runtime)
        {
            if (runtime == null)
                return;

            energyNodes.Remove(runtime);

            var toDisconnect = new List<ulong>();
            foreach (var kvp in towerToEnergy)
                if (kvp.Value == runtime.NetworkObjectId)
                    toDisconnect.Add(kvp.Key);

            foreach (var towerId in toDisconnect)
                if (TryGetTower(towerId, out var tower))
                    DisconnectTower(tower);
        }

        private void OnTowerSpawned(TowerRuntime tower)
        {
            if (tower == null)
                return;

            TryConnectTowerToEnergy(tower);
        }

        private void OnTowerDespawned(TowerRuntime tower)
        {
            if (tower == null)
                return;

            DisconnectTower(tower);
        }

        private void OnPlaceableSpawned(PlaceableBehavior placeable)
        {
            if (placeable is EnergyRuntime energy)
            {
                RegisterEnergyRuntime(energy);
                return;
            }

            if (placeable is TowerRuntime tower)
                OnTowerSpawned(tower);
        }

        private void OnPlaceableDespawned(PlaceableBehavior placeable)
        {
            if (placeable is EnergyRuntime energy)
            {
                UnregisterEnergyRuntime(energy);
                return;
            }

            if (placeable is TowerRuntime tower)
                OnTowerDespawned(tower);
        }

        public bool TryConnectTowerToEnergy(TowerRuntime tower)
        {
            if (!RuntimeNet.IsServer || tower == null)
                return false;

            if (energyNodes.Count == 0)
                BootstrapFromSpawnedRuntimes();

            var towerId = tower.NetworkObjectId;
            if (towerToEnergy.ContainsKey(towerId))
                return true;

            var classType = tower.ClassType;
            if (classType == null)
                return false;

            var energyCost = tower.EnergyCost;
            var candidate = FindBestCandidate(tower.GridPosition, classType, energyCost);
            if (candidate.energy == null)
                return false;

            if (!candidate.energy.TryConnectTower(tower.NetworkObjectId, energyCost))
                return false;

            tower.SetConnection(candidate.energy.NetworkObjectId, candidate.viaAntennaId);
            towerToEnergy[towerId] = candidate.energy.NetworkObjectId;

            if (tower.IsAntenna)
                activeAntennas[towerId] = tower;
            return true;
        }

        private void BootstrapFromSpawnedRuntimes()
        {
            if (!RuntimeNet.IsServer)
                return;

            energyNodes.Clear();
            activeAntennas.Clear();
            towerToEnergy.Clear();

            var spawnedEnergies = FindObjectsByType<EnergyRuntime>(FindObjectsSortMode.None);
            for (var i = 0; i < spawnedEnergies.Length; i++)
            {
                var energy = spawnedEnergies[i];
                if (energy == null || !energy.IsSpawned)
                    continue;

                RegisterEnergyRuntime(energy);
            }

            var spawnedTowers = FindObjectsByType<TowerRuntime>(FindObjectsSortMode.None);
            for (var i = 0; i < spawnedTowers.Length; i++)
            {
                var tower = spawnedTowers[i];
                if (tower == null || !tower.IsSpawned)
                    continue;

                TryConnectTowerToEnergy(tower);
            }
        }

        public void DisconnectTower(TowerRuntime tower)
        {
            if (!RuntimeNet.IsServer) return;

            var towerId = tower.NetworkObjectId;
            var classType = tower.ClassType;
            if (classType == null)
                return;

            var energyCost = tower.EnergyCost;

            // Refund capacity to energy node
            if (towerToEnergy.TryGetValue(towerId, out var energyId))
            {
                var energy = FindEnergyNode(energyId);
                energy?.DisconnectTower(towerId, energyCost);
                towerToEnergy.Remove(towerId);
            }

            // If this tower is an antenna, cascade disconnect all dependents
            if (tower.IsAntenna)
            {
                DisconnectAntennaDependents(towerId);
                activeAntennas.Remove(towerId);
            }

            tower.ClearConnection();
        }

        private void DisconnectAntennaDependents(ulong antennaId)
        {
            var dependents = new List<ulong>();
            foreach (var kvp in towerToEnergy)
                if (kvp.Value == antennaId)
                    dependents.Add(kvp.Key);

            foreach (var depId in dependents)
            {
                if (!TryGetTower(depId, out var dep)) continue;

                // Refund capacity to the antenna's energy source
                if (towerToEnergy.TryGetValue(antennaId, out var energyId))
                {
                    var energy = FindEnergyNode(energyId);
                    energy?.DisconnectTower(depId, dep.EnergyCost);
                }

                towerToEnergy.Remove(depId);
                dep.ClearConnection();
            }
        }

        public bool IsPositionInRange(Vector2Int pos, ClassType classType, int energyCost)
        {
            if (!RuntimeNet.IsServer)
                return false;

            foreach (var energy in energyNodes)
            {
                if (!energy.CanConnectClass(classType) || !energy.HasCapacity(energyCost)) continue;
                if (Vector2Int.Distance(pos, energy.GridPosition) <= energy.EnergyRange)
                    return true;
            }

            foreach (var kvp in activeAntennas)
            {
                if (!kvp.Value.IsAntenna)
                    continue;

                if (Vector2Int.Distance(pos, kvp.Value.GridPosition) > kvp.Value.AntennaRange)
                    continue;

                if (!towerToEnergy.TryGetValue(kvp.Key, out var energyId)) continue;
                var energy = FindEnergyNode(energyId);
                if (energy != null && energy.CanConnectClass(classType) && energy.HasCapacity(energyCost))
                    return true;
            }

            return false;
        }

        public bool IsPositionInRange(Vector2Int pos, PlaceableType placeableType)
        {
            if (placeableType?.Prefab == null)
                return false;

            var towerRuntime = placeableType.Prefab.GetComponent<TowerRuntime>();
            if (towerRuntime == null || towerRuntime.ClassType == null)
                return true;

            return IsPositionInRange(pos, towerRuntime.ClassType, towerRuntime.EnergyCost);
        }

        public float GetEnergyRangeForPosition(Vector2Int pos, ClassType classType, int energyCost)
        {
            if (!RuntimeNet.IsServer)
                return -1f;

            var best = -1f;

            foreach (var energy in energyNodes)
            {
                if (!energy.CanConnectClass(classType) || !energy.HasCapacity(energyCost)) continue;
                var dist = Vector2Int.Distance(pos, energy.GridPosition);
                if (dist <= energy.EnergyRange)
                    best = Mathf.Max(best, energy.EnergyRange - dist);
            }

            foreach (var kvp in activeAntennas)
            {
                if (!kvp.Value.IsAntenna)
                    continue;

                var range = kvp.Value.AntennaRange;
                if (Vector2Int.Distance(pos, kvp.Value.GridPosition) > range) continue;

                if (!towerToEnergy.TryGetValue(kvp.Key, out var energyId)) continue;
                var energy = FindEnergyNode(energyId);
                if (energy != null && energy.CanConnectClass(classType) && energy.HasCapacity(energyCost))
                    best = Mathf.Max(best, range - Vector2Int.Distance(pos, kvp.Value.GridPosition));
            }

            return best;
        }

        private (EnergyRuntime energy, ulong viaAntennaId) FindBestCandidate(
            Vector2Int pos, ClassType classType, int energyCost)
        {
            EnergyRuntime best = null;
            var bestDist = float.MaxValue;
            var bestAntenna = ulong.MaxValue;

            foreach (var energy in energyNodes)
            {
                if (!energy.CanConnectClass(classType) || !energy.HasCapacity(energyCost)) continue;
                var dist = Vector2Int.Distance(pos, energy.GridPosition);
                if (dist <= energy.EnergyRange && dist < bestDist)
                {
                    bestDist = dist;
                    best = energy;
                    bestAntenna = ulong.MaxValue;
                }
            }

            foreach (var kvp in activeAntennas)
            {
                if (!kvp.Value.IsAntenna)
                    continue;

                var range = kvp.Value.AntennaRange;
                if (Vector2Int.Distance(pos, kvp.Value.GridPosition) > range) continue;

                if (!towerToEnergy.TryGetValue(kvp.Key, out var energyId)) continue;
                var energy = FindEnergyNode(energyId);
                if (energy == null || !energy.CanConnectClass(classType) || !energy.HasCapacity(energyCost)) continue;

                var dist = Vector2Int.Distance(pos, kvp.Value.GridPosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = energy;
                    bestAntenna = kvp.Key;
                }
            }

            return (best, bestAntenna);
        }

        private EnergyRuntime FindEnergyNode(ulong networkId)
        {
            foreach (var e in energyNodes)
                if (e.NetworkObjectId == networkId)
                    return e;
            return null;
        }

        private bool TryGetTower(ulong towerId, out TowerRuntime tower)
        {
            tower = null;
            return NetworkManager.Singleton != null
                   && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(towerId, out var obj)
                   && (tower = obj.GetComponent<TowerRuntime>()) != null;
        }

        private void HandleServerStarted()
        {
            BootstrapFromSpawnedRuntimes();
        }
    }
}
