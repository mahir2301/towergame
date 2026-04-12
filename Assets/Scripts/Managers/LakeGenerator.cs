using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class LakeGenerator : NetworkBehaviour
    {
        [SerializeField] private int lakeCount = 3;
        [SerializeField] private int minLakeSize = 5;
        [SerializeField] private int maxLakeSize = 10;
        [SerializeField] private int minDistanceFromEdge = 3;
        [SerializeField] private int waterSeed = 42;
        [SerializeField] private Material waterMaterial;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private GameObject groundObject;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            GenerateLakes();
        }

        private void GenerateLakes()
        {
            var state = Random.state;
            Random.InitState(waterSeed);

            var gridSize = gridManager.GridSize;
            var quadrantWidth = gridSize.x / 2;
            var quadrantHeight = gridSize.y / 2;

            for (var i = 0; i < lakeCount; i++)
            {
                var quadrant = i % 4;
                var quadrantX = quadrant % 2 == 0 ? 0 : quadrantWidth;
                var quadrantY = quadrant < 2 ? 0 : quadrantHeight;

                var lakeWidth = Random.Range(minLakeSize, maxLakeSize + 1);
                var lakeHeight = Random.Range(minLakeSize, maxLakeSize + 1);

                var maxX = quadrantWidth - lakeWidth - minDistanceFromEdge;
                var maxY = quadrantHeight - lakeHeight - minDistanceFromEdge;
                var minX = minDistanceFromEdge;
                var minY = minDistanceFromEdge;

                var offsetX = Random.Range(minX, maxX);
                var offsetY = Random.Range(minY, maxY);

                CreateLake(quadrantX + offsetX, quadrantY + offsetY, lakeWidth, lakeHeight);
            }

            Random.state = state;
        }

        private void CreateLake(int startX, int startY, int width, int height)
        {
            var waterCells = new List<Vector2Int>();

            for (var x = 0; x < width; x++)
                for (var y = 0; y < height; y++)
                    waterCells.Add(new Vector2Int(startX + x, startY + y));

            gridManager.MarkCellsAsWater(waterCells);
            CreateWaterVisuals(startX, startY, width, height);
        }

        private void CreateWaterVisuals(int startX, int startY, int width, int height)
        {
            var worldPos = gridManager.GridToWorld(new Vector2Int(startX, startY));
            var centerX = worldPos.x + width / 2f - 0.5f;
            var centerZ = worldPos.z + height / 2f - 0.5f;

            var waterPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            waterPlane.name = $"Lake_{startX}_{startY}";
            waterPlane.transform.position = new Vector3(centerX, 0.01f, centerZ);
            waterPlane.transform.localScale = new Vector3(width * 0.1f, 1, height * 0.1f);
            waterPlane.GetComponent<MeshRenderer>().material = waterMaterial;

            if (groundObject != null)
                waterPlane.transform.SetParent(groundObject.transform);
        }
    }
}
