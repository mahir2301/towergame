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
            {
                Instance = null;
            }
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
                {
                    toDisconnect.Add(kvp.Key);
                }
            }

            foreach (var towerId in toDisconnect)
            {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(towerId, out var netObj))
                {
                    var tower = netObj.GetComponent<TowerRuntime>();
                    if (tower != null)
                    {
                        CascadeDisconnectTower(tower);
                    }
                }
            }
        }

        public bool TryConnectTowerToEnergy(TowerRuntime tower)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"[EnergyNetwork] Not server, skipping connection for {tower.name}");
                return false;
            }

            var config = tower.GetConfig();
            if (config == null)
            {
                Debug.LogWarning($"[EnergyNetwork] No config for {tower.name}");
                return false;
            }

            var energyCost = config.Stats.energyCost;
            var classType = config.ClassType;
            var towerPos = tower.GridPosition;

            Debug.Log($"[EnergyNetwork] Trying to connect {config.Id} at {towerPos}, cost={energyCost}, class={classType?.DisplayName ?? "null"}, range={energyRuntimes.Count} nodes");

            var candidate = FindBestEnergyCandidate(towerPos, classType, energyCost);
            if (candidate.energy == null)
            {
                Debug.LogWarning($"[EnergyNetwork] No reachable energy source for {config.Id} at {towerPos} (nodes={energyRuntimes.Count}, antennas={activeAntennas.Count})");
                return false;
            }

            if (!candidate.energy.TryConnectTower(tower.NetworkObjectId, energyCost))
            {
                Debug.LogWarning($"[EnergyNetwork] Energy source rejected connection for {config.Id}");
                return false;
            }

            Debug.Log($"[EnergyNetwork] Connected {config.Id} to energy {(candidate.viaAntennaId != ulong.MaxValue ? "via antenna" : "directly")}");

            var towerId = tower.NetworkObjectId;
            var energyId = candidate.energy.NetworkObjectId;
            tower.SetConnection(energyId, candidate.viaAntennaId);
            towerToEnergy[towerId] = energyId;

            if (config.IsAntenna)
            {
                activeAntennas[towerId] = tower;
                antennaDependents[towerId] = new List<ulong>();
            }

            if (candidate.viaAntennaId != ulong.MaxValue &&
                antennaDependents.TryGetValue(candidate.viaAntennaId, out var deps))
            {
                deps.Add(towerId);
            }

            CreatePowerLine(tower, candidate.viaAntennaId);

            return true;
        }

        public void DisconnectTower(TowerRuntime tower)
        {
            if (!IsServer)
            {
                return;
            }

            var towerId = tower.NetworkObjectId;
            var config = tower.GetConfig();
            if (config == null)
            {
                return;
            }

            var energyCost = config.Stats.energyCost;

            if (towerToEnergy.TryGetValue(towerId, out var energyId) && energyById.TryGetValue(energyId, out var energy))
            {
                energy.DisconnectTower(towerId, energyCost);
                towerToEnergy.Remove(towerId);
            }

            if (tower.ConnectedViaAntennaId != ulong.MaxValue &&
                antennaDependents.TryGetValue(tower.ConnectedViaAntennaId, out var deps))
            {
                deps.Remove(towerId);
            }

            if (config.IsAntenna)
            {
                CascadeDisconnectAntenna(towerId);
                activeAntennas.Remove(towerId);
                antennaDependents.Remove(towerId);
            }

            DestroyPowerLine(towerId);
            tower.ClearConnection();
        }

        private void CascadeDisconnectTower(TowerRuntime tower)
        {
            var towerId = tower.NetworkObjectId;
            var config = tower.GetConfig();
            if (config == null)
            {
                return;
            }

            var energyCost = config.Stats.energyCost;

            if (towerToEnergy.TryGetValue(towerId, out var energyId) && energyById.TryGetValue(energyId, out var energy))
            {
                energy.DisconnectTower(towerId, energyCost);
                towerToEnergy.Remove(towerId);
            }

            if (tower.ConnectedViaAntennaId != ulong.MaxValue &&
                antennaDependents.TryGetValue(tower.ConnectedViaAntennaId, out var deps))
            {
                deps.Remove(towerId);
            }

            if (config.IsAntenna)
            {
                CascadeDisconnectAntenna(towerId);
                activeAntennas.Remove(towerId);
                antennaDependents.Remove(towerId);
            }

            DestroyPowerLine(towerId);
            tower.ClearConnection();
        }

        private void CascadeDisconnectAntenna(ulong antennaId)
        {
            if (!antennaDependents.TryGetValue(antennaId, out var dependents))
            {
                return;
            }

            var snapshot = new List<ulong>(dependents);
            foreach (var dependentId in snapshot)
            {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(dependentId, out var netObj))
                {
                    var dependent = netObj.GetComponent<TowerRuntime>();
                    if (dependent != null)
                    {
                        CascadeDisconnectTower(dependent);
                    }
                }
            }

            dependents.Clear();
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
            else if (towerToEnergy.TryGetValue(towerId, out var energyId) && energyById.TryGetValue(energyId, out var energy))
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
            Debug.Log($"[EnergyNetwork] Created power line from {sourceTransform.position} to {tower.transform.position}");
        }

        private void DestroyPowerLine(ulong towerId)
        {
            if (powerLines.TryGetValue(towerId, out var line))
            {
                powerLines.Remove(towerId);
                if (line != null)
                {
                    Destroy(line.gameObject);
                }
            }
        }

        public bool IsPositionInRange(Vector2Int pos, ClassType classType, int energyCost)
        {
            foreach (var energy in energyRuntimes)
            {
                if (!energy.CanConnectClass(classType) || !energy.HasCapacity(energyCost))
                {
                    continue;
                }

                if (Vector2Int.Distance(pos, energy.GridPosition) <= energy.EnergyRange)
                {
                    return true;
                }
            }

            foreach (var kvp in activeAntennas)
            {
                var antenna = kvp.Value;
                var antennaConfig = antenna.GetConfig();
                if (antennaConfig == null)
                {
                    continue;
                }

                if (Vector2Int.Distance(pos, antenna.GridPosition) > antennaConfig.AntennaRange)
                {
                    continue;
                }

                if (!towerToEnergy.TryGetValue(kvp.Key, out var energyId) ||
                    !energyById.TryGetValue(energyId, out var energy))
                {
                    continue;
                }

                if (energy.CanConnectClass(classType) && energy.HasCapacity(energyCost))
                {
                    return true;
                }
            }

            return false;
        }

        public float GetEnergyRangeForPosition(Vector2Int pos, ClassType classType, int energyCost)
        {
            var bestRange = -1f;

            foreach (var energy in energyRuntimes)
            {
                if (!energy.CanConnectClass(classType) || !energy.HasCapacity(energyCost))
                {
                    continue;
                }

                var dist = Vector2Int.Distance(pos, energy.GridPosition);
                if (dist <= energy.EnergyRange)
                {
                    var remaining = energy.EnergyRange - dist;
                    if (remaining > bestRange)
                    {
                        bestRange = remaining;
                    }
                }
            }

            foreach (var kvp in activeAntennas)
            {
                var antenna = kvp.Value;
                var antennaConfig = antenna.GetConfig();
                if (antennaConfig == null)
                {
                    continue;
                }

                if (Vector2Int.Distance(pos, antenna.GridPosition) > antennaConfig.AntennaRange)
                {
                    continue;
                }

                if (!towerToEnergy.TryGetValue(kvp.Key, out var energyId) ||
                    !energyById.TryGetValue(energyId, out var energy))
                {
                    continue;
                }

                if (!energy.CanConnectClass(classType) || !energy.HasCapacity(energyCost))
                {
                    continue;
                }

                var remaining = antennaConfig.AntennaRange - Vector2Int.Distance(pos, antenna.GridPosition);
                if (remaining > bestRange)
                {
                    bestRange = remaining;
                }
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
                {
                    continue;
                }

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
                var antennaConfig = antenna.GetConfig();
                if (antennaConfig == null)
                {
                    continue;
                }

                var antennaPos = antenna.GridPosition;
                var antennaRange = antennaConfig.AntennaRange;

                if (Vector2Int.Distance(pos, antennaPos) > antennaRange)
                {
                    continue;
                }

                if (!towerToEnergy.TryGetValue(kvp.Key, out var energyId) ||
                    !energyById.TryGetValue(energyId, out var energy))
                {
                    continue;
                }

                if (!energy.CanConnectClass(classType) || !energy.HasCapacity(energyCost))
                {
                    continue;
                }

                var dist = Vector2Int.Distance(pos, antennaPos);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestEnergy = energy;
                    bestAntennaId = kvp.Key;
                }
            }

            return (bestEnergy, bestAntennaId);
        }
    }
}