using System;
using System.Collections.Generic;
using Data;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class WorldGenerationManager : NetworkBehaviour
    {
        [Header("Terrain")]
        [SerializeField] private int minDistanceFromEdge = 3;
        [SerializeField] private float waterThreshold = 0.62f;
        [SerializeField] private float waterNoiseScale = 0.045f;
        [SerializeField] private int waterSmoothPasses = 2;

        [Header("Energy Sources")]
        [SerializeField] private int nodeCount = 10;
        [SerializeField] private int minDistanceBetweenNodes = 10;
        [SerializeField] private int defaultMaxCapacity = 100;
        [SerializeField] private int maxAttemptsPerNode = 100;
        [SerializeField] private int edgePadding = 2;
        [SerializeField] private EnergyType[] energyNodeConfig;

        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private Material waterMaterial;
        [SerializeField] private GameObject waterParent;

        private const float WaterSurfaceY = 0.01f;

        private int worldSeed;
        private GameObject waterMeshObject;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (gridManager == null)
                return;

            if (IsServer)
            {
                GenerateServerWorld();
            }
        }

        [Rpc(SendTo.NotServer)]
        private void ApplySeedClientRpc(int seed)
        {
            if (gridManager == null)
                return;

            ApplyClientWorld(seed);
        }

        private void GenerateServerWorld()
        {
            worldSeed = CreateServerSeed();
            gridManager.ClearWorldState();
            GenerateTerrainFromSeed(worldSeed);
            SpawnEnergySourcesFromSeed(worldSeed);
            ApplySeedClientRpc(worldSeed);
        }

        private void ApplyClientWorld(int seed)
        {
            worldSeed = seed;
            GenerateTerrainFromSeed(worldSeed);
        }

        private void GenerateTerrainFromSeed(int seed)
        {
            var random = new System.Random(seed);
            var size = gridManager.GridSize;

            var waterCells = GridWaterGenerator.Generate(size, minDistanceFromEdge, waterNoiseScale, waterThreshold,
                waterSmoothPasses, random);

            gridManager.SetTerrainCells(waterCells);
            RebuildWaterVisuals(waterCells);
        }

        private void SpawnEnergySourcesFromSeed(int seed)
        {
            var random = new System.Random(seed);
            var size = gridManager.GridSize;

            GridWaterGenerator.AdvanceRandom(random);

            GridEnergySourceGenerator.Spawn(
                random,
                size,
                energyNodeConfig,
                gridManager,
                nodeCount,
                minDistanceBetweenNodes,
                defaultMaxCapacity,
                maxAttemptsPerNode,
                edgePadding);
        }

        private void RebuildWaterVisuals(HashSet<Vector2Int> waterCells)
        {
            if (waterMeshObject != null)
                Destroy(waterMeshObject);

            if (waterCells.Count == 0)
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

            var mesh = BuildWaterMesh(waterCells);
            meshFilter.sharedMesh = mesh;
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

                var x = cell.x;
                var z = cell.y;
                var center = GridManager.Instance != null
                    ? GridManager.Instance.GridToWorld(new Vector2Int(x, z), WaterSurfaceY)
                    : new Vector3(x + 0.5f, WaterSurfaceY, z + 0.5f);

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

        private int CreateServerSeed()
        {
            unchecked
            {
                var ticks = DateTime.UtcNow.Ticks;
                var hash = (int)(ticks ^ (ticks >> 32));
                return hash ^ GetInstanceID();
            }
        }
    }

    internal static class GridWaterGenerator
    {
        public static void AdvanceRandom(System.Random random)
        {
            random.Next(-100000, 100001);
            random.Next(-100000, 100001);
        }

        public static HashSet<Vector2Int> Generate(Vector2Int gridSize, int minDistanceFromEdge, float noiseScale,
            float threshold, int smoothPasses, System.Random random)
        {
            var offsetX = random.Next(-100000, 100001);
            var offsetY = random.Next(-100000, 100001);
            var water = new HashSet<Vector2Int>();

            for (var x = 0; x < gridSize.x; x++)
            {
                for (var y = 0; y < gridSize.y; y++)
                {
                    if (x < minDistanceFromEdge || y < minDistanceFromEdge ||
                        x >= gridSize.x - minDistanceFromEdge || y >= gridSize.y - minDistanceFromEdge)
                        continue;

                    var value = SampleFbmNoise(x + offsetX, y + offsetY, noiseScale);
                    if (value >= threshold)
                        water.Add(new Vector2Int(x, y));
                }
            }

            for (var i = 0; i < smoothPasses; i++)
                SmoothMask(water, gridSize);

            return water;
        }

        private static float SampleFbmNoise(int x, int y, float scale)
        {
            var nx = x * scale;
            var ny = y * scale;
            var n1 = Mathf.PerlinNoise(nx, ny);
            var n2 = Mathf.PerlinNoise(nx * 2f, ny * 2f);
            var n3 = Mathf.PerlinNoise(nx * 4f, ny * 4f);
            return n1 * 0.6f + n2 * 0.3f + n3 * 0.1f;
        }

        private static void SmoothMask(HashSet<Vector2Int> water, Vector2Int gridSize)
        {
            var next = new HashSet<Vector2Int>(water.Count);

            for (var x = 0; x < gridSize.x; x++)
            {
                for (var y = 0; y < gridSize.y; y++)
                {
                    var cell = new Vector2Int(x, y);
                    var neighbors = CountNeighbors(water, cell);
                    var isWater = water.Contains(cell);

                    if (isWater ? neighbors >= 3 : neighbors >= 5)
                        next.Add(cell);
                }
            }

            water.Clear();
            foreach (var cell in next)
                water.Add(cell);
        }

        private static int CountNeighbors(HashSet<Vector2Int> water, Vector2Int cell)
        {
            var count = 0;
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    if (water.Contains(new Vector2Int(cell.x + dx, cell.y + dy)))
                        count++;
                }
            }

            return count;
        }
    }

    internal static class GridEnergySourceGenerator
    {
        public static void Spawn(
            System.Random random,
            Vector2Int gridSize,
            EnergyType[] energyTypes,
            GridManager gridManager,
            int nodeCount,
            int minDistanceBetweenNodes,
            int defaultMaxCapacity,
            int maxAttemptsPerNode,
            int edgePadding)
        {
            if (energyTypes == null || energyTypes.Length == 0)
                return;

            var placed = new List<Vector2Int>();

            foreach (var energyType in energyTypes)
            {
                if (energyType == null)
                    continue;

                var spawnedForType = 0;
                var attempts = 0;
                while (spawnedForType < nodeCount && attempts < maxAttemptsPerNode)
                {
                    attempts++;

                    var x = random.Next(edgePadding, Math.Max(edgePadding + 1, gridSize.x - edgePadding));
                    var y = random.Next(edgePadding, Math.Max(edgePadding + 1, gridSize.y - edgePadding));
                    var pos = new Vector2Int(x, y);

                    if (!IsFarEnough(pos, placed, minDistanceBetweenNodes))
                        continue;
                    if (!gridManager.TryPlaceEnergyRuntime(pos, energyType, defaultMaxCapacity, out _))
                        continue;

                    placed.Add(pos);
                    spawnedForType++;
                }
            }
        }

        private static bool IsFarEnough(Vector2Int pos, List<Vector2Int> existing, int minDistance)
        {
            for (var i = 0; i < existing.Count; i++)
            {
                var other = existing[i];
                var dist = Mathf.Max(Mathf.Abs(pos.x - other.x), Mathf.Abs(pos.y - other.y));
                if (dist < minDistance)
                    return false;
            }

            return true;
        }
    }
}
