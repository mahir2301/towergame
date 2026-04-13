using System;
using Game.Shared.Determinism;
using Game.Shared.Data;
using Game.Shared.Runtime;
using Game.Shared.Grid;
using Unity.Netcode;
using UnityEngine;

namespace Game.Server.Managers
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

        private int generatedSeed = -1;
        private bool statePublished;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer || gridManager == null)
                return;

            var seed = CreateServerSeed();
            gridManager.ClearWorldState();

            GenerateTerrain(seed);
            SpawnEnergySources(seed);
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
            var random = new System.Random(seed);
            GridWaterGenerator.AdvanceRandom(random);

            var size = gridManager.GridSize;
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

        private int CreateServerSeed()
        {
            unchecked
            {
                var ticks = DateTime.UtcNow.Ticks;
                return (int)(ticks ^ (ticks >> 32)) ^ GetInstanceID();
            }
        }
    }
}
