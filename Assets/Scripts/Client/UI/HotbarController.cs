using System.Collections.Generic;
using Client.Controllers;
using Shared;
using Shared.Data;
using Shared.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Client.UI
{
    public class HotbarController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private TowerPlacementController placementController;
        [SerializeField] private PlayerLoadout playerLoadout;

        private VisualElement slotsContainer;
        private VisualElement phaseButtonsContainer;
        private readonly List<VisualElement> slots = new();
        private readonly List<GameObject> previewInstances = new();
        private readonly List<Camera> previewCameras = new();
        private readonly List<RenderTexture> previewTextures = new();
        private int selectedIndex = -1;
        private GamePhase currentPhase;

        private static readonly Key[] SlotKeys =
            { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };

        private void Start()
        {
            if (!RuntimeNet.ShouldRunNetworkedClientSystems())
            {
                enabled = false;
                return;
            }

            var root = uiDocument.rootVisualElement;
            slotsContainer = root.Q<VisualElement>("slots");
            phaseButtonsContainer = root.Q<VisualElement>("phase-buttons");

            CreatePhaseButtons();

            GameEvents.PhaseChanged += OnPhaseChanged;
            currentPhase = PhaseManager.Instance.CurrentPhase;

            RebuildSlots();
        }

        private void OnDestroy()
        {
            GameEvents.PhaseChanged -= OnPhaseChanged;
            CleanupPreviews();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            for (var i = 0; i < slots.Count && i < SlotKeys.Length; i++)
            {
                if (keyboard[SlotKeys[i]].wasPressedThisFrame)
                {
                    SelectSlot(i);
                    break;
                }
            }
        }

        private void CreatePhaseButtons()
        {
            if (phaseButtonsContainer == null) return;

            var startWaveBtn = new Button(() => PhaseManager.Instance.RequestSetPhaseRpc(GamePhase.Combat));
            startWaveBtn.text = "Start Wave";
            startWaveBtn.AddToClassList("phase-button");
            startWaveBtn.AddToClassList("combat");
            phaseButtonsContainer.Add(startWaveBtn);

            var buildBtn = new Button(() => PhaseManager.Instance.RequestSetPhaseRpc(GamePhase.Building));
            buildBtn.text = "Build";
            buildBtn.AddToClassList("phase-button");
            buildBtn.AddToClassList("build");
            phaseButtonsContainer.Add(buildBtn);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            currentPhase = phase;
            RebuildSlots();
        }

        private void RebuildSlots()
        {
            CleanupPreviews();
            slotsContainer.Clear();
            slots.Clear();
            selectedIndex = -1;

            if (currentPhase == GamePhase.Combat)
                BuildWeaponSlots();
            else
                BuildTowerSlots();

            if (slots.Count > 0)
                SelectSlot(0);
        }

        private void BuildTowerSlots()
        {
            var placeableTypes = ResolveBuildPlaceables();
            for (var i = 0; i < placeableTypes.Count; i++)
            {
                var config = placeableTypes[i];
                var slot = CreateSlotVisual(config.DisplayName, i);

                if (config.Prefab != null)
                {
                    var previewImage = slot.Q<Image>(className: "slot-preview");
                    var previewTexture = CreatePlaceablePreview(config, i);
                    if (previewImage != null && previewTexture != null)
                        previewImage.image = previewTexture;
                }

                var capturedIndex = i;
                slot.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); SelectSlot(capturedIndex); });
                slotsContainer.Add(slot);
                slots.Add(slot);
            }
        }

        private void BuildWeaponSlots()
        {
            var weaponTypes = GameRegistry.Instance.WeaponTypes;
            for (var i = 0; i < weaponTypes.Count; i++)
            {
                var config = weaponTypes[i];
                var slot = CreateSlotVisual(config.DisplayName, i);

                var previewImage = slot.Q<Image>(className: "slot-preview");
                if (previewImage != null)
                    previewImage.style.backgroundColor = new Color(0.3f, 0.2f, 0.1f, 0.5f);

                var capturedIndex = i;
                slot.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); SelectSlot(capturedIndex); });
                slotsContainer.Add(slot);
                slots.Add(slot);
            }
        }

        private VisualElement CreateSlotVisual(string name, int index)
        {
            var slot = new VisualElement();
            slot.AddToClassList("slot");

            var preview = new Image();
            preview.AddToClassList("slot-preview");
            slot.Add(preview);

            var nameLabel = new Label(name);
            nameLabel.AddToClassList("slot-name");
            slot.Add(nameLabel);

            var keyLabel = new Label($"[{index + 1}]");
            keyLabel.AddToClassList("slot-key");
            slot.Add(keyLabel);

            return slot;
        }

        private RenderTexture CreatePlaceablePreview(PlaceableType config, int index)
        {
            if (config.Prefab == null) return null;

            var previewPos = new Vector3(1000f + index * 10f, 0f, 0f);
            var instance = Instantiate(config.Prefab, previewPos, Quaternion.identity);
            instance.name = $"HotbarPreview_{config.Id}";

            foreach (var mb in instance.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
            foreach (var col in instance.GetComponentsInChildren<Collider>()) col.enabled = false;

            previewInstances.Add(instance);

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

            previewCameras.Add(camera);
            previewTextures.Add(renderTexture);

            return renderTexture;
        }

        private void SelectSlot(int index)
        {
            if (index < 0 || index >= slots.Count) return;

            if (selectedIndex >= 0 && selectedIndex < slots.Count)
                slots[selectedIndex].RemoveFromClassList("selected");

            selectedIndex = index;
            slots[selectedIndex].AddToClassList("selected");

            if (currentPhase == GamePhase.Building && placementController != null)
            {
                var placeableTypes = ResolveBuildPlaceables();
                if (index < placeableTypes.Count)
                    placementController.SetPlaceableType(placeableTypes[index]);
            }
        }

        private IReadOnlyList<PlaceableType> ResolveBuildPlaceables()
        {
            if (playerLoadout != null && playerLoadout.SelectedPlaceables != null && playerLoadout.SelectedPlaceables.Count > 0)
                return playerLoadout.SelectedPlaceables;

            return GameRegistry.Instance.PlaceableTypes;
        }

        private void CleanupPreviews()
        {
            foreach (var cam in previewCameras)
                if (cam != null) Destroy(cam.gameObject);
            foreach (var rt in previewTextures)
                if (rt != null) { rt.Release(); Destroy(rt); }
            foreach (var go in previewInstances)
                if (go != null) Destroy(go);

            previewCameras.Clear();
            previewTextures.Clear();
            previewInstances.Clear();
        }
    }
}
