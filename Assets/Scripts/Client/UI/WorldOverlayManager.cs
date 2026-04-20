using System.Collections.Generic;
using Shared.Runtime;
using Shared.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UI
{
    public class WorldOverlayManager : MonoBehaviour
    {
        public static WorldOverlayManager Instance { get; private set; }

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private StyleSheet overlayStyleSheet;
        [SerializeField] private VisualTreeAsset energyTemplate;
        [SerializeField] private VisualTreeAsset towerTemplate;

        private VisualElement root;
        private readonly Dictionary<EnergyRuntime, (VisualElement root, Label label)> energyOverlays = new();
        private readonly Dictionary<TowerRuntime, (VisualElement root, VisualElement indicator)> towerOverlays = new();

        private void Awake()
        {
            if (!SingletonUtility.TryAssign(Instance, this, value => Instance = value))
                return;

            if (!RuntimeNet.ShouldRunNetworkedClientSystems())
            {
                enabled = false;
                return;
            }

            if (uiDocument == null)
            {
                RuntimeLog.Overlay.Error(RuntimeLog.Code.OverlayMissingDocument,
                    "UI Document not assigned.");
                return;
            }

            root = uiDocument.rootVisualElement;
            if (overlayStyleSheet != null)
                root.styleSheets.Add(overlayStyleSheet);
        }

        private void LateUpdate()
        {
            UpdatePositions();
        }

        private void OnDestroy()
        {
            SingletonUtility.ClearIfCurrent(Instance, this, () => Instance = null);
        }

        public void RegisterEnergy(EnergyRuntime energy)
        {
            if (energyOverlays.ContainsKey(energy) || root == null || energyTemplate == null) return;

            var overlay = energyTemplate.CloneTree();
            overlay.style.position = Position.Absolute;
            var label = overlay.Q<Label>("energy-label");
            if (label == null)
            {
                RuntimeLog.Overlay.Warning(RuntimeLog.Code.OverlayMissingEnergyLabel,
                    "Energy template missing 'energy-label'.");
                return;
            }

            root.Add(overlay);
            energyOverlays[energy] = (overlay, label);
        }

        public void UnregisterEnergy(EnergyRuntime energy)
        {
            if (!energyOverlays.TryGetValue(energy, out var view)) return;
            view.root.RemoveFromHierarchy();
            energyOverlays.Remove(energy);
        }

        public void RegisterTower(TowerRuntime tower)
        {
            if (towerOverlays.ContainsKey(tower) || root == null || towerTemplate == null) return;

            var overlay = towerTemplate.CloneTree();
            overlay.style.position = Position.Absolute;
            var indicator = overlay.Q("tower-indicator");
            if (indicator == null)
            {
                RuntimeLog.Overlay.Warning(RuntimeLog.Code.OverlayMissingTowerIndicator,
                    "Tower template missing 'tower-indicator'.");
                return;
            }

            root.Add(overlay);
            towerOverlays[tower] = (overlay, indicator);
        }

        public void UnregisterTower(TowerRuntime tower)
        {
            if (!towerOverlays.TryGetValue(tower, out var view)) return;
            view.root.RemoveFromHierarchy();
            towerOverlays.Remove(tower);
        }

        private void UpdatePositions()
        {
            if (root == null) return;

            var cam = Camera.main;
            if (cam == null) return;
            if (root.panel == null) return;

            foreach (var kvp in energyOverlays)
            {
                if (kvp.Key == null) continue;
                var energy = kvp.Key;
                var worldPos = energy.transform.position + Vector3.up * 2f;
                SetOverlayPosition(kvp.Value.root, worldPos, cam);

                var label = kvp.Value.label;
                label.text = $"{energy.CurrentCapacity}/{energy.MaxCapacity}";
                var ratio = energy.MaxCapacity > 0 ? (float)energy.CurrentCapacity / energy.MaxCapacity : 0;
                label.RemoveFromClassList("high");
                label.RemoveFromClassList("medium");
                label.RemoveFromClassList("low");
                label.AddToClassList(ratio > 0.5f ? "high" : ratio > 0.2f ? "medium" : "low");
            }

            foreach (var kvp in towerOverlays)
            {
                if (kvp.Key == null) continue;
                var tower = kvp.Key;
                var worldPos = tower.transform.position + Vector3.up * 2f;
                SetOverlayPosition(kvp.Value.root, worldPos, cam);

                var indicator = kvp.Value.indicator;
                indicator.RemoveFromClassList("powered");
                indicator.RemoveFromClassList("unpowered");
                indicator.AddToClassList(tower.IsPowered ? "powered" : "unpowered");
            }
        }

        private void SetOverlayPosition(VisualElement overlay, Vector3 worldPosition, Camera camera)
        {
            var depth = Vector3.Dot(worldPosition - camera.transform.position, camera.transform.forward);
            if (depth < camera.nearClipPlane || depth > camera.farClipPlane)
            {
                overlay.style.display = DisplayStyle.None;
                return;
            }

            overlay.style.display = DisplayStyle.Flex;
            var panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, worldPosition, camera);
            overlay.style.left = panelPos.x;
            overlay.style.top = panelPos.y;
        }
    }
}
