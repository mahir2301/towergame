using Data;
using System.Collections.Generic;
using UnityEngine;

namespace Managers
{
    public class MapSetupManager : MonoBehaviour
    {
        [Header("Energy Node Settings")]
        [SerializeField] private int nodeCount = 3;
        [SerializeField] private int minDistanceBetweenNodes = 10;

        [Header("Energy Node prefabs")]
        [SerializeField]
        private EnergyType[] energyNodeConfig;

        [Header("References")]
        [SerializeField] private GridManager gridManager;

        private void Start()
        {
            if (energyNodeConfig == null)
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
            var maxAttempts = 100;

            foreach (var node in energyNodeConfig)
            {

                while (placed < nodeCount && attempts < maxAttempts)
                {
                    attempts++;

                    var x = Random.Range(2, gridSize.x - Default.x);
                    var y = Random.Range(2, gridSize.y - Default.y);
                    var newPos = new Vector2Int(x, y);

                    if(IsValidPosition(newPos, placedNodesPos)) {
                        if (gridManager.TryPlaceEnergyNode(newPos, node, out var instance))
                        {
                            placed++;
                            placedNodesPos.Add(newPos);
                            Debug.Log($"Placed energy node at {newPos}");
                        }
                    }

                }

                placed = 0;

                Debug.Log($"Placed {placed} energy nodes after {attempts} attempts");
            }
        }

        private bool IsValidPosition(Vector2Int pos, List<Vector2Int> existing)
        {
            foreach (var existingPos in existing)
            {
                // Chebyshev distance (grid squares distance, not Euclidean)
                var dist = Mathf.Max(Mathf.Abs(pos.x - existingPos.x), Mathf.Abs(pos.y - existingPos.y));
                if (dist < minDistanceBetweenNodes)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
