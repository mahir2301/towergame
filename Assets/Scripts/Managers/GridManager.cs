using System.Collections.Generic;
using Data;
using Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        [SerializeField] private Vector2Int gridSize = new(32, 32);

        private readonly Dictionary<Vector2Int, GameObject> occupiedCells = new();
        private readonly HashSet<Vector2Int> waterCells = new();

        public Vector2Int GridSize => gridSize;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool IsWaterCell(Vector2Int gridPos)
        {
            return waterCells.Contains(gridPos);
        }

        public void MarkCellsAsWater(List<Vector2Int> cells)
        {
            foreach (var cell in cells)
                waterCells.Add(cell);
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
                return GridToWorld(gridPos, yOffset);

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
                for (var y = 0; y < size.y; y++)
                    occupiedCells[new Vector2Int(gridPos.x + x, gridPos.y + y)] = obj;
        }

        public bool IsCellAvailable(Vector2Int gridPos, Vector2Int size, bool allowWater = false)
        {
            for (var x = 0; x < size.x; x++)
            {
                for (var y = 0; y < size.y; y++)
                {
                    var checkPos = new Vector2Int(gridPos.x + x, gridPos.y + y);
                    if (!IsValidPosition(checkPos) || occupiedCells.ContainsKey(checkPos))
                        return false;
                    if (!allowWater && waterCells.Contains(checkPos))
                        return false;
                }
            }
            return true;
        }

        public bool TryPlaceEnergyRuntime(Vector2Int gridPos, EnergyType config, int maxCapacity,
            out EnergyRuntime instance)
        {
            instance = null;
            if (config?.Prefab == null) return false;

            var prefab = config.Prefab.GetComponent<EnergyRuntime>();
            if (prefab == null) return false;

            if (!IsCellAvailable(gridPos, Vector2Int.one))
                return false;

            var worldPos = GridToWorld(gridPos);
            instance = Instantiate(prefab, worldPos, Quaternion.identity);
            instance.Initialize(config, maxCapacity, gridPos);
            RegisterOccupiedCells(gridPos, Vector2Int.one, instance.gameObject);

            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                netObj.Spawn();

            return true;
        }

        public bool TryPlaceTowerRuntime(Vector2Int gridPos, TowerType config, out TowerRuntime instance)
        {
            instance = null;
            if (config?.Prefab == null) return false;

            var prefab = config.Prefab.GetComponent<TowerRuntime>();
            if (prefab == null) return false;

            if (!IsCellAvailable(gridPos, config.Size, config.CanBePlacedOnWater))
                return false;

            var worldPos = TowerWorldPos(gridPos, config);
            instance = Instantiate(prefab, worldPos, Quaternion.identity);
            instance.Initialize(config, gridPos);
            RegisterOccupiedCells(gridPos, config.Size, instance.gameObject);

            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                netObj.Spawn();

            return true;
        }

        public static Vector3 TowerWorldPos(Vector2Int gridPos, TowerType config)
        {
            if (Instance == null) return Vector3.zero;
            var size = config != null ? config.Size : Vector2Int.one;
            var offset = config != null ? config.PlacementOffset : Vector3.zero;
            return Instance.GridToWorld(gridPos, size, 0f) + offset;
        }
    }
}
