using UnityEngine;

namespace Visuals
{
    public class RangeIndicator : MonoBehaviour
    {
        [SerializeField] private int segments = 64;
        [SerializeField] private float yOffset = 0.02f;
        [SerializeField] private float lineWidth = 0.05f;
        [SerializeField] private Color inRangeColor = new(0.2f, 0.9f, 0.2f, 0.5f);
        [SerializeField] private Color outOfRangeColor = new(0.9f, 0.2f, 0.2f, 0.5f);

        private LineRenderer lineRenderer;
        private bool isVisible;
        private float currentRadius;
        private bool currentInRange;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public bool IsVisible => isVisible;

        public void Show(float radius, bool inRange)
        {
            if (lineRenderer == null)
            {
                SetupLineRenderer();
            }

            currentRadius = radius;
            currentInRange = inRange;
            isVisible = true;

            UpdateCircle();
            UpdateColor();

            lineRenderer.enabled = true;
        }

        public void ShowEnergy(float radius)
        {
            Show(radius, true);
        }

        public void ShowAntenna(float radius)
        {
            Show(radius, false);
        }

        public void UpdateRange(float radius, bool inRange)
        {
            if (Mathf.Abs(currentRadius - radius) > 0.01f)
            {
                currentRadius = radius;
                UpdateCircle();
            }

            if (currentInRange != inRange)
            {
                currentInRange = inRange;
                UpdateColor();
            }
        }

        public void Hide()
        {
            isVisible = false;
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }
        }

        public void SetRadius(float radius)
        {
            currentRadius = radius;
            UpdateCircle();
        }

        public void SetInRange(bool inRange)
        {
            currentInRange = inRange;
            UpdateColor();
        }

        private void SetupLineRenderer()
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.sortingOrder = 1;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetColor(BaseColorId, inRangeColor);
            lineRenderer.material = mat;

            lineRenderer.positionCount = segments;
        }

        private void UpdateCircle()
        {
            if (lineRenderer == null || currentRadius <= 0)
            {
                return;
            }

            var angleStep = 360f / segments;
            for (var i = 0; i < segments; i++)
            {
                var angle = i * angleStep * Mathf.Deg2Rad;
                var x = Mathf.Cos(angle) * currentRadius;
                var z = Mathf.Sin(angle) * currentRadius;
                lineRenderer.SetPosition(i, new Vector3(x, yOffset, z));
            }
        }

        private void UpdateColor()
        {
            if (lineRenderer == null)
            {
                return;
            }

            var color = currentInRange ? inRangeColor : outOfRangeColor;
            lineRenderer.material.SetColor(BaseColorId, color);
        }

        private void OnDestroy()
        {
            if (lineRenderer != null && lineRenderer.material != null)
            {
                Destroy(lineRenderer.material);
            }
        }
    }
}