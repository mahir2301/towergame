using System;
using System.Collections.Generic;
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
        [SerializeField] private string[] energyTypeIds;

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
                SpawnNexus(seed);
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

        private void SpawnNexus(int seed)
        {
            var registry = GameRegistry.Instance;
            if (registry == null)
                throw new InvalidOperationException("GameRegistry is missing at Resources/GameRegistry.");

            var nexusConfig = registry.GetNexusType();
            if (nexusConfig == null)
                throw new InvalidOperationException("No NexusType found in GameRegistry.");

            var size = gridManager.GridSize;
            nexusCenter = new Vector2Int(size.x / 2, size.y / 2);
            var halfSize = NexusRuntime.NexusSize / 2;
            var gridPos = new Vector2Int(nexusCenter.x - halfSize, nexusCenter.y - halfSize);

            RuntimeLog.WorldGen.Info(RuntimeLog.Code.WorldGenNexusDone,
                $"Spawning nexus: gridSize={size}, center={nexusCenter}, gridPos={gridPos}, " +
                $"prefab={nexusConfig.Prefab != null}, hasNexusRuntime={nexusConfig.Prefab?.GetComponent<NexusRuntime>() != null}, " +
                $"cellsAvailable={gridManager.IsCellAvailable(gridPos, new Vector2Int(NexusRuntime.NexusSize, NexusRuntime.NexusSize), false)}.");

            if (!serverSpawnManager.TryPlaceNexusRuntime(gridPos, nexusConfig, out _))
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
            gridManager.SetTerrainCells(waterCells);
        }

        private void SpawnEnergySources(int seed)
        {
            var random = new System.Random(seed + 1);
            var size = gridManager.GridSize;
            var energyTypes = ResolveEnergyTypes();

            GridEnergySourceGenerator.Spawn(
                random,
                size,
                energyTypes,
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

        private EnergyType[] ResolveEnergyTypes()
        {
            var registry = GameRegistry.Instance;
            if (registry == null)
                throw new InvalidOperationException("GameRegistry is missing at Resources/GameRegistry.");

            var resolved = new List<EnergyType>();
            if (energyTypeIds != null && energyTypeIds.Length > 0)
            {
                for (var i = 0; i < energyTypeIds.Length; i++)
                {
                    var id = energyTypeIds[i];
                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    var type = registry.GetEnergyType(id);
                    if (type != null)
                        resolved.Add(type);
                    else
                        RuntimeLog.WorldGen.Warning(RuntimeLog.Code.WorldGenEnergyFailed,
                            $"Unknown energy type id '{id}' in WorldGenerationManager.energyTypeIds.");
                }
            }
            else
            {
                var energyTypes = registry.EnergyTypes;
                for (var i = 0; i < energyTypes.Count; i++)
                {
                    var type = energyTypes[i];
                    if (type != null)
                        resolved.Add(type);
                }
            }

            if (resolved.Count == 0)
                throw new InvalidOperationException("No valid EnergyType entries resolved from GameRegistry.");

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
