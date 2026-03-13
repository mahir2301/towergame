using System.Collections.Generic;
using Components;
using Data;
using UnityEngine;
using static UnityEngine.Rendering.STP;

namespace Managers
{
    public class GridManager : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField]
        private Vector2Int gridSize = new(32, 32);

        private readonly Dictionary<Vector2Int, GameObject> occupiedCells = new();

        public Vector2Int GridSize => gridSize;

        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.z));
        }

        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            return new Vector3(gridPos.x + 0.5f, 0.5f, gridPos.y + 0.5f);
        }

        public Vector3 GridToWorld(Vector2Int gridPos, Vector2Int size)
        {
            if (size is { x: <= 1, y: <= 1 })
            {
                return GridToWorld(gridPos);
            }

            var centerX = gridPos.x + size.x * 0.5f;
            var centerZ = gridPos.y + size.y * 0.5f;
            return new Vector3(centerX, 0.5f, centerZ);
        }


        public bool IsValidPosition(Vector2Int gridPos)
        {
            return gridPos.x >= 0 && gridPos.x < gridSize.x
                                  && gridPos.y >= 0 && gridPos.y < gridSize.y;
        }

        public bool IsCellAvailable(Vector2Int gridPos, Vector2Int size)
        {
            for (var x = 0; x < size.x; x++)
            {
                for (var y = 0; y < size.y; y++)
                {
                    var checkPos = new Vector2Int(gridPos.x + x, gridPos.y + y);
                    if (!IsValidPosition(checkPos) || occupiedCells.ContainsKey(checkPos))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool TryPlaceTower(Vector2Int gridPos, TowerConfig config, out GameObject instance)
        {
            instance = null;

            if (config == null || config.towerPrefab == null)
            {
                Debug.LogError("TowerConfig or prefab is null!");
                return false;
            }

            if (!IsCellAvailable(gridPos, config.gridSize))
            {
                return false;
            }

            var worldPos = GridToWorld(gridPos, config.gridSize);
            instance = Instantiate(config.towerPrefab, worldPos, Quaternion.identity);

            for (var x = 0; x < config.gridSize.x; x++)
            {
                for (var y = 0; y < config.gridSize.y; y++)
                {
                    occupiedCells[new Vector2Int(gridPos.x + x, gridPos.y + y)] = instance;
                }
            }

            return true;
        }

        public bool IsCellOccupied(Vector2Int gridPos)
        {
            return occupiedCells.ContainsKey(gridPos);
        }

        public bool TryPlaceEnergyNode(Vector2Int gridPos, EnergyNodeConfig config, out GameObject instance)
        {
            instance = null;

            if (config == null || config.energyNodePrefab == null)
            {
                Debug.LogError("TowerConfig or prefab is null!");
                return false;
            }

            if (!IsCellAvailable(gridPos, config.gridSize))
            {
                return false;
            }

            var worldPos = GridToWorld(gridPos, config.gridSize);
            instance = Instantiate(config.energyNodePrefab, worldPos, Quaternion.identity);

            for (var x = 0; x < config.gridSize.x; x++)
            {
                for (var y = 0; y < config.gridSize.y; y++)
                {
                    occupiedCells[new Vector2Int(gridPos.x + x, gridPos.y + y)] = instance;
                }
            }

            return true;


        }
    }
}