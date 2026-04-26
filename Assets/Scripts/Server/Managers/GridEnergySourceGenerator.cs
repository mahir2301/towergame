using System;
using System.Collections.Generic;
using Shared.Data;
using Shared.Grid;
using Shared.Runtime;
using UnityEngine;

namespace Server.Managers
{
    internal static class GridEnergySourceGenerator
    {
        public static void Spawn(
            System.Random random,
            Vector2Int gridSize,
            PlaceableType[] energyPlaceables,
            GridManager gridManager,
            ServerSpawnManager spawnManager,
            int nodeCount,
            int minDistanceBetweenNodes,
            int defaultMaxCapacity,
            int maxAttemptsPerNode,
            int edgePadding,
            Vector2Int nexusCenter = default,
            int nexusExclusionZone = 0)
        {
            if (energyPlaceables == null || energyPlaceables.Length == 0)
                return;

            var placed = new List<Vector2Int>();
            var hasExclusion = nexusExclusionZone > 0 && nexusCenter != default;
            var nexusHalf = NexusRuntime.NexusSize / 2;
            var nexusMin = new Vector2Int(nexusCenter.x - nexusHalf, nexusCenter.y - nexusHalf);
            var nexusMax = new Vector2Int(nexusMin.x + NexusRuntime.NexusSize - 1, nexusMin.y + NexusRuntime.NexusSize - 1);

            foreach (var energyPlaceable in energyPlaceables)
            {
                if (energyPlaceable == null)
                    continue;

                var energyRuntime = energyPlaceable.Prefab != null
                    ? energyPlaceable.Prefab.GetComponent<EnergyRuntime>()
                    : null;
                if (energyRuntime == null)
                    continue;

                var energyRange = energyRuntime.EnergyRange;

                var spawnedForType = 0;
                var attempts = 0;
                while (spawnedForType < nodeCount && attempts < maxAttemptsPerNode)
                {
                    attempts++;

                    var x = random.Next(edgePadding, Math.Max(edgePadding + 1, gridSize.x - edgePadding));
                    var y = random.Next(edgePadding, Math.Max(edgePadding + 1, gridSize.y - edgePadding));
                    var pos = new Vector2Int(x, y);

                    if (!IsFarEnough(pos, placed, minDistanceBetweenNodes))
                        continue;
                    if (!gridManager.IsCellAvailable(pos, Vector2Int.one, null))
                        continue;
                    if (hasExclusion)
                    {
                        var dx = Mathf.Max(0, Mathf.Max(nexusMin.x - pos.x, pos.x - nexusMax.x));
                        var dy = Mathf.Max(0, Mathf.Max(nexusMin.y - pos.y, pos.y - nexusMax.y));
                        if (Mathf.Max(dx, dy) < nexusExclusionZone)
                            continue;
                        var distToCenter = Mathf.Max(Mathf.Abs(pos.x - nexusCenter.x), Mathf.Abs(pos.y - nexusCenter.y));
                        if (distToCenter < energyRange)
                            continue;
                    }
                    if (!spawnManager.TryPlacePlaceableRuntime(pos, energyPlaceable, out _, out _, defaultMaxCapacity))
                        continue;

                    placed.Add(pos);
                    spawnedForType++;
                }
            }
        }

        private static bool IsFarEnough(Vector2Int pos, List<Vector2Int> existing, int minDistance)
        {
            for (var i = 0; i < existing.Count; i++)
            {
                var other = existing[i];
                var dist = Mathf.Max(Mathf.Abs(pos.x - other.x), Mathf.Abs(pos.y - other.y));
                if (dist < minDistance)
                    return false;
            }

            return true;
        }
    }
}
