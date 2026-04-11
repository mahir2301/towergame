using UnityEngine;

namespace Visuals
{
    public class PowerLineVisual : MonoBehaviour
    {
        [SerializeField] private int points = 6;
        [SerializeField] private float sag = 0.25f;
        [SerializeField] private Color lineColor = new(0.3f, 0.55f, 0.9f, 0.7f);
        [SerializeField] private float lineWidth = 0.06f;

        private LineRenderer lineRenderer;
        private Vector3 startPos;
        private Vector3 endPos;
        private float startY;
        private float endY;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public void Setup(Vector3 start, Vector3 end)
        {
            startPos = start;
            endPos = end;
            startY = start.y;
            endY = end.y;

            SetupLineRenderer();
            Draw();
        }

        private void SetupLineRenderer()
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = false;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetColor(BaseColorId, lineColor);
            lineRenderer.material = mat;
        }

        private void Draw()
        {
            lineRenderer.positionCount = points;
            for (var i = 0; i < points; i++)
            {
                var t = i / (float)(points - 1);
                var x = Mathf.Lerp(startPos.x, endPos.x, t);
                var z = Mathf.Lerp(startPos.z, endPos.z, t);
                var y = Mathf.Lerp(startY, endY, t) - sag * 4f * t * (1f - t);
                lineRenderer.SetPosition(i, new Vector3(x, y, z));
            }
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