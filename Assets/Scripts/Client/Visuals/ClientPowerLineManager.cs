using System.Collections.Generic;
using Game.Shared.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Game.Client.Visuals
{
    public class ClientPowerLineManager : MonoBehaviour
    {
        private readonly Dictionary<ulong, PowerLineVisual> linesByTower = new();
        private readonly Dictionary<ulong, TowerRuntime> towersById = new();
        private readonly Dictionary<ulong, EnergyRuntime> energyById = new();

        private void LateUpdate()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
                return;

            RebuildLookup();
            RefreshLines();
        }

        private void RebuildLookup()
        {
            towersById.Clear();
            energyById.Clear();

            var towers = FindObjectsByType<TowerRuntime>(FindObjectsSortMode.None);
            for (var i = 0; i < towers.Length; i++)
            {
                var tower = towers[i];
                if (tower != null && tower.IsSpawned)
                    towersById[tower.NetworkObjectId] = tower;
            }

            var energyNodes = FindObjectsByType<EnergyRuntime>(FindObjectsSortMode.None);
            for (var i = 0; i < energyNodes.Length; i++)
            {
                var energy = energyNodes[i];
                if (energy != null && energy.IsSpawned)
                    energyById[energy.NetworkObjectId] = energy;
            }
        }

        private void RefreshLines()
        {
            var activeTowers = new HashSet<ulong>();

            foreach (var kvp in towersById)
            {
                var tower = kvp.Value;
                if (tower == null || !tower.IsPowered || tower.ConnectedEnergyId == ulong.MaxValue)
                    continue;

                if (!TryResolveSource(tower, out var source))
                    continue;

                activeTowers.Add(tower.NetworkObjectId);
                var line = GetOrCreateLine(tower.NetworkObjectId);
                line.SetEndpoints(source.position, tower.transform.position);
            }

            var stale = new List<ulong>();
            foreach (var kvp in linesByTower)
            {
                if (!activeTowers.Contains(kvp.Key))
                    stale.Add(kvp.Key);
            }

            for (var i = 0; i < stale.Count; i++)
            {
                var id = stale[i];
                if (!linesByTower.TryGetValue(id, out var line))
                    continue;

                if (line != null)
                    Destroy(line.gameObject);
                linesByTower.Remove(id);
            }
        }

        private bool TryResolveSource(TowerRuntime tower, out Transform source)
        {
            source = null;
            if (tower.ConnectedViaAntennaId != ulong.MaxValue && towersById.TryGetValue(tower.ConnectedViaAntennaId, out var antenna))
            {
                source = antenna.transform;
                return true;
            }

            if (energyById.TryGetValue(tower.ConnectedEnergyId, out var energy))
            {
                source = energy.transform;
                return true;
            }

            return false;
        }

        private PowerLineVisual GetOrCreateLine(ulong towerId)
        {
            if (linesByTower.TryGetValue(towerId, out var existing) && existing != null)
                return existing;

            var go = new GameObject($"PowerLine_{towerId}");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<PowerLineVisual>();
            linesByTower[towerId] = line;
            return line;
        }

        private void OnDestroy()
        {
            foreach (var kvp in linesByTower)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
            linesByTower.Clear();
        }
    }
}
