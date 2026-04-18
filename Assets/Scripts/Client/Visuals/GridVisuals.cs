using Shared;
using Shared.Runtime;
using Shared.Utilities;
using UnityEngine;
using Unity.Netcode;

namespace Client.Visuals
{
    public class GridVisuals : MonoBehaviour
    {
        [SerializeField] private Color baseColor = new(0.18039216f, 0.49019608f, 0.19607843f, 1f);
        [SerializeField] private Color gridColor = new(0.03f, 0.13f, 0.03f, 0.6f);
        [SerializeField] private float gridThickness = 0.03f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int GridColorId = Shader.PropertyToID("_GridColor");
        private static readonly int GridThicknessId = Shader.PropertyToID("_GridThickness");

        private MeshRenderer meshRenderer;
        private Material material;
        private Color gridColorHidden;
        private bool gridVisible = true;
        private bool subscribedToPhaseEvents;

        private void Awake()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsClient)
            {
                enabled = false;
                return;
            }

            meshRenderer = GetComponent<MeshRenderer>();

            var shader = Shader.Find("Custom/Ground");
            if (shader == null)
            {
                RuntimeLog.Visual.Error(RuntimeLog.Code.VisualMissingGroundShader,
                    "Could not find 'Custom/Ground' shader.");
                return;
            }

            gridColorHidden = gridColor;
            gridColorHidden.a = 0f;

            material = new Material(shader);
            material.SetColor(BaseColorId, baseColor);
            material.SetColor(GridColorId, gridColor);
            material.SetFloat(GridThicknessId, gridThickness);
            meshRenderer.material = material;
        }

        private void Start()
        {
            if (RuntimeBootstrap.IsReady)
                SubscribeToPhaseEvents();
        }

        private void Update()
        {
            if (!subscribedToPhaseEvents && RuntimeBootstrap.IsReady)
                SubscribeToPhaseEvents();
        }

        private void OnDestroy()
        {
            if (subscribedToPhaseEvents)
            {
                GameEvents.PhaseChanged -= OnPhaseChanged;
                subscribedToPhaseEvents = false;
            }

            if (material != null) Destroy(material);
        }

        private void SubscribeToPhaseEvents()
        {
            if (subscribedToPhaseEvents)
                return;

            GameEvents.PhaseChanged += OnPhaseChanged;
            subscribedToPhaseEvents = true;

            var phaseManager = PhaseManager.Instance;
            if (phaseManager != null)
                SetGridVisible(phaseManager.CurrentPhase == GamePhase.Building);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            SetGridVisible(phase == GamePhase.Building);
        }

        public void SetGridVisible(bool visible)
        {
            gridVisible = visible;
            if (material != null)
                material.SetColor(GridColorId, visible ? gridColor : gridColorHidden);
        }

        public bool IsGridVisible => gridVisible;
    }
}
