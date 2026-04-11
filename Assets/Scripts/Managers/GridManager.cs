using System.Collections.Generic;
using Data;
using Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class GridManager : MonoBehaviour
    {
        [SerializeField]
        private Vector2Int gridSize = new(32, 32);

        private readonly Dictionary<Vector2Int, GameObject> occupiedCells = new();
        private readonly HashSet<Vector2Int> waterCells = new();

        public Vector2Int GridSize => gridSize;

        public bool IsWaterCell(Vector2Int gridPos)
        {
            return waterCells.Contains(gridPos);
        }

        public void MarkCellsAsWater(List<Vector2Int> cells)
        {
            foreach (var cell in cells)
            {
                waterCells.Add(cell);
            }
        }

        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.z));
        }

        public Vector3 GridToWorld(Vector2Int gridPos, float yOffset = 0.5f)
        {
            return new Vector3(gridPos.x + 0.5f, yOffset, gridPos.y + 0.5f);
        }

        public Vector3 GridToWorld(Vector2Int gridPos, Vector2Int size, float yOffset = 0.5f)
        {
            if (size is { x: <= 1, y: <= 1 })
            {
                return GridToWorld(gridPos, yOffset);
            }

            var centerX = gridPos.x + size.x * 0.5f;
            var centerZ = gridPos.y + size.y * 0.5f;
            return new Vector3(centerX, yOffset, centerZ);
        }

        public bool IsValidPosition(Vector2Int gridPos)
        {
            return gridPos.x >= 0 && gridPos.x < gridSize.x
                                  && gridPos.y >= 0 && gridPos.y < gridSize.y;
        }

        public void RegisterOccupiedCells(Vector2Int gridPos, Vector2Int size, GameObject obj)
        {
            for (var x = 0; x < size.x; x++)
            {
                for (var y = 0; y < size.y; y++)
                {
                    occupiedCells[new Vector2Int(gridPos.x + x, gridPos.y + y)] = obj;
                }
            }
        }

        public bool IsCellAvailable(Vector2Int gridPos, Vector2Int size, bool allowWater = false)
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
                    if (!allowWater && waterCells.Contains(checkPos))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool TryPlaceElement<T>(Vector2Int gridPos, T prefab, out T instance) where T : MonoBehaviour, IPlaceable
        {
            instance = null;

            if (prefab == null)
            {
                Debug.LogError($"Prefab is null for type {typeof(T).Name}!");
                return false;
            }

            Vector2Int size = prefab.Size;

            if (!IsCellAvailable(gridPos, size, prefab.CanBePlacedOnWater))
            {
                return false;
            }

            var worldPos = GridToWorld(gridPos, size);
            var spawnedInstance = Instantiate(prefab, worldPos, Quaternion.identity);
            spawnedInstance.Initialize(gridPos);

            var networkObject = spawnedInstance.GetComponent<NetworkObject>();
            if (networkObject != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                networkObject.Spawn();
            }

            for (var x = 0; x < size.x; x++)
            {
                for (var y = 0; y < size.y; y++)
                {
                    occupiedCells[new Vector2Int(gridPos.x + x, gridPos.y + y)] = spawnedInstance.gameObject;
                }
            }

            instance = spawnedInstance;
            return true;
        }

        public bool TryPlaceEnergyRuntime(Vector2Int gridPos, EnergyType config, int maxCapacity,
            out EnergyRuntime instance)
        {
            instance = null;

            if (config?.Prefab == null)
            {
                Debug.LogError("EnergyType or prefab is null!");
                return false;
            }

            var energyPrefab = config.Prefab.GetComponent<EnergyRuntime>();
            if (energyPrefab == null)
            {
                Debug.LogError("Energy prefab does not have EnergyRuntime component!");
                return false;
            }

            if (!TryPlaceElement(gridPos, energyPrefab, out instance))
            {
                return false;
            }

            instance.Initialize(config, maxCapacity, gridPos);
            return true;
        }

        public bool TryPlaceTowerRuntime(Vector2Int gridPos, TowerType config, out TowerRuntime instance)
        {
            instance = null;

            if (config?.Prefab == null)
            {
                Debug.LogError("TowerType or prefab is null!");
                return false;
            }

            var towerPrefab = config.Prefab.GetComponent<TowerRuntime>();
            if (towerPrefab == null)
            {
                Debug.LogError("Tower prefab does not have TowerRuntime component!");
                return false;
            }

            var basePos = GridToWorld(gridPos, config.Size, 0f);
            var worldPos = basePos + towerPrefab.PlacementOffset;

            if (!TryPlaceElement(gridPos, towerPrefab, out instance))
            {
                return false;
            }

            instance.Initialize(config.Id, gridPos, config.Size, worldPos);
            return true;
        }
    }
}