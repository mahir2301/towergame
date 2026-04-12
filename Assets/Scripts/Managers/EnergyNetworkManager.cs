using System.Collections.Generic;
using Data;
using Runtime;
using Unity.Netcode;
using UnityEngine;
using Visuals;

namespace Managers
{
    public class EnergyNetworkManager : NetworkBehaviour
    {
        public static EnergyNetworkManager Instance { get; private set; }

        private readonly List<EnergyRuntime> energyRuntimes = new();
        private readonly Dictionary<ulong, EnergyRuntime> energyById = new();
        private readonly Dictionary<ulong, TowerRuntime> activeAntennas = new();
        private readonly Dictionary<ulong, List<ulong>> antennaDependents = new();
        private readonly Dictionary<ulong, ulong> towerToEnergy = new();
        private readonly Dictionary<ulong, PowerLineVisual> powerLines = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this)
                Instance = null;
        }

        public void RegisterEnergyRuntime(EnergyRuntime runtime)
        {
            energyRuntimes.Add(runtime);
            energyById[runtime.NetworkObjectId] = runtime;
        }

        public void UnregisterEnergyRuntime(EnergyRuntime runtime)
        {
            var energyId = runtime.NetworkObjectId;
            energyRuntimes.Remove(runtime);
            energyById.Remove(energyId);

            var toDisconnect = new List<ulong>();
            foreach (var kvp in towerToEnergy)
            {
                if (kvp.Value == energyId)
                    toDisconnect.Add(kvp.Key);
            }

            foreach (var towerId in toDisconnect)
            {
                if (TryGetTower(towerId, out var tower))
                    DisconnectTowerInternal(tower, cascadeAntenna: true);
            }
        }

        public bool TryConnectTowerToEnergy(TowerRuntime tower)
        {
            if (!IsServer) return false;

            var config = tower.Config;
            if (config == null) return false;

            var energyCost = config.Stats.energyCost;
            var candidate = FindBestEnergyCandidate(tower.GridPosition, config.ClassType, energyCost);

            if (candidate.energy == null)
                return false;

            if (!candidate.energy.TryConnectTower(tower.NetworkObjectId, energyCost))
                return false;

            var towerId = tower.NetworkObjectId;
            var energyId = candidate.energy.NetworkObjectId;
            tower.SetConnection(energyId, candidate.viaAntennaId);
            towerToEnergy[towerId] = energyId;

            if (config.IsAntenna)
            {
                activeAntennas[towerId] = tower;
                antennaDependents[towerId] = new List<ulong>();
            }

            if (candidate.viaAntennaId != ulong.MaxValue
                && antennaDependents.TryGetValue(candidate.viaAntennaId, out var deps))
            {
                deps.Add(towerId);
            }

            CreatePowerLine(tower, candidate.viaAntennaId);
            return true;
        }

        public void DisconnectTower(TowerRuntime tower)
        {
            if (!IsServer) return;
            DisconnectTowerInternal(tower, cascadeAntenna: true);
        }

        private void DisconnectTowerInternal(TowerRuntime tower, bool cascadeAntenna)
        {
            var towerId = tower.NetworkObjectId;
            var config = tower.Config;

            if (towerToEnergy.TryGetValue(towerId, out var energyId)
                && energyById.TryGetValue(energyId, out var energy)
                && config != null)
            {
                energy.DisconnectTower(towerId, config.Stats.energyCost);
                towerToEnergy.Remove(towerId);
            }

            if (tower.ConnectedViaAntennaId != ulong.MaxValue
                && antennaDependents.TryGetValue(tower.ConnectedViaAntennaId, out var deps))
            {
                deps.Remove(towerId);
            }

            if (cascadeAntenna && config != null && config.IsAntenna)
            {
                DisconnectAntennaDependents(towerId);
                activeAntennas.Remove(towerId);
                antennaDependents.Remove(towerId);
            }

            DestroyPowerLine(towerId);
            tower.ClearConnection();
        }

        private void DisconnectAntennaDependents(ulong antennaId)
        {
            if (!antennaDependents.TryGetValue(antennaId, out var dependents))
                return;

            var snapshot = new List<ulong>(dependents);
            foreach (var dependentId in snapshot)
            {
                if (TryGetTower(dependentId, out var dependent))
                    DisconnectTowerInternal(dependent, cascadeAntenna: true);
            }
            dependents.Clear();
        }

        public bool IsPositionInRange(Vector2Int pos, ClassType classType, int energyCost)
        {
            foreach (var energy in energyRuntimes)
            {
                if (!energy.CanConnectClass(classType) || !energy.HasCapacity(energyCost))
                    continue;

                if (Vector2Int.Distance(pos, energy.GridPosition) <= energy.EnergyRange)
                    return true;
            }

            foreach (var kvp in activeAntennas)
            {
                var antenna = kvp.Value;
                var antennaConfig = antenna.Config;
                if (antennaConfig == null) continue;

                if (Vector2Int.Distance(pos, antenna.GridPosition) > antennaConfig.Stats.antennaRange)
                    continue;

                if (!towerToEnergy.TryGetValue(kvp.Key, out var energyId)
                    || !energyById.TryGetValue(energyId, out var energy))
                    continue;

                if (energy.CanConnectClass(classType) && energy.HasCapacity(energyCost))
                    return true;
            }

            return false;
        }

        public float GetEnergyRangeForPosition(Vector2Int pos, ClassType classType, int energyCost)
        {
            var bestRange = -1f;

            foreach (var energy in energyRuntimes)
            {
                if (!energy.CanConnectClass(classType) || !energy.HasCapacity(energyCost))
                    continue;

                var dist = Vector2Int.Distance(pos, energy.GridPosition);
                if (dist <= energy.EnergyRange)
                {
                    var remaining = energy.EnergyRange - dist;
                    if (remaining > bestRange)
                        bestRange = remaining;
                }
            }

            foreach (var kvp in activeAntennas)
            {
                var antenna = kvp.Value;
                var antennaConfig = antenna.Config;
                if (antennaConfig == null) continue;

                var antennaRange = antennaConfig.Stats.antennaRange;
                if (Vector2Int.Distance(pos, antenna.GridPosition) > antennaRange)
                    continue;

                if (!towerToEnergy.TryGetValue(kvp.Key, out var energyId)
                    || !energyById.TryGetValue(energyId, out var energy))
                    continue;

                if (!energy.CanConnectClass(classType) || !energy.HasCapacity(energyCost))
                    continue;

                var remaining = antennaRange - Vector2Int.Distance(pos, antenna.GridPosition);
                if (remaining > bestRange)
                    bestRange = remaining;
            }

            return bestRange;
        }

        private (EnergyRuntime energy, ulong viaAntennaId) FindBestEnergyCandidate(
            Vector2Int pos, ClassType classType, int energyCost)
        {
            EnergyRuntime bestEnergy = null;
            var bestDistance = float.MaxValue;
            var bestAntennaId = ulong.MaxValue;

            foreach (var energy in energyRuntimes)
            {
                if (!energy.CanConnectClass(classType) || !energy.HasCapacity(energyCost))
                    continue;

                var dist = Vector2Int.Distance(pos, energy.GridPosition);
                if (dist <= energy.EnergyRange && dist < bestDistance)
                {
                    bestDistance = dist;
                    bestEnergy = energy;
                    bestAntennaId = ulong.MaxValue;
                }
            }

            foreach (var kvp in activeAntennas)
            {
                var antenna = kvp.Value;
                var antennaConfig = antenna.Config;
                if (antennaConfig == null) continue;

                var antennaRange = antennaConfig.Stats.antennaRange;
                if (Vector2Int.Distance(pos, antenna.GridPosition) > antennaRange)
                    continue;

                if (!towerToEnergy.TryGetValue(kvp.Key, out var energyId)
                    || !energyById.TryGetValue(energyId, out var energy))
                    continue;

                if (!energy.CanConnectClass(classType) || !energy.HasCapacity(energyCost))
                    continue;

                var dist = Vector2Int.Distance(pos, antenna.GridPosition);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestEnergy = energy;
                    bestAntennaId = kvp.Key;
                }
            }

            return (bestEnergy, bestAntennaId);
        }

        private void CreatePowerLine(TowerRuntime tower, ulong viaAntennaId)
        {
            var towerId = tower.NetworkObjectId;
            DestroyPowerLine(towerId);

            Transform sourceTransform;

            if (viaAntennaId != ulong.MaxValue && activeAntennas.TryGetValue(viaAntennaId, out var antenna))
            {
                sourceTransform = antenna.transform;
            }
            else if (towerToEnergy.TryGetValue(towerId, out var energyId)
                     && energyById.TryGetValue(energyId, out var energy))
            {
                sourceTransform = energy.transform;
            }
            else
            {
                return;
            }

            var lineGo = new GameObject($"PowerLine_{towerId}");
            lineGo.transform.SetParent(transform);
            var powerLine = lineGo.AddComponent<PowerLineVisual>();
            powerLine.Setup(sourceTransform.position, tower.transform.position);
            powerLines[towerId] = powerLine;
        }

        private void DestroyPowerLine(ulong towerId)
        {
            if (powerLines.TryGetValue(towerId, out var line))
            {
                powerLines.Remove(towerId);
                if (line != null)
                    Destroy(line.gameObject);
            }
        }

        private bool TryGetTower(ulong towerId, out TowerRuntime tower)
        {
            tower = null;
            return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(towerId, out var netObj)
                   && (tower = netObj.GetComponent<TowerRuntime>()) != null;
        }
    }
}
