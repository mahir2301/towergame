using System.Collections.Generic;
using Game.Client.UI;
using Game.Shared.Grid;
using Game.Shared.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Game.Client.Visuals
{
    public class ClientRuntimePresentationManager : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;

        private readonly Dictionary<ulong, EnergyRuntime> registeredEnergy = new();
        private readonly Dictionary<ulong, TowerRuntime> registeredTowers = new();
        private readonly Dictionary<ulong, RangeIndicator> energyIndicators = new();
        private readonly Dictionary<ulong, RangeIndicator> towerIndicators = new();

        private void LateUpdate()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
                return;

            SyncEnergyPresentation();
            SyncTowerPresentation();
        }

        private void SyncEnergyPresentation()
        {
            var seen = new HashSet<ulong>();
            var energyNodes = FindObjectsByType<EnergyRuntime>(FindObjectsSortMode.None);
            for (var i = 0; i < energyNodes.Length; i++)
            {
                var energy = energyNodes[i];
                if (energy == null || !energy.IsSpawned)
                    continue;

                var id = energy.NetworkObjectId;
                seen.Add(id);

                if (!registeredEnergy.ContainsKey(id))
                {
                    registeredEnergy[id] = energy;
                    WorldOverlayManager.Instance?.RegisterEnergy(energy);
                    gridManager?.RegisterOccupiedCells(energy.GridPosition, Vector2Int.one, energy.gameObject);
                }

                if (!energyIndicators.TryGetValue(id, out var indicator) || indicator == null)
                {
                    indicator = CreateIndicator(energy.transform, "EnergyRangeIndicator");
                    energyIndicators[id] = indicator;
                }

                indicator.ShowEnergy(energy.EnergyRange);
            }

            RemoveStaleEnergy(seen);
        }

        private void SyncTowerPresentation()
        {
            var seen = new HashSet<ulong>();
            var towers = FindObjectsByType<TowerRuntime>(FindObjectsSortMode.None);
            for (var i = 0; i < towers.Length; i++)
            {
                var tower = towers[i];
                if (tower == null || !tower.IsSpawned)
                    continue;

                var id = tower.NetworkObjectId;
                seen.Add(id);

                if (!registeredTowers.ContainsKey(id))
                {
                    registeredTowers[id] = tower;
                    WorldOverlayManager.Instance?.RegisterTower(tower);
                    gridManager?.RegisterOccupiedCells(tower.GridPosition, tower.Size, tower.gameObject);
                }

                if (tower.Config == null || !tower.Config.IsAntenna)
                    continue;

                if (!towerIndicators.TryGetValue(id, out var indicator) || indicator == null)
                {
                    indicator = CreateIndicator(tower.transform, "AntennaRangeIndicator");
                    towerIndicators[id] = indicator;
                }

                indicator.Show(tower.Config.Stats.antennaRange, tower.IsPowered);
            }

            RemoveStaleTowers(seen);
        }

        private static RangeIndicator CreateIndicator(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RangeIndicator>();
        }

        private void RemoveStaleEnergy(HashSet<ulong> seen)
        {
            var stale = new List<ulong>();
            foreach (var kvp in registeredEnergy)
            {
                if (!seen.Contains(kvp.Key))
                    stale.Add(kvp.Key);
            }

            for (var i = 0; i < stale.Count; i++)
            {
                var id = stale[i];
                if (registeredEnergy.TryGetValue(id, out var energy))
                {
                    WorldOverlayManager.Instance?.UnregisterEnergy(energy);
                    if (energy != null)
                        gridManager?.UnregisterOccupiedCells(energy.GridPosition, Vector2Int.one);
                }
                registeredEnergy.Remove(id);

                if (energyIndicators.TryGetValue(id, out var indicator) && indicator != null)
                    Destroy(indicator.gameObject);
                energyIndicators.Remove(id);
            }
        }

        private void RemoveStaleTowers(HashSet<ulong> seen)
        {
            var stale = new List<ulong>();
            foreach (var kvp in registeredTowers)
            {
                if (!seen.Contains(kvp.Key))
                    stale.Add(kvp.Key);
            }

            for (var i = 0; i < stale.Count; i++)
            {
                var id = stale[i];
                if (registeredTowers.TryGetValue(id, out var tower))
                {
                    WorldOverlayManager.Instance?.UnregisterTower(tower);
                    if (tower != null)
                        gridManager?.UnregisterOccupiedCells(tower.GridPosition, tower.Size);
                }
                registeredTowers.Remove(id);

                if (towerIndicators.TryGetValue(id, out var indicator) && indicator != null)
                    Destroy(indicator.gameObject);
                towerIndicators.Remove(id);
            }
        }

        private void OnDestroy()
        {
            foreach (var kvp in energyIndicators)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
            foreach (var kvp in towerIndicators)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
        }
    }
}
