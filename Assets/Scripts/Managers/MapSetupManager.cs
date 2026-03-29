using Data;
using Runtime;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Managers
{
    public class MapSetupManager : MonoBehaviour
    {
        [SerializeField]
        private int nodeCount;
        [SerializeField]
        private int minDistanceBetweenNodes;
        [SerializeField]
        private int defaultMaxCapacity;
        [SerializeField]
        private EnergyType[] energyNodeConfig;
        [SerializeField]
        private GridManager gridManager;

        private void Start()
        {
            if (energyNodeConfig == null || energyNodeConfig.Length == 0)
            {
                return;
            }

            SpawnEnergyNodes();
        }

        private static readonly Vector2Int Default = new(1, 1);

        private void SpawnEnergyNodes()
        {
            var gridSize = gridManager.GridSize;
            var placed = 0;
            var placedNodesPos = new List<Vector2Int>();

            var attempts = 0;
            const int maxAttempts = 100;

            foreach (var node in energyNodeConfig)
            {
                while (placed < nodeCount && attempts < maxAttempts)
                {
                    attempts++;

                    var x = Random.Range(2, gridSize.x - Default.x);
                    var y = Random.Range(2, gridSize.y - Default.y);
                    var newPos = new Vector2Int(x, y);

                    if (!IsValidPosition(newPos, placedNodesPos) ||
                        gridManager.IsWaterCell(newPos) ||
                        !gridManager.TryPlaceEnergyRuntime(newPos, node, defaultMaxCapacity, out var energyRuntime))
                    {
                        continue;
                    }

                    placed++;
                    placedNodesPos.Add(newPos);
                    Debug.Log($"Placed energy node at {newPos}");
                }

                placed = 0;

                Debug.Log($"Placed {placed} energy nodes after {attempts} attempts");
            }
        }

        private bool IsValidPosition(Vector2Int pos, List<Vector2Int> existing)
        {
            return existing
                .Select(existingPos => Mathf.Max(Mathf.Abs(pos.x - existingPos.x), Mathf.Abs(pos.y - existingPos.y)))
                .All(dist => dist >= minDistanceBetweenNodes);
        }
    }
}