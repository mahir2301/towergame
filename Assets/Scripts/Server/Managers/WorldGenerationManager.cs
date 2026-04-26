using System;
using System.Collections.Generic;
using System.Linq;
using Shared.Data;
using Shared.Determinism;
using Shared.Grid;
using Shared.Runtime;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace Server.Managers
{
    public class WorldGenerationManager : NetworkBehaviour
    {
        [Header("Nexus")]
        [SerializeField] private int nexusExclusionZone = 8;

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
        [SerializeField] private PlaceableType nexusPlaceableType;
        [SerializeField] private PlaceableType[] energyPlaceables;

        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private WorldGenerationState worldGenerationState;
        [SerializeField] private ServerSpawnManager serverSpawnManager;

        private int generatedSeed = -1;
        private bool statePublished;
        private Vector2Int nexusCenter;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!RuntimeNet.IsServer)
                return;

            if (!HasRequiredReferences(out var issue))
            {
                RuntimeLog.WorldGen.Error(RuntimeLog.Code.WorldGenTerrainFailed,
                    $"Cannot generate world: {issue}");
                return;
            }

            if (worldGenerationState.IsSpawned)
                HandleWorldStateReady();
            else
                worldGenerationState.ServerSpawned += HandleWorldStateReady;
        }

        public override void OnNetworkDespawn()
        {
            if (worldGenerationState != null)
                worldGenerationState.ServerSpawned -= HandleWorldStateReady;

            base.OnNetworkDespawn();
        }

        private void HandleWorldStateReady()
        {
            worldGenerationState.ServerSpawned -= HandleWorldStateReady;

            if (statePublished || generatedSeed >= 0)
                return;

            var seed = CreateServerSeed();
            gridManager.ClearWorldState();

            RuntimeLog.WorldGen.Info(RuntimeLog.Code.WorldGenStart,
                $"Starting world generation with seed {seed}.");

            try
            {
                SpawnNexus();
                RuntimeLog.WorldGen.Info(RuntimeLog.Code.WorldGenNexusDone,
                    "Nexus spawn complete.");
            }
            catch (Exception ex)
            {
                RuntimeLog.WorldGen.Error(RuntimeLog.Code.WorldGenNexusFailed,
                    $"Nexus spawn failed: {ex}");
                return;
            }

            try
            {
                GenerateTerrain(seed);
                RuntimeLog.WorldGen.Info(RuntimeLog.Code.WorldGenTerrainDone,
                    "Terrain generation complete.");
            }
            catch (Exception ex)
            {
                RuntimeLog.WorldGen.Error(RuntimeLog.Code.WorldGenTerrainFailed,
                    $"Terrain generation failed: {ex}");
                return;
            }

            try
            {
                SpawnEnergySources(seed);
                RuntimeLog.WorldGen.Info(RuntimeLog.Code.WorldGenEnergyDone,
                    "Energy source spawn complete.");
            }
            catch (Exception ex)
            {
                RuntimeLog.WorldGen.Error(RuntimeLog.Code.WorldGenEnergyFailed,
                    $"Energy source spawn failed: {ex}");
                return;
            }

            generatedSeed = seed;
            PublishWorldState();
        }

        private void SpawnNexus()
        {
            var registry = GameRegistry.Instance;
            if (registry == null)
                throw new InvalidOperationException("GameRegistry is missing at Resources/GameRegistry.");

            var nexusType = ResolveNexusPlaceableType(registry);
            if (nexusType == null)
                throw new InvalidOperationException("No valid nexus PlaceableType found in GameRegistry.");

            var size = gridManager.GridSize;
            nexusCenter = new Vector2Int(size.x / 2, size.y / 2);
            var halfSize = NexusRuntime.NexusSize / 2;
            var gridPos = new Vector2Int(nexusCenter.x - halfSize, nexusCenter.y - halfSize);
            var nexusRuntime = nexusType.Prefab != null ? nexusType.Prefab.GetComponent<NexusRuntime>() : null;

            RuntimeLog.WorldGen.Info(RuntimeLog.Code.WorldGenNexusDone,
                $"Spawning nexus: gridSize={size}, center={nexusCenter}, gridPos={gridPos}, " +
                $"placeableTypeId={nexusType.Id}, prefab={nexusType.Prefab != null}, hasNexusRuntime={nexusRuntime != null}, " +
                $"cellsAvailable={gridManager.IsCellAvailable(gridPos, new Vector2Int(NexusRuntime.NexusSize, NexusRuntime.NexusSize), null)}.");

            if (!serverSpawnManager.TryPlacePlaceableRuntime(gridPos, nexusType, out _, out _))
                throw new InvalidOperationException($"Failed to place nexus at {gridPos}.");
        }

        private void PublishWorldState()
        {
            worldGenerationState.SetServerValues(generatedSeed, minDistanceFromEdge, waterThreshold, waterNoiseScale,
                waterSmoothPasses);
            statePublished = true;
            RuntimeLog.WorldGen.Info(RuntimeLog.Code.WorldGenStatePublished,
                $"Published world state (seed={generatedSeed}).");
        }

        private void GenerateTerrain(int seed)
        {
            var random = new System.Random(seed);
            var size = gridManager.GridSize;
            var waterCells = GridWaterGenerator.Generate(size, minDistanceFromEdge, waterNoiseScale, waterThreshold,
                waterSmoothPasses, random, nexusCenter, nexusExclusionZone);

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
                    {
                        waterTile = tile;
                        break;
                    }
                }
            }

            var flattenedTiles = new TileType[size.x * size.y];
            for (var y = 0; y < size.y; y++)
            {
                for (var x = 0; x < size.x; x++)
                {
                    var pos = new Vector2Int(x, y);
                    var index = y * size.x + x;
                    if (waterCells.Contains(pos) && waterTile != null)
                        flattenedTiles[index] = waterTile;
                    else
                        flattenedTiles[index] = defaultTile;
                }
            }

            gridManager.SetTileMap(flattenedTiles);
            var tileIds = new string[flattenedTiles.Length];
            for (var i = 0; i < flattenedTiles.Length; i++)
                tileIds[i] = flattenedTiles[i] != null ? flattenedTiles[i].Id : string.Empty;

            worldGenerationState.SetServerTileMap(tileIds);
        }

        private void SpawnEnergySources(int seed)
        {
            var random = new System.Random(seed + 1);
            var size = gridManager.GridSize;
            var energyPlaceables = ResolveEnergyPlaceables();

            GridEnergySourceGenerator.Spawn(
                random,
                size,
                energyPlaceables,
                gridManager,
                serverSpawnManager,
                nodeCount,
                minDistanceBetweenNodes,
                defaultMaxCapacity,
                maxAttemptsPerNode,
                edgePadding,
                nexusCenter,
                nexusExclusionZone);
        }

        private PlaceableType ResolveNexusPlaceableType(GameRegistry registry)
        {
            if (nexusPlaceableType != null && nexusPlaceableType.Prefab != null
                                          && nexusPlaceableType.Prefab.GetComponent<NexusRuntime>() != null)
            {
                return nexusPlaceableType;
            }

            if (nexusPlaceableType != null)
            {
                RuntimeLog.WorldGen.Warning(RuntimeLog.Code.WorldGenNexusFailed,
                    $"Invalid nexusPlaceableType '{nexusPlaceableType?.Id}'. Falling back to first nexus-like PlaceableType.");
            }

            var placeables = registry.PlaceableTypes;
            for (var i = 0; i < placeables.Count; i++)
            {
                var candidate = placeables[i];
                if (candidate == null || candidate.Prefab == null)
                    continue;

                if (candidate.Prefab.GetComponent<NexusRuntime>() != null)
                    return candidate;
            }

            return null;
        }

        private PlaceableType[] ResolveEnergyPlaceables()
        {
            var registry = GameRegistry.Instance;
            if (registry == null)
                throw new InvalidOperationException("GameRegistry is missing at Resources/GameRegistry.");

            var resolved = new List<PlaceableType>();
            if (energyPlaceables != null && energyPlaceables.Length > 0)
            {
                for (var i = 0; i < energyPlaceables.Length; i++)
                {
                    var type = energyPlaceables[i];
                    if (type == null || type.Prefab == null || type.Prefab.GetComponent<EnergySourceRuntime>() == null)
                    {
                        RuntimeLog.WorldGen.Warning(RuntimeLog.Code.WorldGenEnergyFailed,
                            $"Invalid energy placeable at index {i} in WorldGenerationManager.energyPlaceables.");
                        continue;
                    }

                    resolved.Add(type);
                }
            }
            else
            {
                var placeables = registry.PlaceableTypes;
                for (var i = 0; i < placeables.Count; i++)
                {
                    var type = placeables[i];
                    if (type != null && type.Prefab != null && type.Prefab.GetComponent<EnergySourceRuntime>() != null)
                        resolved.Add(type);
                }
            }

            if (resolved.Count == 0)
                throw new InvalidOperationException("No valid energy PlaceableType entries resolved from GameRegistry.");

            return resolved.ToArray();
        }

        private int CreateServerSeed()
        {
            unchecked
            {
                var ticks = DateTime.UtcNow.Ticks;
                return (int)(ticks ^ (ticks >> 32)) ^ GetInstanceID();
            }
        }

        public bool HasRequiredReferences(out string issue)
        {
            return ReferenceValidator.Validate(out issue,
                (gridManager, nameof(gridManager)),
                (worldGenerationState, nameof(worldGenerationState)),
                (serverSpawnManager, nameof(serverSpawnManager)));
        }
    }
}
