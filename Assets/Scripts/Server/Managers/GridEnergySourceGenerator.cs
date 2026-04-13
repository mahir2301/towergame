using System;
using System.Collections.Generic;
using Game.Shared.Data;
using Game.Shared.Grid;
using UnityEngine;

namespace Game.Server.Managers
{
    internal static class GridEnergySourceGenerator
    {
        public static void Spawn(
            System.Random random,
            Vector2Int gridSize,
            EnergyType[] energyTypes,
            GridManager gridManager,
            int nodeCount,
            int minDistanceBetweenNodes,
            int defaultMaxCapacity,
            int maxAttemptsPerNode,
            int edgePadding)
        {
            if (energyTypes == null || energyTypes.Length == 0)
                return;

            var placed = new List<Vector2Int>();

            foreach (var energyType in energyTypes)
            {
                if (energyType == null)
                    continue;

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
                    if (!gridManager.TryPlaceEnergyRuntime(pos, energyType, defaultMaxCapacity, out _))
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
