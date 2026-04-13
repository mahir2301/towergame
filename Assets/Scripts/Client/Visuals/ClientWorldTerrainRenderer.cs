using System.Collections.Generic;
using Game.Shared.Determinism;
using Game.Shared.Grid;
using Game.Shared.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Game.Client.Visuals
{
    public class ClientWorldTerrainRenderer : MonoBehaviour
    {
        [SerializeField] private WorldGenerationState worldGenerationState;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private Material waterMaterial;
        [SerializeField] private GameObject waterParent;

        private const float WaterSurfaceY = 0.06f;

        private GameObject waterMeshObject;
        private int appliedSeed = int.MinValue;

        private void LateUpdate()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
                return;
            if (worldGenerationState == null || gridManager == null)
                return;

            var seed = worldGenerationState.ReplicatedSeed;
            if (seed < 0 || seed == appliedSeed)
                return;

            var random = new System.Random(seed);
            var waterCells = GridWaterGenerator.Generate(
                gridManager.GridSize,
                worldGenerationState.MinDistanceFromEdge,
                worldGenerationState.WaterNoiseScale,
                worldGenerationState.WaterThreshold,
                worldGenerationState.WaterSmoothPasses,
                random);

            gridManager.SetTerrainCells(waterCells);
            RebuildWaterVisuals(waterCells);
            appliedSeed = seed;
        }

        private void RebuildWaterVisuals(HashSet<Vector2Int> waterCells)
        {
            if (waterMeshObject != null)
                Destroy(waterMeshObject);

            if (waterCells.Count == 0 || waterMaterial == null)
                return;

            waterMeshObject = new GameObject("WaterMesh");
            waterMeshObject.transform.position = Vector3.zero;
            waterMeshObject.transform.rotation = Quaternion.identity;
            waterMeshObject.transform.localScale = Vector3.one;
            if (waterParent != null)
                waterMeshObject.transform.SetParent(waterParent.transform, true);

            var meshFilter = waterMeshObject.AddComponent<MeshFilter>();
            var meshRenderer = waterMeshObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = waterMaterial;
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

                var center = GridManager.Instance != null
                    ? GridManager.Instance.GridToWorld(cell, WaterSurfaceY)
                    : new Vector3(cell.x + 0.5f, WaterSurfaceY, cell.y + 0.5f);

                vertices[v + 0] = new Vector3(center.x - 0.5f, WaterSurfaceY, center.z - 0.5f);
                vertices[v + 1] = new Vector3(center.x + 0.5f, WaterSurfaceY, center.z - 0.5f);
                vertices[v + 2] = new Vector3(center.x + 0.5f, WaterSurfaceY, center.z + 0.5f);
                vertices[v + 3] = new Vector3(center.x - 0.5f, WaterSurfaceY, center.z + 0.5f);

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
