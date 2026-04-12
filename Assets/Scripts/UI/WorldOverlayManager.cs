using System.Collections.Generic;
using Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class WorldOverlayManager : MonoBehaviour
    {
        public static WorldOverlayManager Instance { get; private set; }

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Camera mainCamera;

        private VisualElement root;

        private VisualElement Root
        {
            get
            {
                if (root == null && uiDocument != null)
                    root = uiDocument.rootVisualElement;
                return root;
            }
        }
        private readonly Dictionary<EnergyRuntime, EnergyOverlayEntry> energyOverlays = new();
        private readonly Dictionary<TowerRuntime, TowerOverlayEntry> towerOverlays = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            if (mainCamera == null) return;

            foreach (var kvp in energyOverlays)
            {
                if (kvp.Key == null) continue;
                UpdateEnergyOverlay(kvp.Value, kvp.Key);
            }

            foreach (var kvp in towerOverlays)
            {
                if (kvp.Key == null) continue;
                UpdateTowerOverlay(kvp.Value, kvp.Key);
            }
        }

        public void RegisterEnergy(EnergyRuntime energy)
        {
            if (energyOverlays.ContainsKey(energy)) return;
            var r = Root;
            if (r == null) return;
            energyOverlays[energy] = CreateEnergyOverlay(energy);
        }

        public void UnregisterEnergy(EnergyRuntime energy)
        {
            if (energyOverlays.TryGetValue(energy, out var entry))
            {
                Root.Remove(entry.container);
                energyOverlays.Remove(energy);
            }
        }

        public void RegisterTower(TowerRuntime tower)
        {
            if (towerOverlays.ContainsKey(tower)) return;
            var r = Root;
            if (r == null) return;
            towerOverlays[tower] = CreateTowerOverlay(tower);
        }

        public void UnregisterTower(TowerRuntime tower)
        {
            if (towerOverlays.TryGetValue(tower, out var entry))
            {
                Root.Remove(entry.container);
                towerOverlays.Remove(tower);
            }
        }

        private EnergyOverlayEntry CreateEnergyOverlay(EnergyRuntime energy)
        {
            var container = new VisualElement();
            container.style.position = Position.Absolute;
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;

            var bg = new VisualElement();
            bg.style.backgroundColor = new Color(0, 0, 0, 0.6f);
            bg.style.borderTopLeftRadius = 4;
            bg.style.borderTopRightRadius = 4;
            bg.style.borderBottomLeftRadius = 4;
            bg.style.borderBottomRightRadius = 4;
            bg.style.paddingTop = 4;
            bg.style.paddingBottom = 4;
            bg.style.paddingLeft = 8;
            bg.style.paddingRight = 8;

            var label = new Label();
            label.style.color = Color.white;
            label.style.fontSize = 14;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;

            bg.Add(label);
            container.Add(bg);
            Root.Add(container);

            var visualCenter = GetVisualCenter(energy.transform, 1f);

            return new EnergyOverlayEntry { container = container, label = label, bg = bg, visualCenter = visualCenter };
        }

        private TowerOverlayEntry CreateTowerOverlay(TowerRuntime tower)
        {
            var container = new VisualElement();
            container.style.position = Position.Absolute;
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;

            var indicator = new VisualElement();
            indicator.style.width = 12;
            indicator.style.height = 12;
            indicator.style.borderTopLeftRadius = 6;
            indicator.style.borderTopRightRadius = 6;
            indicator.style.borderBottomLeftRadius = 6;
            indicator.style.borderBottomRightRadius = 6;
            indicator.style.backgroundColor = Color.red;

            container.Add(indicator);
            Root.Add(container);

            var visualCenter = GetVisualCenter(tower.transform, 1.5f);

            return new TowerOverlayEntry { container = container, indicator = indicator, visualCenter = visualCenter };
        }

        private Vector3 GetVisualCenter(Transform target, float defaultHeight)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                return bounds.center;
            }

            return target.position + Vector3.up * defaultHeight;
        }

        private void UpdateEnergyOverlay(EnergyOverlayEntry entry, EnergyRuntime energy)
        {
            var worldPos = entry.visualCenter + Vector3.up * 0.5f;
            if (!WorldToScreen(worldPos, out var screenPos))
            {
                entry.container.style.display = DisplayStyle.None;
                return;
            }

            entry.container.style.display = DisplayStyle.Flex;
            entry.container.style.left = screenPos.x - 30;
            entry.container.style.top = Screen.height - screenPos.y - 15;

            entry.label.text = $"{energy.CurrentCapacity}/{energy.MaxCapacity}";

            var ratio = energy.MaxCapacity > 0 ? (float)energy.CurrentCapacity / energy.MaxCapacity : 0;
            var color = ratio > 0.5f ? new Color(0.3f, 0.9f, 0.3f) : ratio > 0.2f ? new Color(0.9f, 0.9f, 0.3f) : new Color(0.9f, 0.3f, 0.3f);
            entry.label.style.color = color;
        }

        private void UpdateTowerOverlay(TowerOverlayEntry entry, TowerRuntime tower)
        {
            var worldPos = entry.visualCenter;
            if (!WorldToScreen(worldPos, out var screenPos))
            {
                entry.container.style.display = DisplayStyle.None;
                return;
            }

            entry.container.style.display = DisplayStyle.Flex;
            entry.container.style.left = screenPos.x - 6;
            entry.container.style.top = Screen.height - screenPos.y - 6;

            entry.indicator.style.backgroundColor = tower.IsPowered ? new Color(0.2f, 0.9f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);
        }

        private bool WorldToScreen(Vector3 worldPos, out Vector2 screenPos)
        {
            var viewportPos = mainCamera.WorldToViewportPoint(worldPos);
            if (viewportPos.z < 0)
            {
                screenPos = default;
                return false;
            }

            screenPos = new Vector2(viewportPos.x * Screen.width, viewportPos.y * Screen.height);
            return true;
        }

        private struct EnergyOverlayEntry
        {
            public VisualElement container;
            public Label label;
            public VisualElement bg;
            public Vector3 visualCenter;
        }

        private struct TowerOverlayEntry
        {
            public VisualElement container;
            public VisualElement indicator;
            public Vector3 visualCenter;
        }
    }
}
