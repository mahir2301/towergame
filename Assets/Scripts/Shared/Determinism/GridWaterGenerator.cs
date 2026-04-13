using System.Collections.Generic;
using UnityEngine;

namespace Shared.Determinism
{
    public static class GridWaterGenerator
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
}
