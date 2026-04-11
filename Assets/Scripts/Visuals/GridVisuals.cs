using UnityEngine;

namespace Visuals
{
    public class GridVisuals : MonoBehaviour
    {
        [SerializeField] private Color baseColor = new(0.18039216f, 0.49019608f, 0.19607843f, 1f);
        [SerializeField] private Color gridColor = new(0.03f, 0.13f, 0.03f, 0.6f);
        [SerializeField] private float gridThickness = 0.03f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int GridColorId = Shader.PropertyToID("_GridColor");
        private static readonly int GridThicknessId = Shader.PropertyToID("_GridThickness");

        private void Awake()
        {
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null) return;

            var shader = Shader.Find("Custom/Ground");
            if (shader == null)
            {
                Debug.LogError("GridVisuals: Could not find 'Custom/Ground' shader!");
                return;
            }

            var material = new Material(shader);
            material.SetColor(BaseColorId, baseColor);
            material.SetColor(GridColorId, gridColor);
            material.SetFloat(GridThicknessId, gridThickness);
            renderer.material = material;
        }
    }
}