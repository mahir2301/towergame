using System.Collections.Generic;
using Data;
using Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public enum GridTerrainType : byte
    {
        Empty = 0,
        Water = 1,
    }

    public enum GridObjectKind : byte
    {
        Unknown = 0,
        EnergySource = 1,
        Tower = 2,
    }

    public sealed class GridObjectRecord
    {
        public int Id;
        public GridObjectKind Kind;
        public Vector2Int GridPosition;
        public Vector2Int Size;
        public string ConfigId;
        public ulong RuntimeNetId;
        public GameObject RuntimeObject;
    }

    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        [SerializeField] private Vector2Int gridSize = new(32, 32);

        private readonly Dictionary<Vector2Int, int> occupiedCells = new();
        private readonly Dictionary<int, GridObjectRecord> gridObjects = new();
        private GridTerrainType[] terrainMap;
        private int nextObjectId = 1;

        public Vector2Int GridSize => gridSize;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureTerrainMap();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void ClearWorldState()
        {
            EnsureTerrainMap();
            for (var i = 0; i < terrainMap.Length; i++)
                terrainMap[i] = GridTerrainType.Empty;

            occupiedCells.Clear();
            gridObjects.Clear();
            nextObjectId = 1;
        }

        public void SetTerrainCells(IReadOnlyCollection<Vector2Int> waterCells)
        {
            EnsureTerrainMap();
            for (var i = 0; i < terrainMap.Length; i++)
                terrainMap[i] = GridTerrainType.Empty;

            foreach (var cell in waterCells)
            {
                if (!IsValidPosition(cell))
                    continue;
                terrainMap[ToIndex(cell)] = GridTerrainType.Water;
            }
        }

        public GridTerrainType GetTerrainType(Vector2Int gridPos)
        {
            if (!IsValidPosition(gridPos))
                return GridTerrainType.Empty;
            EnsureTerrainMap();
            return terrainMap[ToIndex(gridPos)];
        }

        public bool IsWaterCell(Vector2Int gridPos)
        {
            return GetTerrainType(gridPos) == GridTerrainType.Water;
        }

        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.z));
        }

        public Vector3 GridToWorld(Vector2Int gridPos, float yOffset = 0.5f)
        {
            return new Vector3(gridPos.x + 0.5f, yOffset, gridPos.y + 0.5f);
        }

        public Vector3 GridToWorld(Vector2Int gridPos, Vector2Int size, float yOffset = 0f)
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

        public bool IsCellAvailable(Vector2Int gridPos, Vector2Int size, bool allowWater = false)
        {
            for (var x = 0; x < size.x; x++)
            {
                for (var y = 0; y < size.y; y++)
                {
                    var checkPos = new Vector2Int(gridPos.x + x, gridPos.y + y);
                    if (!IsValidPosition(checkPos) || occupiedCells.ContainsKey(checkPos))
                        return false;
                    if (!allowWater && IsWaterCell(checkPos))
                        return false;
                }
            }

            return true;
        }

        public bool TryPlaceEnergyRuntime(Vector2Int gridPos, EnergyType config, int maxCapacity, out EnergyRuntime instance)
        {
            instance = null;
            if (config?.Prefab == null)
                return false;

            var prefab = config.Prefab.GetComponent<EnergyRuntime>();
            if (prefab == null)
                return false;

            if (!TryRegisterGridObject(GridObjectKind.EnergySource, gridPos, Vector2Int.one, false, config.Id, out var record))
                return false;

            instance = Instantiate(prefab, GridToWorld(gridPos), Quaternion.identity);
            instance.Initialize(config, maxCapacity, gridPos);
            BindRuntimeObject(record.Id, instance.gameObject);

            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                netObj.Spawn();
                BindRuntimeNetId(record.Id, netObj.NetworkObjectId);
            }

            return true;
        }

        public bool TryPlaceTowerRuntime(Vector2Int gridPos, TowerType config, out TowerRuntime instance)
        {
            instance = null;
            if (config?.Prefab == null)
                return false;

            var prefab = config.Prefab.GetComponent<TowerRuntime>();
            if (prefab == null)
                return false;

            if (!TryRegisterGridObject(GridObjectKind.Tower, gridPos, config.Size, config.CanBePlacedOnWater, config.Id,
                    out var record))
                return false;

            instance = Instantiate(prefab, TowerWorldPos(gridPos, config), Quaternion.identity);
            instance.Initialize(config, gridPos);
            BindRuntimeObject(record.Id, instance.gameObject);

            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                netObj.Spawn();
                BindRuntimeNetId(record.Id, netObj.NetworkObjectId);
            }

            return true;
        }

        public void RegisterOccupiedCells(Vector2Int gridPos, Vector2Int size, GameObject obj)
        {
            TryRegisterGridObject(GridObjectKind.Unknown, gridPos, size, true, string.Empty, out _);
        }

        public static Vector3 TowerWorldPos(Vector2Int gridPos, TowerType config)
        {
            if (Instance == null)
                return Vector3.zero;

            var size = config != null ? config.Size : Vector2Int.one;
            var offset = config != null ? config.PlacementOffset : Vector3.zero;
            return Instance.GridToWorld(gridPos, size, 0f) + offset;
        }

        private bool TryRegisterGridObject(GridObjectKind kind, Vector2Int gridPos, Vector2Int size, bool allowWater,
            string configId, out GridObjectRecord record)
        {
            record = null;
            if (!IsCellAvailable(gridPos, size, allowWater))
                return false;

            record = new GridObjectRecord
            {
                Id = nextObjectId++,
                Kind = kind,
                GridPosition = gridPos,
                Size = size,
                ConfigId = configId,
                RuntimeNetId = ulong.MaxValue,
                RuntimeObject = null,
            };

            gridObjects[record.Id] = record;
            for (var x = 0; x < size.x; x++)
            {
                for (var y = 0; y < size.y; y++)
                {
                    occupiedCells[new Vector2Int(gridPos.x + x, gridPos.y + y)] = record.Id;
                }
            }

            return true;
        }

        private void BindRuntimeObject(int objectId, GameObject obj)
        {
            if (gridObjects.TryGetValue(objectId, out var record))
                record.RuntimeObject = obj;
        }

        private void BindRuntimeNetId(int objectId, ulong netId)
        {
            if (gridObjects.TryGetValue(objectId, out var record))
                record.RuntimeNetId = netId;
        }

        private void EnsureTerrainMap()
        {
            var expectedLength = Mathf.Max(1, gridSize.x * gridSize.y);
            if (terrainMap == null || terrainMap.Length != expectedLength)
                terrainMap = new GridTerrainType[expectedLength];
        }

        private int ToIndex(Vector2Int gridPos)
        {
            return gridPos.y * gridSize.x + gridPos.x;
        }
    }
}
