using Client.Visuals;
using Shared;
using Shared.Data;
using Shared.Grid;
using Shared.Runtime;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Client.Controllers
{
    public class TowerPlacementController : MonoBehaviour
    {
        private const string LogPrefix = "[Placement]";

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
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsClient)
                return;

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

            if (PhaseManager.Instance == null || PhaseManager.Instance.CurrentPhase != GamePhase.Building)
            {
                ghostInstance.SetActive(false);
                currentGridPos = null;
                return;
            }

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

            var result = EvaluateClientPlacement(currentGridPos.Value, currentTowerConfig);
            if (result != PlacementResult.Success && result != PlacementResult.OutOfEnergyRange)
            {
                ghostInstance.SetActive(false);
                return;
            }

            ghostInstance.SetActive(true);
            ghostInstance.transform.position = GridManager.TowerWorldPos(currentGridPos.Value, currentTowerConfig);

            UpdateGhostEnergyIndicator(result == PlacementResult.Success);
        }

        private void UpdateGhostEnergyIndicator(bool canConnect)
        {
            if (currentTowerConfig == null || currentGridPos == null)
                return;

            ghostPropertyBlock.SetColor(BaseColorProperty, canConnect ? canConnectColor : cannotConnectColor);

            foreach (var renderer in ghostRenderers)
                renderer.SetPropertyBlock(ghostPropertyBlock);
        }

        private Vector2Int? GetMouseGridPosition()
        {
            if (Mouse.current == null)
                return null;

            var mousePos = Mouse.current.position.ReadValue();
            var ray = mainCamera.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0));
            var groundPlane = new Plane(Vector3.up, 0);

            if (!groundPlane.Raycast(ray, out var distance))
                return null;

            return gridManager.WorldToGrid(ray.GetPoint(distance));
        }

        public void OnPlaceTower(InputAction.CallbackContext context)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsClient)
                return;

            if (!context.performed)
                return;

            if (currentGridPos is null || currentTowerConfig == null)
                return;

            if (IsPointerOverAnyUI())
                return;

            var result = EvaluateClientPlacement(currentGridPos.Value, currentTowerConfig);
            if (result != PlacementResult.Success && result != PlacementResult.OutOfEnergyRange)
            {
                Debug.Log($"{LogPrefix} Client prevented placement for '{currentTowerConfig.Id}' at {currentGridPos.Value}: {result}.");
                return;
            }

            towerSpawnSystem.RequestPlaceTowerServerRpc(currentTowerConfig.Id, currentGridPos.Value);
        }

        private PlacementResult EvaluateClientPlacement(Vector2Int gridPos, TowerType towerConfig)
        {
            if (gridManager == null || towerSpawnSystem == null || PhaseManager.Instance == null)
                return PlacementResult.MissingDependencies;

            if (towerConfig == null || towerConfig.Prefab == null || towerConfig.ClassType == null)
                return PlacementResult.InvalidTowerType;

            if (PhaseManager.Instance.CurrentPhase != GamePhase.Building)
                return PlacementResult.OutOfBuildPhase;

            if (!gridManager.IsValidPosition(gridPos))
                return PlacementResult.InvalidGridPosition;

            if (!gridManager.IsCellAvailable(gridPos, towerConfig.Size, towerConfig.CanBePlacedOnWater))
                return PlacementResult.CellBlocked;

            var isInRange = IsInEnergyRangeFromRegistry(gridPos, towerConfig.ClassType, towerConfig.Stats.energyCost);
            return isInRange ? PlacementResult.Success : PlacementResult.OutOfEnergyRange;
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

        private static bool IsInEnergyRangeFromRegistry(Vector2Int pos, ClassType classType, int energyCost)
        {
            if (classType == null || ClientObjectRegistry.Instance == null)
                return false;

            var energies = ClientObjectRegistry.Instance.EnergyNodes;
            foreach (var kvp in energies)
            {
                var energy = kvp.Value;
                if (energy == null || !energy.IsSpawned) continue;
                if (!energy.CanConnectClass(classType) || !energy.HasCapacity(energyCost)) continue;
                if (Vector2Int.Distance(pos, energy.GridPosition) <= energy.EnergyRange)
                    return true;
            }

            var towers = ClientObjectRegistry.Instance.Towers;
            foreach (var kvp in towers)
            {
                var antenna = kvp.Value;
                if (antenna == null || !antenna.IsSpawned || !antenna.IsPowered) continue;
                if (antenna.Config == null || !antenna.Config.IsAntenna) continue;

                var range = antenna.Config.Stats.antennaRange;
                if (Vector2Int.Distance(pos, antenna.GridPosition) > range) continue;

                var sourceEnergyId = antenna.ConnectedEnergyId;
                if (sourceEnergyId == ulong.MaxValue) continue;

                if (energies.TryGetValue(sourceEnergyId, out var energy) && energy != null
                    && energy.IsSpawned
                    && energy.CanConnectClass(classType)
                    && energy.HasCapacity(energyCost))
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
