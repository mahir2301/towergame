using System.Collections.Generic;
using System.Linq;
using Shared.Data;
using Shared.Determinism;
using Shared.Grid;
using Shared.Runtime;
using Shared.Utilities;
using UnityEngine;

namespace Client.Visuals
{
    public class ClientWorldTerrainRenderer : MonoBehaviour
    {
        [SerializeField] private WorldGenerationState worldGenerationState;
        [SerializeField] private Material waterMaterial;

        private const float WaterSurfaceY = 0.06f;

        private GameObject waterMeshObject;
        private int lastAppliedSeed = -1;
        private int lastAppliedMinDistanceFromEdge = int.MinValue;
        private float lastAppliedWaterThreshold = float.NaN;
        private float lastAppliedWaterNoiseScale = float.NaN;
        private int lastAppliedWaterSmoothPasses = int.MinValue;
        private readonly SubscriptionGroup stateSubscriptions = new();

        private void OnEnable()
        {
            InvalidateCachedState();
        }

        private void Start()
        {
            if (!RuntimeNet.ShouldRunNetworkedClientSystems())
                return;

            if (!EnsureStateReference())
            {
                RuntimeLog.Water.Error(RuntimeLog.Code.WaterMissingState,
                    "ClientWorldTerrainRenderer requires a WorldGenerationState reference.");
                enabled = false;
                return;
            }

            SubscribeToStateChanges();
            TryApplyTerrainFromState();
        }

        private void OnDestroy()
        {
            stateSubscriptions.UnbindAll();

            if (waterMeshObject != null)
                Destroy(waterMeshObject);
        }

        private void OnWorldStateChanged()
        {
            TryApplyTerrainFromState();
        }

        private bool EnsureStateReference()
        {
            if (worldGenerationState != null)
                return true;

            worldGenerationState = FindFirstObjectByType<WorldGenerationState>();
            if (worldGenerationState == null)
                return false;

            return true;
        }

        private void SubscribeToStateChanges()
        {
            if (worldGenerationState == null)
                return;

            stateSubscriptions.UnbindAll();
            stateSubscriptions.Add(
                () => worldGenerationState.SubscribeStateChanged(OnWorldStateChanged, replayCurrentState: true),
                () => worldGenerationState.UnsubscribeStateChanged(OnWorldStateChanged));
        }

        private void TryApplyTerrainFromState()
        {
            if (worldGenerationState == null || !worldGenerationState.IsSpawned)
                return;

            var newSeed = worldGenerationState.ReplicatedSeed;
            if (newSeed < 0)
                return;

            var minDistanceFromEdge = worldGenerationState.MinDistanceFromEdge;
            var waterThreshold = worldGenerationState.WaterThreshold;
            var waterNoiseScale = worldGenerationState.WaterNoiseScale;
            var waterSmoothPasses = worldGenerationState.WaterSmoothPasses;

            if (newSeed == lastAppliedSeed
                && minDistanceFromEdge == lastAppliedMinDistanceFromEdge
                && Mathf.Approximately(waterThreshold, lastAppliedWaterThreshold)
                && Mathf.Approximately(waterNoiseScale, lastAppliedWaterNoiseScale)
                && waterSmoothPasses == lastAppliedWaterSmoothPasses
                && HasWaterVisual())
            {
                return;
            }

            var gridManager = GridManager.Instance;
            if (gridManager == null)
            {
                RuntimeLog.Water.Error(RuntimeLog.Code.WaterMissingGrid,
                    "Cannot rebuild terrain because GridManager.Instance is missing.");
                return;
            }

            var waterCells = new HashSet<Vector2Int>();
            var tileTypeMap = worldGenerationState.GetTileTypeIdMap();
            var expectedCellCount = gridManager.GridSize.x * gridManager.GridSize.y;
            if (tileTypeMap.Length == expectedCellCount)
            {
                var flatTiles = new TileType[expectedCellCount];
                var waterTag = GameRegistry.Instance?.Tags.FirstOrDefault(t => t != null && t.Id == "water");
                for (var index = 0; index < tileTypeMap.Length; index++)
                {
                    var tile = GameRegistry.Instance?.GetTileType(tileTypeMap[index]);
                    flatTiles[index] = tile;

                    if (waterTag != null && tile != null && tile.HasTag(waterTag))
                    {
                        var x = index % gridManager.GridSize.x;
                        var y = index / gridManager.GridSize.x;
                        waterCells.Add(new Vector2Int(x, y));
                    }
                }

                gridManager.SetTileMap(flatTiles);
            }
            else
            {
                waterCells = GridWaterGenerator.Generate(
                    gridManager.GridSize,
                    minDistanceFromEdge,
                    waterNoiseScale,
                    waterThreshold,
                    waterSmoothPasses,
                    new System.Random(newSeed),
                    new Vector2Int(gridManager.GridSize.x / 2, gridManager.GridSize.y / 2),
                    NexusRuntime.ExclusionZone);

                var registry = GameRegistry.Instance;
                var waterTag = registry?.Tags.FirstOrDefault(t => t != null && t.Id == "water");
                TileType waterTile = null;
                TileType defaultTile = null;
                if (registry != null)
                {
                    var tiles = registry.TileTypes;
                    for (var i = 0; i < tiles.Count; i++)
                    {
                        var tile = tiles[i];
                        if (tile == null)
                            continue;

                        if (defaultTile == null)
                            defaultTile = tile;

                        if (waterTag != null && tile.HasTag(waterTag))
                            waterTile = tile;
                    }
                }

                var flatTiles = new TileType[expectedCellCount];
                for (var index = 0; index < expectedCellCount; index++)
                {
                    var x = index % gridManager.GridSize.x;
                    var y = index / gridManager.GridSize.x;
                    var pos = new Vector2Int(x, y);

                    if (waterCells.Contains(pos) && waterTile != null)
                        flatTiles[index] = waterTile;
                    else
                        flatTiles[index] = defaultTile;
                }

                gridManager.SetTileMap(flatTiles);
            }

            RebuildWaterVisuals(waterCells);

            lastAppliedSeed = newSeed;
            lastAppliedMinDistanceFromEdge = minDistanceFromEdge;
            lastAppliedWaterThreshold = waterThreshold;
            lastAppliedWaterNoiseScale = waterNoiseScale;
            lastAppliedWaterSmoothPasses = waterSmoothPasses;

            RuntimeLog.Water.Info(RuntimeLog.Code.WaterRebuilt,
                $"Rebuilt water mesh for seed={newSeed} cells={waterCells.Count}.");
        }

        private void RebuildWaterVisuals(HashSet<Vector2Int> waterCells)
        {
            if (waterMeshObject != null)
                Destroy(waterMeshObject);

            if (waterCells.Count == 0)
                return;

            if (waterMaterial == null)
            {
                RuntimeLog.Water.Error(RuntimeLog.Code.WaterMissingMaterial,
                    "Cannot rebuild water mesh because waterMaterial is not assigned.");
                return;
            }

            waterMeshObject = new GameObject("WaterMesh");
            waterMeshObject.transform.SetParent(transform, false);

            var meshFilter = waterMeshObject.AddComponent<MeshFilter>();
            var meshRenderer = waterMeshObject.AddComponent<MeshRenderer>();

            var waterMat = new Material(waterMaterial);
            waterMat.SetInt("_Cull", 0);
            waterMat.renderQueue = 2001;
            meshRenderer.sharedMaterial = waterMat;

            meshFilter.sharedMesh = BuildWaterMesh(waterCells);
        }

        private bool HasWaterVisual()
        {
            if (waterMeshObject == null)
                return false;

            var meshFilter = waterMeshObject.GetComponent<MeshFilter>();
            var meshRenderer = waterMeshObject.GetComponent<MeshRenderer>();
            return meshFilter != null && meshRenderer != null && meshFilter.sharedMesh != null && meshRenderer.sharedMaterial != null;
        }

        private void InvalidateCachedState()
        {
            lastAppliedSeed = -1;
            lastAppliedMinDistanceFromEdge = int.MinValue;
            lastAppliedWaterThreshold = float.NaN;
            lastAppliedWaterNoiseScale = float.NaN;
            lastAppliedWaterSmoothPasses = int.MinValue;
        }

        public bool HasRequiredReferences(out string issue)
        {
            return ReferenceValidator.Validate(out issue,
                (worldGenerationState, nameof(worldGenerationState)),
                (waterMaterial, nameof(waterMaterial)));
        }

        private static Mesh BuildWaterMesh(HashSet<Vector2Int> waterCells)
        {
            var quadCount = waterCells.Count;
            var vertices = new Vector3[quadCount * 4];
            var triangles = new int[quadCount * 6];
            var uvs = new Vector2[quadCount * 4];

            var i = 0;
            foreach (var cell in waterCells)
            {
                var v = i * 4;
                var t = i * 6;

                vertices[v + 0] = new Vector3(cell.x, WaterSurfaceY, cell.y);
                vertices[v + 1] = new Vector3(cell.x + 1, WaterSurfaceY, cell.y);
                vertices[v + 2] = new Vector3(cell.x + 1, WaterSurfaceY, cell.y + 1);
                vertices[v + 3] = new Vector3(cell.x, WaterSurfaceY, cell.y + 1);

                uvs[v + 0] = new Vector2(0, 0);
                uvs[v + 1] = new Vector2(1, 0);
                uvs[v + 2] = new Vector2(1, 1);
                uvs[v + 3] = new Vector2(0, 1);

                triangles[t + 0] = v + 0;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 0;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;

                i++;
            }

            var mesh = new Mesh { name = "GeneratedWater" };
            mesh.indexFormat = quadCount * 4 > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
