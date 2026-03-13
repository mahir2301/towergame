using Data;
using Managers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Controllers
{
    public class TowerPlacementController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private GridManager gridManager;
        [SerializeField]
        private Camera mainCamera;
        [SerializeField]
        private TowerConfig currentTowerConfig;

        private GameObject ghostInstance;
        private bool isValidPlacement;
        private Vector2Int currentGridPos;
        private bool isMouseOverGrid;

        private void Start()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            CreateGhost();
        }

        private void Update()
        {
            UpdateGhostState();
        }

        private void CreateGhost()
        {
            if (currentTowerConfig?.towerPrefab == null)
            {
                return;
            }

            ghostInstance = Instantiate(currentTowerConfig.towerPrefab, Vector3.zero, Quaternion.identity);
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

            ghostInstance.SetActive(false);
        }

        private void UpdateGhostState()
        {
            var gridPos = GetMouseGridPosition(out var isOverGround);

            if (!isOverGround)
            {
                if (!ghostInstance || !ghostInstance.activeSelf)
                {
                    return;
                }

                ghostInstance.SetActive(false);
                isMouseOverGrid = false;

                return;
            }

            isMouseOverGrid = true;

            if (gridPos == currentGridPos && ghostInstance.activeSelf)
            {
                return;
            }

            currentGridPos = gridPos;

            var towerSize = currentTowerConfig?.gridSize ?? Vector2Int.one;
            isValidPlacement = gridManager.IsCellAvailable(currentGridPos, towerSize);

            ghostInstance.SetActive(isValidPlacement);

            if (!isValidPlacement)
            {
                return;
            }

            var worldPos = gridManager.GridToWorld(currentGridPos, towerSize);
            ghostInstance.transform.position = worldPos;
        }

        private Vector2Int GetMouseGridPosition(out bool isOverGround)
        {
            isOverGround = false;
            var mousePos = Mouse.current.position.ReadValue();
            var ray = mainCamera.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0));

            var groundPlane = new Plane(Vector3.up, 0);

            if (!groundPlane.Raycast(ray, out var distance))
            {
                return Vector2Int.zero;
            }

            var worldPos = ray.GetPoint(distance);
            isOverGround = true;
            return gridManager.WorldToGrid(worldPos);
        }

        public void OnPlaceTower(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }

            if (!isMouseOverGrid || !isValidPlacement || currentTowerConfig == null)
            {
                return;
            }

            if (gridManager.TryPlaceTower(currentGridPos, currentTowerConfig, out var instance))
            {
                Debug.Log($"Placed tower at {currentGridPos}");
            }
        }

        public void SetTowerConfig(TowerConfig config)
        {
            currentTowerConfig = config;

            if (ghostInstance == null)
            {
                return;
            }

            Destroy(ghostInstance);
            CreateGhost();
        }
    }
}