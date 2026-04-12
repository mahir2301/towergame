using Data;
using Runtime;
using System.Collections.Generic;
using UnityEngine;

namespace Managers
{
    public class MapSetupManager : MonoBehaviour
    {
        [SerializeField] private int nodeCount;
        [SerializeField] private int minDistanceBetweenNodes;
        [SerializeField] private int defaultMaxCapacity;
        [SerializeField] private int maxAttemptsPerNode = 100;
        [SerializeField] private int edgePadding = 2;
        [SerializeField] private EnergyType[] energyNodeConfig;
        [SerializeField] private GridManager gridManager;

        private void Start()
        {
            if (energyNodeConfig == null || energyNodeConfig.Length == 0)
                return;

            SpawnEnergyNodes();
        }

        private void SpawnEnergyNodes()
        {
            var gridSize = gridManager.GridSize;
            var placedNodesPos = new List<Vector2Int>();

            foreach (var node in energyNodeConfig)
            {
                var placed = 0;
                var attempts = 0;

                while (placed < nodeCount && attempts < maxAttemptsPerNode)
                {
                    attempts++;

                    var x = Random.Range(edgePadding, gridSize.x - edgePadding);
                    var y = Random.Range(edgePadding, gridSize.y - edgePadding);
                    var newPos = new Vector2Int(x, y);

                    if (!IsValidPosition(newPos, placedNodesPos)
                        || gridManager.IsWaterCell(newPos)
                        || !gridManager.TryPlaceEnergyRuntime(newPos, node, defaultMaxCapacity, out _))
                        continue;

                    placed++;
                    placedNodesPos.Add(newPos);
                }

                Debug.Log($"[MapSetup] Placed {placed}/{nodeCount} {node.DisplayName} nodes in {attempts} attempts");
            }
        }

        private bool IsValidPosition(Vector2Int pos, List<Vector2Int> existing)
        {
            foreach (var existingPos in existing)
            {
                var dist = Mathf.Max(Mathf.Abs(pos.x - existingPos.x), Mathf.Abs(pos.y - existingPos.y));
                if (dist < minDistanceBetweenNodes)
                    return false;
            }
            return true;
        }
    }
}
