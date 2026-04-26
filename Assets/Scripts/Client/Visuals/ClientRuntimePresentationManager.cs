using System.Collections.Generic;
using Client.UI;
using Shared;
using Shared.Grid;
using Shared.Runtime;
using Shared.Runtime.Placeables;
using Shared.Utilities;
using UnityEngine;

namespace Client.Visuals
{
    public class ClientRuntimePresentationManager : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;

        private readonly Dictionary<ulong, EnergyRuntime> energyById = new();
        private readonly Dictionary<ulong, TowerRuntime> towerById = new();
        private readonly Dictionary<ulong, RangeIndicator> energyIndicators = new();
        private readonly Dictionary<ulong, RangeIndicator> towerIndicators = new();

        private void OnEnable()
        {
            if (!RuntimeNet.ShouldRunNetworkedClientSystems())
            {
                enabled = false;
                return;
            }

            GameEvents.PlaceableSpawned += HandlePlaceableSpawned;
            GameEvents.PlaceableDespawned += HandlePlaceableDespawned;

            BootstrapExistingRuntimes();
        }

        private void OnDisable()
        {
            GameEvents.PlaceableSpawned -= HandlePlaceableSpawned;
            GameEvents.PlaceableDespawned -= HandlePlaceableDespawned;

            ClearAllPresentation();
        }

        private void LateUpdate()
        {
            if (!IsClientActive())
                return;

            foreach (var kvp in towerIndicators)
            {
                if (!towerById.TryGetValue(kvp.Key, out var tower) || tower == null || !tower.IsSpawned || kvp.Value == null)
                    continue;

                if (!tower.IsAntenna)
                    continue;

                kvp.Value.UpdateRange(tower.AntennaRange, tower.IsPowered);
            }
        }

        private void BootstrapExistingRuntimes()
        {
            if (!IsClientActive())
                return;

            var energies = FindObjectsByType<EnergyRuntime>(FindObjectsSortMode.None);
            for (var i = 0; i < energies.Length; i++)
                HandleEnergySpawned(energies[i]);

            var towers = FindObjectsByType<TowerRuntime>(FindObjectsSortMode.None);
            for (var i = 0; i < towers.Length; i++)
                HandleTowerSpawned(towers[i]);
        }

        private void HandleEnergySpawned(EnergyRuntime energy)
        {
            if (!IsClientActive() || energy == null || !energy.IsSpawned)
                return;

            var id = energy.NetworkObjectId;
            if (energyById.ContainsKey(id))
                return;

            energyById[id] = energy;
            WorldOverlayManager.Instance?.RegisterEnergy(energy);
            gridManager?.RegisterOccupiedCells(energy.GridPosition, Vector2Int.one, energy.gameObject);

            var indicator = CreateIndicator(energy.transform, "EnergyRangeIndicator");
            indicator.ShowEnergy(energy.EnergyRange);
            energyIndicators[id] = indicator;
        }

        private void HandlePlaceableSpawned(PlaceableBehavior placeable)
        {
            if (placeable is EnergyRuntime energy)
            {
                HandleEnergySpawned(energy);
                return;
            }

            if (placeable is TowerRuntime tower)
                HandleTowerSpawned(tower);
        }

        private void HandlePlaceableDespawned(PlaceableBehavior placeable)
        {
            if (placeable is EnergyRuntime energy)
            {
                HandleEnergyDespawned(energy);
                return;
            }

            if (placeable is TowerRuntime tower)
                HandleTowerDespawned(tower);
        }

        private void HandleEnergyDespawned(EnergyRuntime energy)
        {
            if (energy == null)
                return;

            var id = energy.NetworkObjectId;
            WorldOverlayManager.Instance?.UnregisterEnergy(energy);
            gridManager?.UnregisterOccupiedCells(energy.GridPosition, Vector2Int.one);
            energyById.Remove(id);

            if (energyIndicators.TryGetValue(id, out var indicator) && indicator != null)
                Destroy(indicator.gameObject);

            energyIndicators.Remove(id);
        }

        private void HandleTowerSpawned(TowerRuntime tower)
        {
            if (!IsClientActive() || tower == null || !tower.IsSpawned)
                return;

            var id = tower.NetworkObjectId;
            if (towerById.ContainsKey(id))
                return;

            towerById[id] = tower;
            WorldOverlayManager.Instance?.RegisterTower(tower);
            gridManager?.RegisterOccupiedCells(tower.GridPosition, tower.Size, tower.gameObject);

            if (!tower.IsAntenna)
                return;

            var indicator = CreateIndicator(tower.transform, "AntennaRangeIndicator");
            indicator.Show(tower.AntennaRange, tower.IsPowered);
            towerIndicators[id] = indicator;
        }

        private void HandleTowerDespawned(TowerRuntime tower)
        {
            if (tower == null)
                return;

            var id = tower.NetworkObjectId;
            WorldOverlayManager.Instance?.UnregisterTower(tower);
            gridManager?.UnregisterOccupiedCells(tower.GridPosition, tower.Size);
            towerById.Remove(id);

            if (towerIndicators.TryGetValue(id, out var indicator) && indicator != null)
                Destroy(indicator.gameObject);

            towerIndicators.Remove(id);
        }

        private static bool IsClientActive()
        {
            return RuntimeNet.ShouldRunNetworkedClientSystems();
        }

        private static RangeIndicator CreateIndicator(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RangeIndicator>();
        }

        private void ClearAllPresentation()
        {
            foreach (var kvp in energyById)
            {
                if (kvp.Value == null)
                    continue;

                WorldOverlayManager.Instance?.UnregisterEnergy(kvp.Value);
                gridManager?.UnregisterOccupiedCells(kvp.Value.GridPosition, Vector2Int.one);
            }

            foreach (var kvp in towerById)
            {
                if (kvp.Value == null)
                    continue;

                WorldOverlayManager.Instance?.UnregisterTower(kvp.Value);
                gridManager?.UnregisterOccupiedCells(kvp.Value.GridPosition, kvp.Value.Size);
            }

            foreach (var kvp in energyIndicators)
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);

            foreach (var kvp in towerIndicators)
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);

            energyById.Clear();
            towerById.Clear();
            energyIndicators.Clear();
            towerIndicators.Clear();
        }
    }
}
