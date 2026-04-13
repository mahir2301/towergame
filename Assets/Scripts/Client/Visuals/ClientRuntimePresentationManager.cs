using System.Collections.Generic;
using Client.UI;
using Shared.Grid;
using Shared.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Client.Visuals
{
    public class ClientRuntimePresentationManager : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;

        private readonly HashSet<ulong> registeredEnergy = new();
        private readonly HashSet<ulong> registeredTowers = new();
        private readonly Dictionary<ulong, RangeIndicator> energyIndicators = new();
        private readonly Dictionary<ulong, RangeIndicator> towerIndicators = new();

        private void LateUpdate()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
                return;
            if (ClientObjectRegistry.Instance == null)
                return;

            SyncEnergyPresentation();
            SyncTowerPresentation();
        }

        private void SyncEnergyPresentation()
        {
            var energyNodes = ClientObjectRegistry.Instance.EnergyNodes;
            var seen = new HashSet<ulong>();

            foreach (var kvp in energyNodes)
            {
                var energy = kvp.Value;
                if (energy == null || !energy.IsSpawned)
                    continue;

                var id = energy.NetworkObjectId;
                seen.Add(id);

                if (!registeredEnergy.Contains(id))
                {
                    registeredEnergy.Add(id);
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
            var towers = ClientObjectRegistry.Instance.Towers;
            var seen = new HashSet<ulong>();

            foreach (var kvp in towers)
            {
                var tower = kvp.Value;
                if (tower == null || !tower.IsSpawned)
                    continue;

                var id = tower.NetworkObjectId;
                seen.Add(id);

                if (!registeredTowers.Contains(id))
                {
                    registeredTowers.Add(id);
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
            foreach (var id in registeredEnergy)
            {
                if (!seen.Contains(id))
                    stale.Add(id);
            }

            for (var i = 0; i < stale.Count; i++)
            {
                var id = stale[i];
                var energy = ClientObjectRegistry.Instance.EnergyNodes.TryGetValue(id, out var e) ? e : null;
                WorldOverlayManager.Instance?.UnregisterEnergy(energy);
                if (energy != null)
                    gridManager?.UnregisterOccupiedCells(energy.GridPosition, Vector2Int.one);
                registeredEnergy.Remove(id);

                if (energyIndicators.TryGetValue(id, out var indicator) && indicator != null)
                    Destroy(indicator.gameObject);
                energyIndicators.Remove(id);
            }
        }

        private void RemoveStaleTowers(HashSet<ulong> seen)
        {
            var stale = new List<ulong>();
            foreach (var id in registeredTowers)
            {
                if (!seen.Contains(id))
                    stale.Add(id);
            }

            for (var i = 0; i < stale.Count; i++)
            {
                var id = stale[i];
                var tower = ClientObjectRegistry.Instance.Towers.TryGetValue(id, out var t) ? t : null;
                WorldOverlayManager.Instance?.UnregisterTower(tower);
                if (tower != null)
                    gridManager?.UnregisterOccupiedCells(tower.GridPosition, tower.Size);
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
