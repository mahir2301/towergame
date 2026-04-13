using System.Collections.Generic;
using Client.Controllers;
using Shared.Data;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Client.UI
{
    public class HotbarController : MonoBehaviour
    {
        private readonly struct PreviewResources
        {
            public PreviewResources(GameObject instance, Camera camera, RenderTexture texture)
            {
                Instance = instance;
                Camera = camera;
                Texture = texture;
            }

            public GameObject Instance { get; }
            public Camera Camera { get; }
            public RenderTexture Texture { get; }
        }

        [SerializeField]
        private UIDocument uiDocument;
        [SerializeField]
        private TowerPlacementController placementController;
        [SerializeField]
        private VisualTreeAsset slotTemplate;
        [SerializeField]
        private int sortingOrder = 100;

        private VisualElement slotsContainer;
        private readonly List<VisualElement> slots = new();
        private readonly List<PreviewResources> previews = new();
        private int selectedIndex = -1;

        private static readonly Key[] SlotKeys =
            { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };

        private void Start()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsClient)
            {
                enabled = false;
                return;
            }

            uiDocument.sortingOrder = sortingOrder;

            var root = uiDocument.rootVisualElement;
            slotsContainer = root.Q<VisualElement>("slots");
            if (slotsContainer == null) { Debug.LogError("[Hotbar] Missing 'slots' container"); return; }

            if (slotTemplate == null) { Debug.LogError("[Hotbar] Slot template not assigned!"); return; }
            if (placementController == null) placementController = FindAnyObjectByType<TowerPlacementController>();

            var towerTypes = GameRegistry.Instance?.TowerTypes;
            if (towerTypes == null || towerTypes.Count == 0)
            {
                Debug.LogError("[Hotbar] No tower types in GameRegistry!");
                return;
            }

            for (var i = 0; i < towerTypes.Count; i++)
            {
                CreateSlot(towerTypes[i], i);
            }

            SelectSlot(0);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            for (var i = 0; i < slots.Count && i < SlotKeys.Length; i++)
            {
                if (keyboard[SlotKeys[i]].wasPressedThisFrame)
                {
                    SelectSlot(i);
                    break;
                }
            }
        }

        private void CreateSlot(TowerType config, int index)
        {
            var slot = slotTemplate.CloneTree();

            var previewImage = slot.Q<Image>("slot-preview");
            previewImage ??= slot.Q<Image>(className: "slot-preview");

            var previewTexture = CreatePreview(config, index);
            if (previewImage != null && previewTexture != null) previewImage.image = previewTexture;

            var nameLabel = slot.Q<Label>(className: "slot-name");
            if (nameLabel != null) nameLabel.text = config.DisplayName;

            var keyLabel = slot.Q<Label>(className: "slot-key");
            if (keyLabel != null) keyLabel.text = $"[{index + 1}]";

            var capturedIndex = index;
            slot.RegisterCallback<ClickEvent>(evt => {
                evt.StopPropagation();
                SelectSlot(capturedIndex);
            });

            slotsContainer.Add(slot);
            slots.Add(slot);
        }

        private RenderTexture CreatePreview(TowerType config, int index)
        {
            if (config.Prefab == null) return null;

            var previewPos = new Vector3(1000f + index * 10f, 0f, 0f);
            var instance = Instantiate(config.Prefab, previewPos, Quaternion.identity);
            instance.name = $"HotbarPreview_{config.Id}";
            PrefabHelper.DisableForPreview(instance);

            var bounds = new Bounds(previewPos, Vector3.one);
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
            }

            var cameraGo = new GameObject($"HotbarCamera_{config.Id}");
            cameraGo.transform.SetParent(transform);

            var isoRotation = Quaternion.Euler(30f, 45f, 0f);
            var cameraSize = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 1.8f;
            var cameraDistance = cameraSize;

            cameraGo.transform.rotation = isoRotation;
            cameraGo.transform.position = bounds.center - cameraGo.transform.forward * cameraDistance;

            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = cameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = cameraDistance * 5f;
            camera.depth = -10;

            var renderTexture = new RenderTexture(128, 128, 16, RenderTextureFormat.ARGB32);
            renderTexture.Create();
            camera.targetTexture = renderTexture;

            previews.Add(new PreviewResources(instance, camera, renderTexture));

            return renderTexture;
        }

        private void SelectSlot(int index)
        {
            if (index < 0 || index >= slots.Count) return;

            if (selectedIndex >= 0 && selectedIndex < slots.Count) slots[selectedIndex].RemoveFromClassList("selected");

            selectedIndex = index;
            slots[selectedIndex].AddToClassList("selected");

            var towerTypes = GameRegistry.Instance?.TowerTypes;
            if (towerTypes != null && index < towerTypes.Count && placementController != null)
                placementController.SetTowerConfig(towerTypes[index]);
        }

        private void OnDestroy()
        {
            foreach (var preview in previews)
            {
                if (preview.Camera != null) Destroy(preview.Camera.gameObject);
                if (preview.Texture != null)
                {
                    preview.Texture.Release();
                    Destroy(preview.Texture);
                }
                if (preview.Instance != null) Destroy(preview.Instance);
            }
        }
    }
}
