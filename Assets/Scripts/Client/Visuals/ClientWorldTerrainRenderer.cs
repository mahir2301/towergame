using System.Collections.Generic;
using Shared.Determinism;
using Shared.Grid;
using Shared.Runtime;
using Shared.Utilities;
using Unity.Netcode;
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
        private bool subscribedToSeed;

        private void Start()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsClient)
                return;

            if (!RuntimeBootstrap.IsReady)
                return;

            if (!EnsureStateReference())
                return;

            SubscribeToSeedChange();
            TryApplyTerrainFromState();
        }

        private void Update()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsClient)
                return;

            if (!RuntimeBootstrap.IsReady)
                return;

            if (!EnsureStateReference())
                return;

            TryApplyTerrainFromState();
        }

        private void OnDestroy()
        {
            UnsubscribeFromSeedChange();
        }

        private void OnSeedChanged(int previousValue, int newValue)
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

            SubscribeToSeedChange();
            return true;
        }

        private void SubscribeToSeedChange()
        {
            if (subscribedToSeed || worldGenerationState == null)
                return;

            worldGenerationState.Seed.OnValueChanged += OnSeedChanged;
            subscribedToSeed = true;
        }

        private void UnsubscribeFromSeedChange()
        {
            if (!subscribedToSeed || worldGenerationState == null)
                return;

            worldGenerationState.Seed.OnValueChanged -= OnSeedChanged;
            subscribedToSeed = false;
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
                && waterSmoothPasses == lastAppliedWaterSmoothPasses)
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

            var waterCells = GridWaterGenerator.Generate(
                gridManager.GridSize,
                minDistanceFromEdge,
                waterNoiseScale,
                waterThreshold,
                waterSmoothPasses,
                new System.Random(newSeed));

            gridManager.SetTerrainCells(waterCells);
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

            if (waterCells.Count == 0 || waterMaterial == null)
                return;

            waterMeshObject = new GameObject("WaterMesh");

            var meshFilter = waterMeshObject.AddComponent<MeshFilter>();
            var meshRenderer = waterMeshObject.AddComponent<MeshRenderer>();

            var waterMat = new Material(waterMaterial);
            waterMat.renderQueue = 2001;
            meshRenderer.sharedMaterial = waterMat;

            meshFilter.sharedMesh = BuildWaterMesh(waterCells);
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
