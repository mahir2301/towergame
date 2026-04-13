using System.Collections.Generic;
using Shared.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Client.Visuals
{
    public class ClientPowerLineManager : MonoBehaviour
    {
        private readonly Dictionary<ulong, PowerLineVisual> linesByTower = new();

        private void LateUpdate()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
                return;

            RefreshLines();
        }

        private void RefreshLines()
        {
            if (ClientObjectRegistry.Instance == null)
                return;

            var towers = ClientObjectRegistry.Instance.Towers;
            var energyNodes = ClientObjectRegistry.Instance.EnergyNodes;

            var activeTowers = new HashSet<ulong>();

            foreach (var kvp in towers)
            {
                var tower = kvp.Value;
                if (tower == null || !tower.IsSpawned || !tower.IsPowered || tower.ConnectedEnergyId == ulong.MaxValue)
                    continue;

                if (!TryResolveSource(tower, energyNodes, towers, out var source))
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

        private static bool TryResolveSource(TowerRuntime tower,
            IReadOnlyDictionary<ulong, EnergyRuntime> energyNodes,
            IReadOnlyDictionary<ulong, TowerRuntime> towers,
            out Transform source)
        {
            source = null;
            if (tower.ConnectedViaAntennaId != ulong.MaxValue && towers.TryGetValue(tower.ConnectedViaAntennaId, out var antenna))
            {
                source = antenna.transform;
                return true;
            }

            if (energyNodes.TryGetValue(tower.ConnectedEnergyId, out var energy))
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
