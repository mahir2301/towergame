using Client.Visuals;
using Shared;
using Shared.Data;
using Shared.Grid;
using Shared.Runtime;
using Shared.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Client.Controllers
{
    public class TowerPlacementController : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;
        [SerializeField] private PlaceableSpawnSystem placeableSpawnSystem;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private PlaceableType currentPlaceableType;
        [SerializeField] private Color canConnectColor = new(0.2f, 0.8f, 0.2f, 0.6f);
        [SerializeField] private Color cannotConnectColor = new(0.8f, 0.2f, 0.2f, 0.6f);

        private GameObject ghostInstance;
        private Renderer[] ghostRenderers;
        private MaterialPropertyBlock ghostPropertyBlock;
        private Vector2Int? currentGridPos;
        private readonly SubscriptionGroup subscriptions = new();

        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        private void Start()
        {
            ghostPropertyBlock = new MaterialPropertyBlock();
            CreateGhost();
        }

        private void OnEnable()
        {
            subscriptions.Add(
                () => ClientEvents.PlacementResponseReceived += HandlePlacementResponseReceived,
                () => ClientEvents.PlacementResponseReceived -= HandlePlacementResponseReceived);
        }

        private void OnDisable()
        {
            subscriptions.UnbindAll();
        }

        private void Update()
        {
            if (!RuntimeNet.ShouldRunNetworkedClientSystems())
                return;

            if (!RuntimeBootstrap.IsReady)
            {
                if (ghostInstance != null)
                    ghostInstance.SetActive(false);
                return;
            }

            UpdateGhostState();
        }

        private void CreateGhost()
        {
            if (currentPlaceableType?.Prefab == null)
                return;

            ghostInstance = Instantiate(currentPlaceableType.Prefab, Vector3.zero, Quaternion.identity);
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

            if (gridPos is null || currentPlaceableType == null)
            {
                ghostInstance.SetActive(false);
                return;
            }

            var response = EvaluateClientPlacement(currentGridPos.Value, currentPlaceableType);
            if (!PlacementValidator.IsPlacementAllowed(response, allowOutOfRange: true))
            {
                ghostInstance.SetActive(false);
                return;
            }

            ghostInstance.SetActive(true);
            ghostInstance.transform.position = GridManager.PlaceableWorldPos(currentGridPos.Value, currentPlaceableType);

            UpdateGhostEnergyIndicator(response.Accepted);
        }

        private void UpdateGhostEnergyIndicator(bool canConnect)
        {
            if (currentPlaceableType == null || currentGridPos == null)
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
            if (!RuntimeNet.ShouldRunNetworkedClientSystems())
                return;

            if (!RuntimeBootstrap.IsReady)
                return;

            if (!context.performed)
                return;

            TryPlaceTower();
        }

        public void TryPlaceTower()
        {
            if (!RuntimeNet.ShouldRunNetworkedClientSystems())
                return;

            if (!RuntimeBootstrap.IsReady)
                return;

            if (currentGridPos is null || currentPlaceableType == null)
                return;

            if (IsPointerOverAnyUI())
                return;

            var response = EvaluateClientPlacement(currentGridPos.Value, currentPlaceableType);
            if (!PlacementValidator.IsPlacementAllowed(response, allowOutOfRange: true))
            {
                RuntimeLog.Placement.Info(RuntimeLog.Code.PlacementClientBlocked,
                    $"Client prevented placement for '{currentPlaceableType.Id}' at {currentGridPos.Value}: {response.Code}.");
                return;
            }

            if (placeableSpawnSystem == null)
                return;

            placeableSpawnSystem.RequestPlaceableServerRpc(currentPlaceableType.Id, currentGridPos.Value);
        }

        private PlacementResponse EvaluateClientPlacement(Vector2Int gridPos, PlaceableType placeableType)
        {
            return PlacementValidator.ValidatePlacement(gridPos, placeableType, gridManager, PhaseManager.Instance,
                IsInEnergyRangeFromRegistry);
        }

        private static void HandlePlacementResponseReceived(PlacementResponse response)
        {
            if (PlacementValidator.IsPlacementAllowed(response, allowOutOfRange: true))
            {
                RuntimeLog.Placement.Info(RuntimeLog.Code.PlacementServerResult,
                    $"Server accepted placement for '{response.PlaceableTypeId}' at {response.GridPos} with result {response.Code}.");
                return;
            }

            RuntimeLog.Placement.Warning(RuntimeLog.Code.PlacementServerResult,
                $"Server rejected placement for '{response.PlaceableTypeId}' at {response.GridPos}: {response.Code}.");
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

        private static bool IsInEnergyRangeFromRegistry(Vector2Int pos, PlaceableType placeableType)
        {
            var towerRuntime = placeableType != null ? placeableType.Prefab?.GetComponent<TowerRuntime>() : null;
            var classType = towerRuntime != null ? towerRuntime.ClassType : null;
            var energyCost = towerRuntime != null ? towerRuntime.EnergyCost : 0;

            if (classType == null || ClientObjectRegistry.Instance == null)
                return true;

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
                var tower = kvp.Value;
                if (tower == null || !tower.IsSpawned || !tower.IsPowered) continue;
                if (!tower.TryGetComponent<AntennaRuntime>(out var antenna)) continue;

                var range = antenna.Range;
                if (Vector2Int.Distance(pos, tower.GridPosition) > range) continue;

                var sourceEnergyId = tower.ConnectedEnergyId;
                if (sourceEnergyId == ulong.MaxValue) continue;

                if (energies.TryGetValue(sourceEnergyId, out var energy) && energy != null
                    && energy.IsSpawned
                    && energy.CanConnectClass(classType)
                    && energy.HasCapacity(energyCost))
                    return true;
            }

            return false;
        }

        public void SetPlaceableType(PlaceableType type)
        {
            currentPlaceableType = type;

            if (ghostInstance != null)
                Destroy(ghostInstance);

            CreateGhost();
        }

    }
}
