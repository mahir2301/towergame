using System.Collections.Generic;
using Shared.Data;
using Shared.Grid;
using Shared.Runtime;
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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnergyRuntime.ServerSpawned += RegisterEnergyRuntime;
            EnergyRuntime.ServerDespawned += UnregisterEnergyRuntime;
            TowerRuntime.ServerSpawned += OnTowerSpawned;
            TowerRuntime.ServerDespawned += OnTowerDespawned;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            EnergyRuntime.ServerSpawned -= RegisterEnergyRuntime;
            EnergyRuntime.ServerDespawned -= UnregisterEnergyRuntime;
            TowerRuntime.ServerSpawned -= OnTowerSpawned;
            TowerRuntime.ServerDespawned -= OnTowerDespawned;

            if (Instance == this)
                Instance = null;
        }

        private void RegisterEnergyRuntime(EnergyRuntime runtime)
        {
            energyNodes.Add(runtime);
        }

        private void UnregisterEnergyRuntime(EnergyRuntime runtime)
        {
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
            TryConnectTowerToEnergy(tower);
        }

        private void OnTowerDespawned(TowerRuntime tower)
        {
            DisconnectTower(tower);
        }

        public bool TryConnectTowerToEnergy(TowerRuntime tower)
        {
            if (!IsServer) return false;

            var config = tower.Config;
            if (config == null) return false;

            var candidate = FindBestCandidate(tower.GridPosition, config.ClassType, config.Stats.energyCost);
            if (candidate.energy == null)
                return false;

            if (!candidate.energy.TryConnectTower(tower.NetworkObjectId, config.Stats.energyCost))
                return false;

            var towerId = tower.NetworkObjectId;
            tower.SetConnection(candidate.energy.NetworkObjectId, candidate.viaAntennaId);
            towerToEnergy[towerId] = candidate.energy.NetworkObjectId;

            if (config.IsAntenna)
                activeAntennas[towerId] = tower;
            return true;
        }

        public void DisconnectTower(TowerRuntime tower)
        {
            if (!IsServer) return;

            var towerId = tower.NetworkObjectId;
            var config = tower.Config;
            if (config == null) return;

            // Refund capacity to energy node
            if (towerToEnergy.TryGetValue(towerId, out var energyId))
            {
                var energy = FindEnergyNode(energyId);
                energy?.DisconnectTower(towerId, config.Stats.energyCost);
                towerToEnergy.Remove(towerId);
            }

            // If this tower is an antenna, cascade disconnect all dependents
            if (config.IsAntenna)
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
                    energy?.DisconnectTower(depId, dep.Config?.Stats.energyCost ?? 0);
                }

                towerToEnergy.Remove(depId);
                dep.ClearConnection();
            }
        }

        public bool IsPositionInRange(Vector2Int pos, ClassType classType, int energyCost)
        {
            foreach (var energy in energyNodes)
            {
                if (!energy.CanConnectClass(classType) || !energy.HasCapacity(energyCost)) continue;
                if (Vector2Int.Distance(pos, energy.GridPosition) <= energy.EnergyRange)
                    return true;
            }

            foreach (var kvp in activeAntennas)
            {
                var antennaConfig = kvp.Value.Config;
                if (antennaConfig == null) continue;

                if (Vector2Int.Distance(pos, kvp.Value.GridPosition) > antennaConfig.Stats.antennaRange)
                    continue;

                if (!towerToEnergy.TryGetValue(kvp.Key, out var energyId)) continue;
                var energy = FindEnergyNode(energyId);
                if (energy != null && energy.CanConnectClass(classType) && energy.HasCapacity(energyCost))
                    return true;
            }

            return false;
        }

        public float GetEnergyRangeForPosition(Vector2Int pos, ClassType classType, int energyCost)
        {
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
                var cfg = kvp.Value.Config;
                if (cfg == null) continue;
                var range = cfg.Stats.antennaRange;
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
                var cfg = kvp.Value.Config;
                if (cfg == null) continue;
                var range = cfg.Stats.antennaRange;
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
            return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(towerId, out var obj)
                   && (tower = obj.GetComponent<TowerRuntime>()) != null;
        }
    }
}
