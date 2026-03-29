using Data;
using Managers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Controllers
{
    public class TowerPlacementController : MonoBehaviour
    {
        [SerializeField]
        private GridManager gridManager;
        [SerializeField]
        private Camera mainCamera;
        [SerializeField]
        private TowerType currentTowerConfig;

        private GameObject ghostInstance;
        private Vector2Int? currentGridPos;

        private void Start()
        {
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
            var isValidPlacement = gridManager.IsCellAvailable(currentGridPos.Value, towerSize);

            if (!isValidPlacement)
            {
                ghostInstance.SetActive(false);

                return;
            }

            var worldPos = gridManager.GridToWorld(currentGridPos.Value, towerSize, 2.5f);
            ghostInstance.SetActive(true);
            ghostInstance.transform.position = worldPos;
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
            Debug.Log(worldPos);
            return gridManager.WorldToGrid(worldPos);
        }

        public void OnPlaceTower(InputAction.CallbackContext context)
        {
            if (!context.performed || currentGridPos is null || !currentTowerConfig)
            {
                return;
            }

            if (gridManager.TryPlaceTowerRuntime(currentGridPos.Value, currentTowerConfig, out var towerRuntime))
            {
                Debug.Log($"Placed tower at {currentGridPos}");
            }
        }

        public void SetTowerConfig(TowerType config)
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