using System.Collections.Generic;
using Controllers;
using Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace UI
{
    public class HotbarController : MonoBehaviour
    {
        [SerializeField]
        private UIDocument uiDocument;
        [SerializeField]
        private TowerPlacementController placementController;

        private VisualElement slotsContainer;
        private readonly List<VisualElement> slots = new();
        private readonly List<RenderTexture> previewTextures = new();
        private readonly List<GameObject> previewInstances = new();
        private readonly List<Camera> previewCameras = new();
        private int selectedIndex = -1;

        private static readonly Key[] SlotKeys =
            { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };

        private void Start()
        {
            var root = uiDocument.rootVisualElement;
            slotsContainer = root.Q<VisualElement>("slots");

            var towerTypes = GameRegistry.Instance.TowerTypes;
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
            var slot = new VisualElement();
            slot.AddToClassList("slot");

            var previewImage = new Image();
            previewImage.AddToClassList("slot-preview");

            var previewTexture = CreatePreview(config, index);
            if (previewTexture != null)
            {
                previewImage.image = previewTexture;
            }

            var nameLabel = new Label(config.DisplayName);
            nameLabel.AddToClassList("slot-name");

            var keyLabel = new Label($"[{index + 1}]");
            keyLabel.AddToClassList("slot-key");

            slot.Add(previewImage);
            slot.Add(nameLabel);
            slot.Add(keyLabel);

            var capturedIndex = index;
            slot.RegisterCallback<ClickEvent>(_ => SelectSlot(capturedIndex));

            slotsContainer.Add(slot);
            slots.Add(slot);
        }

        private RenderTexture CreatePreview(TowerType config, int index)
        {
            if (config.Prefab == null)
            {
                return null;
            }

            var previewPos = new Vector3(1000f + index * 10f, 0f, 0f);
            var instance = Instantiate(config.Prefab, previewPos, Quaternion.identity);
            instance.name = $"HotbarPreview_{config.Id}";

            foreach (var mb in instance.GetComponentsInChildren<MonoBehaviour>())
            {
                mb.enabled = false;
            }

            foreach (var col in instance.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            foreach (var rb in instance.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = true;
            }

            previewInstances.Add(instance);

            var bounds = new Bounds(previewPos, Vector3.one);
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
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

            previewCameras.Add(camera);
            previewTextures.Add(renderTexture);

            return renderTexture;
        }

        private void SelectSlot(int index)
        {
            if (index < 0 || index >= slots.Count)
            {
                return;
            }

            if (selectedIndex >= 0 && selectedIndex < slots.Count)
            {
                slots[selectedIndex].RemoveFromClassList("selected");
            }

            selectedIndex = index;
            slots[selectedIndex].AddToClassList("selected");

            var towerTypes = GameRegistry.Instance.TowerTypes;
            if (towerTypes != null && index < towerTypes.Count && placementController != null)
            {
                placementController.SetTowerConfig(towerTypes[index]);
            }
        }

        private void OnDestroy()
        {
            foreach (var rt in previewTextures)
            {
                if (rt != null)
                {
                    rt.Release();
                    Destroy(rt);
                }
            }

            foreach (var go in previewInstances)
            {
                if (go != null)
                {
                    Destroy(go);
                }
            }

            foreach (var cam in previewCameras)
            {
                if (cam != null)
                {
                    Destroy(cam.gameObject);
                }
            }
        }
    }
}