using Data;
using Managers;
using Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Controllers
{
    public class TowerPlacementController : MonoBehaviour
    {
        [SerializeField]
        private GridManager gridManager;
        [SerializeField]
        private TowerSpawnSystem towerSpawnSystem;
        [SerializeField]
        private Camera mainCamera;
        [SerializeField]
        private TowerType currentTowerConfig;
        [SerializeField]
        private Color canConnectColor = new(0.2f, 0.8f, 0.2f, 0.6f);
        [SerializeField]
        private Color cannotConnectColor = new(0.8f, 0.2f, 0.2f, 0.6f);

        private GameObject ghostInstance;
        private Renderer[] ghostRenderers;
        private MaterialPropertyBlock ghostPropertyBlock;
        private Vector2Int? currentGridPos;
        private bool isInRange;
        private Vector3 cachedGhostOffset;

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
            {
                return;
            }

            var prefabRuntime = currentTowerConfig.Prefab.GetComponent<TowerRuntime>();
            cachedGhostOffset = prefabRuntime?.PlacementOffset ?? Vector3.zero;

            ghostInstance = Instantiate(currentTowerConfig.Prefab, Vector3.zero, Quaternion.identity);
            ghostInstance.name = "TowerGhost";

            foreach (var mb in ghostInstance.GetComponentsInChildren<MonoBehaviour>())
            {
                if (mb != this)
                {
                    mb.enabled = false;
                }
            }

            foreach (var col in ghostInstance.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            foreach (var rb in ghostInstance.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = true;
            }

            ghostRenderers = ghostInstance.GetComponentsInChildren<Renderer>();

            ghostInstance.SetActive(false);
        }

        private void UpdateGhostState()
        {
            var gridPos = GetMouseGridPosition();
            currentGridPos = gridPos;

            if (gridPos is null)
            {
                ghostInstance.SetActive(false);
                return;
            }

            var towerSize = currentTowerConfig?.Size ?? Vector2Int.one;
            var canPlaceOnWater = currentTowerConfig?.CanBePlacedOnWater ?? false;
            var isValidPlacement = gridManager.IsCellAvailable(currentGridPos.Value, towerSize, canPlaceOnWater);

            if (!isValidPlacement)
            {
                ghostInstance.SetActive(false);
                return;
            }

            var basePos = gridManager.GridToWorld(currentGridPos.Value, towerSize, 0f);
            var worldPos = basePos + cachedGhostOffset;
            ghostInstance.SetActive(true);
            ghostInstance.transform.position = worldPos;

            UpdateGhostEnergyIndicator();
        }

        private void UpdateGhostEnergyIndicator()
        {
            if (currentTowerConfig == null || currentGridPos == null)
            {
                return;
            }

            var classType = currentTowerConfig.ClassType;
            var gridPos = currentGridPos.Value;

            isInRange = IsGridPositionPowered(gridPos, classType);
            var tintColor = isInRange ? canConnectColor : cannotConnectColor;

            ghostPropertyBlock.SetColor(BaseColorProperty, tintColor);

            foreach (var renderer in ghostRenderers)
            {
                renderer.SetPropertyBlock(ghostPropertyBlock);
            }
        }

        private bool IsGridPositionPowered(Vector2Int gridPos, ClassType classType)
        {
            foreach (var energy in FindObjectsByType<EnergyRuntime>(FindObjectsSortMode.None))
            {
                if (classType != null && !classType.CanConnectTo(energy.Config))
                {
                    continue;
                }

                if (Vector2Int.Distance(gridPos, energy.GridPosition) <= energy.EnergyRange)
                {
                    return true;
                }
            }

            foreach (var tower in FindObjectsByType<TowerRuntime>(FindObjectsSortMode.None))
            {
                var config = tower.GetConfig();
                if (config == null || config.AntennaRange <= 0 || !tower.IsPowered)
                {
                    continue;
                }

                if (Vector2Int.Distance(gridPos, tower.GridPosition) <= config.AntennaRange)
                {
                    return true;
                }
            }

            return false;
        }

        private Vector2Int? GetMouseGridPosition()
        {
            var mousePos = Mouse.current.position.ReadValue();
            var ray = mainCamera.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0));
            var groundPlane = new Plane(Vector3.up, 0);

            if (!groundPlane.Raycast(ray, out var distance))
            {
                return null;
            }

            var worldPos = ray.GetPoint(distance);
            return gridManager.WorldToGrid(worldPos);
        }

        public void OnPlaceTower(InputAction.CallbackContext context)
        {
            if (!context.performed || currentGridPos is null || !currentTowerConfig)
            {
                return;
            }

            var canPlaceOnWater = currentTowerConfig.CanBePlacedOnWater;
            if (!gridManager.IsCellAvailable(currentGridPos.Value, currentTowerConfig.Size, canPlaceOnWater))
            {
                return;
            }

            if (towerSpawnSystem == null)
            {
                Debug.LogError("[Placement] TowerSpawnSystem reference is null! Assign it in the Inspector.");
                return;
            }

            Debug.Log($"[Placement] Requesting {currentTowerConfig.Id} at {currentGridPos.Value}");
            towerSpawnSystem.RequestPlaceTowerServerRpc(currentTowerConfig.Id, currentGridPos.Value);
        }

        public void SetTowerConfig(TowerType config)
        {
            currentTowerConfig = config;

            if (ghostInstance != null)
            {
                Destroy(ghostInstance);
            }

            CreateGhost();
        }
    }
}