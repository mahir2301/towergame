using System.Collections.Generic;
using Shared.Data;
using Shared.Utilities;
using UnityEngine;

namespace Shared.Grid
{
    public sealed class GridObjectRecord
    {
        public int Id;
        public Vector2Int GridPosition;
        public Vector2Int Size;
        public string TypeId;
        public ulong RuntimeNetId;
        public GameObject RuntimeObject;
        public List<TagType> Tags;
    }

    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        [SerializeField] private Vector2Int gridSize = new(32, 32);

        private readonly Dictionary<Vector2Int, int> occupiedCells = new();
        private readonly Dictionary<int, GridObjectRecord> gridObjects = new();
        [SerializeField] private TileType defaultTileType;

        private string[] tileTypeIds;
        private int nextObjectId = 1;

        public Vector2Int GridSize => gridSize;

        private void Awake()
        {
            if (!SingletonUtility.TryAssign(Instance, this, value => Instance = value))
                return;

            EnsureTerrainMap();
        }

        private void OnDestroy()
        {
            SingletonUtility.ClearIfCurrent(Instance, this, () => Instance = null);
        }

        public void ClearWorldState()
        {
            EnsureTerrainMap();
            var defaultId = defaultTileType != null ? defaultTileType.Id : string.Empty;
            for (var i = 0; i < tileTypeIds.Length; i++)
                tileTypeIds[i] = defaultId;

            occupiedCells.Clear();
            gridObjects.Clear();
            nextObjectId = 1;
        }

        public void SetTileMap(TileType[] flattenedTiles)
        {
            EnsureTerrainMap();
            if (flattenedTiles == null)
                return;

            var count = Mathf.Min(tileTypeIds.Length, flattenedTiles.Length);
            for (var i = 0; i < count; i++)
            {
                tileTypeIds[i] = flattenedTiles[i] != null ? flattenedTiles[i].Id : string.Empty;
            }

            for (var i = count; i < tileTypeIds.Length; i++)
                tileTypeIds[i] = string.Empty;
        }

        public TileType GetTileTypeAt(Vector2Int gridPos)
        {
            if (!IsValidPosition(gridPos))
                return null;

            EnsureTerrainMap();
            return GameRegistry.Instance?.GetTileType(tileTypeIds[ToIndex(gridPos)]);
        }

        public bool CellHasTileTag(Vector2Int gridPos, TagType tag)
        {
            var tile = GetTileTypeAt(gridPos);
            return tile != null && tile.HasTag(tag);
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

        public bool IsCellAvailable(Vector2Int gridPos, Vector2Int size, IReadOnlyList<TileType> allowedTileTypes)
        {
            for (var x = 0; x < size.x; x++)
            {
                for (var y = 0; y < size.y; y++)
                {
                    var checkPos = new Vector2Int(gridPos.x + x, gridPos.y + y);
                    if (!IsValidPosition(checkPos) || occupiedCells.ContainsKey(checkPos))
                        return false;

                    var tileType = GetTileTypeAt(checkPos);
                    if (!IsTileAllowed(tileType, allowedTileTypes))
                        return false;
                }
            }

            return true;
        }

        public void RegisterOccupiedCells(Vector2Int gridPos, Vector2Int size, GameObject obj)
        {
            TryRegisterOccupancy(string.Empty, gridPos, size, out _);
        }

        public void UnregisterOccupiedCells(Vector2Int gridPos, Vector2Int size)
        {
            for (var x = 0; x < size.x; x++)
            {
                for (var y = 0; y < size.y; y++)
                {
                    occupiedCells.Remove(new Vector2Int(gridPos.x + x, gridPos.y + y));
                }
            }
        }

        public bool TryRegisterOccupancy(string typeId, Vector2Int gridPos, Vector2Int size,
            out GridObjectRecord record)
        {
            record = null;
            if (!IsCellAvailable(gridPos, size, null))
                return false;

            record = new GridObjectRecord
            {
                Id = nextObjectId++,
                GridPosition = gridPos,
                Size = size,
                TypeId = typeId,
                RuntimeNetId = ulong.MaxValue,
                RuntimeObject = null,
                Tags = null,
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

        public void BindRuntimeObject(int objectId, GameObject obj)
        {
            if (gridObjects.TryGetValue(objectId, out var record))
                record.RuntimeObject = obj;
        }

        public void BindRuntimeNetId(int objectId, ulong netId)
        {
            if (gridObjects.TryGetValue(objectId, out var record))
                record.RuntimeNetId = netId;
        }

        public static Vector3 PlaceableWorldPos(Vector2Int gridPos, PlaceableType config)
        {
            if (Instance == null)
                return Vector3.zero;

            var size = config != null ? config.Size : Vector2Int.one;
            var offset = config != null ? config.PlacementOffset : Vector3.zero;
            return Instance.GridToWorld(gridPos, size, 0f) + offset;
        }

        private static bool IsTileAllowed(TileType tileType, IReadOnlyList<TileType> allowedTileTypes)
        {
            if (allowedTileTypes == null || allowedTileTypes.Count == 0)
                return true;

            if (tileType == null)
                return false;

            for (var i = 0; i < allowedTileTypes.Count; i++)
            {
                if (allowedTileTypes[i] == tileType)
                    return true;
            }

            return false;
        }

        private void EnsureTerrainMap()
        {
            var expectedLength = Mathf.Max(1, gridSize.x * gridSize.y);
            if (tileTypeIds == null || tileTypeIds.Length != expectedLength)
                tileTypeIds = new string[expectedLength];
        }

        private int ToIndex(Vector2Int gridPos)
        {
            return gridPos.y * gridSize.x + gridPos.x;
        }
    }
}
