using Data;
using Managers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Utilities;

namespace Controllers
{
    public class TowerPlacementController : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;
        [SerializeField] private TowerSpawnSystem towerSpawnSystem;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private TowerType currentTowerConfig;
        [SerializeField] private Color canConnectColor = new(0.2f, 0.8f, 0.2f, 0.6f);
        [SerializeField] private Color cannotConnectColor = new(0.8f, 0.2f, 0.2f, 0.6f);

        private GameObject ghostInstance;
        private Renderer[] ghostRenderers;
        private MaterialPropertyBlock ghostPropertyBlock;
        private Vector2Int? currentGridPos;

        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        private void Start()
        {
            ghostPropertyBlock = new MaterialPropertyBlock();
            CreateGhost();
        }

        private void Update()
        {
            UpdateGhostState();
        }

        private void CreateGhost()
        {
            if (currentTowerConfig?.Prefab == null)
                return;

            ghostInstance = Instantiate(currentTowerConfig.Prefab, Vector3.zero, Quaternion.identity);
            ghostInstance.name = "TowerGhost";
            PrefabHelper.DisableForPreview(ghostInstance);

            ghostRenderers = ghostInstance.GetComponentsInChildren<Renderer>();
            ghostInstance.SetActive(false);
        }

        private void UpdateGhostState()
        {
            if (ghostInstance == null) return;

            if (IsPointerOverAnyUI())
            {
                ghostInstance.SetActive(false);
                currentGridPos = null;
                return;
            }

            var gridPos = GetMouseGridPosition();
            currentGridPos = gridPos;

            if (gridPos is null || currentTowerConfig == null)
            {
                ghostInstance.SetActive(false);
                return;
            }

            var isValidPlacement = gridManager.IsCellAvailable(
                currentGridPos.Value, currentTowerConfig.Size, currentTowerConfig.CanBePlacedOnWater);

            if (!isValidPlacement)
            {
                ghostInstance.SetActive(false);
                return;
            }

            ghostInstance.SetActive(true);
            ghostInstance.transform.position = GridManager.TowerWorldPos(currentGridPos.Value, currentTowerConfig);

            UpdateGhostEnergyIndicator();
        }

        private void UpdateGhostEnergyIndicator()
        {
            if (currentTowerConfig == null || currentGridPos == null)
                return;

            var classType = currentTowerConfig.ClassType;
            var energyCost = currentTowerConfig.Stats.energyCost;
            var gridPos = currentGridPos.Value;

            var networkManager = EnergyNetworkManager.Instance;
            var isInRange = networkManager != null
                            && networkManager.IsPositionInRange(gridPos, classType, energyCost);

            var tintColor = isInRange ? canConnectColor : cannotConnectColor;
            ghostPropertyBlock.SetColor(BaseColorProperty, tintColor);

            foreach (var renderer in ghostRenderers)
                renderer.SetPropertyBlock(ghostPropertyBlock);
        }

        private Vector2Int? GetMouseGridPosition()
        {
            var mousePos = Mouse.current.position.ReadValue();
            var ray = mainCamera.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0));
            var groundPlane = new Plane(Vector3.up, 0);

            if (!groundPlane.Raycast(ray, out var distance))
                return null;

            var worldPos = ray.GetPoint(distance);
            return gridManager.WorldToGrid(worldPos);
        }

        public void OnPlaceTower(InputAction.CallbackContext context)
        {
            if (!context.performed || currentGridPos is null || currentTowerConfig == null)
                return;

            if (IsPointerOverAnyUI())
                return;

            if (!gridManager.IsCellAvailable(
                    currentGridPos.Value, currentTowerConfig.Size, currentTowerConfig.CanBePlacedOnWater))
                return;

            towerSpawnSystem.RequestPlaceTowerServerRpc(currentTowerConfig.Id, currentGridPos.Value);
        }

        private static bool IsPointerOverAnyUI()
        {
            if (Mouse.current == null)
                return false;

            var mousePos = Mouse.current.position.ReadValue();
            var panelInputPos = new Vector2(mousePos.x, Screen.height - mousePos.y);
            var documents = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);

            for (var i = 0; i < documents.Length; i++)
            {
                var root = documents[i].rootVisualElement;
                var panel = root?.panel;
                if (panel == null)
                    continue;

                var panelPos = RuntimePanelUtils.ScreenToPanel(panel, panelInputPos);
                var hit = panel.Pick(panelPos);
                if (hit != null && hit != root)
                    return true;
            }

            return false;
        }

        public void SetTowerConfig(TowerType config)
        {
            currentTowerConfig = config;

            if (ghostInstance != null)
                Destroy(ghostInstance);

            CreateGhost();
        }
    }
}
