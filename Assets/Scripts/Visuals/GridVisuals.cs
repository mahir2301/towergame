using Managers;
using UnityEngine;

namespace Visuals
{
    public class GridVisuals : MonoBehaviour
    {
        [SerializeField]
        private GridManager gridManager;
        [SerializeField]
        private float lineWidth = 0.02f;
        [SerializeField]
        private Color lineColor = new(1, 1, 1, 0.1f);
        [SerializeField]
        private float yOffset = 0.01f;

        private static Material LineMaterial => new(Shader.Find("Universal Render Pipeline/Unlit"));

        private void Start()
        {
            CreateGridLines();
        }

        private void CreateGridLines()
        {
            var linesParent = new GameObject("GridLines");
            linesParent.transform.SetParent(transform);
            linesParent.transform.localPosition = Vector3.zero;

            var gridSize = gridManager.GridSize;

            for (var x = 0; x <= gridSize.x; x++)
            {
                CreateLine(
                    new Vector3(x, yOffset, 0),
                    new Vector3(x, yOffset, gridSize.y),
                    linesParent.transform
                );
            }

            for (var z = 0; z <= gridSize.y; z++)
            {
                CreateLine(
                    new Vector3(0, yOffset, z),
                    new Vector3(gridSize.x, yOffset, z),
                    linesParent.transform
                );
            }
        }

        private void CreateLine(Vector3 start, Vector3 end, Transform parent)
        {
            var line = new GameObject("Line");
            line.transform.SetParent(parent);

            var lr = line.AddComponent<LineRenderer>();
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.material = LineMaterial;
            lr.startColor = lineColor;
            lr.endColor = lineColor;
        }
    }
}