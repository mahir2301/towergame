using System;
using Shared.Determinism;
using Shared.Data;
using Shared.Runtime;
using Shared.Grid;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace Server.Managers
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
        [SerializeField] private WorldGenerationState worldGenerationState;
        [SerializeField] private ServerSpawnManager serverSpawnManager;

        private int generatedSeed = -1;
        private bool statePublished;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer)
                return;

            if (!HasRequiredReferences(out var issue))
            {
                RuntimeLog.WorldGen.Error(RuntimeLog.Code.WorldGenTerrainFailed,
                    $"Cannot generate world: {issue}");
                return;
            }

            var seed = CreateServerSeed();
            gridManager.ClearWorldState();

            RuntimeLog.WorldGen.Info(RuntimeLog.Code.WorldGenStart,
                $"Starting world generation with seed {seed}.");

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
            TryPublishWorldState();
        }

        private void Update()
        {
            if (!IsServer || statePublished || generatedSeed < 0)
                return;

            TryPublishWorldState();
        }

        private void TryPublishWorldState()
        {
            if (worldGenerationState == null || !worldGenerationState.IsSpawned)
                return;

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
                waterSmoothPasses, random);
            gridManager.SetTerrainCells(waterCells);
        }

        private void SpawnEnergySources(int seed)
        {
            var random = new System.Random(seed + 1);
            var size = gridManager.GridSize;
            GridEnergySourceGenerator.Spawn(
                random,
                size,
                energyNodeConfig,
                gridManager,
                serverSpawnManager,
                nodeCount,
                minDistanceBetweenNodes,
                defaultMaxCapacity,
                maxAttemptsPerNode,
                edgePadding);
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
            if (gridManager == null)
            {
                issue = "gridManager is not assigned.";
                return false;
            }

            if (worldGenerationState == null)
            {
                issue = "worldGenerationState is not assigned.";
                return false;
            }

            if (serverSpawnManager == null)
            {
                issue = "serverSpawnManager is not assigned.";
                return false;
            }

            issue = null;
            return true;
        }
    }
}
