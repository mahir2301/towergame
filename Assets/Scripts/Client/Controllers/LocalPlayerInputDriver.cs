using Shared;
using Shared.Runtime;
using Shared.Utilities;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Controllers
{
    public class LocalPlayerInputDriver : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private TowerPlacementController placementController;
        [SerializeField] private float zoomMin = 3f;
        [SerializeField] private float zoomMax = 12f;
        [SerializeField] private float zoomSensitivity = 50f;
        [SerializeField] private float zoomAcceleration = 10f;

        private readonly Matrix4x4 isoMatrix = Matrix4x4.Rotate(Quaternion.Euler(0f, 45f, 0f));
        private bool jumpPressedThisFrame;
        private float targetZoom;
        private PlayerRuntime currentPlayer;

        private void OnEnable()
        {
            GameEvents.PlayerActionResultReceived += HandlePlayerActionResultReceived;
            ClientEvents.LocalPlayerChanged += HandleLocalPlayerChanged;
        }

        private void OnDisable()
        {
            GameEvents.PlayerActionResultReceived -= HandlePlayerActionResultReceived;
            ClientEvents.LocalPlayerChanged -= HandleLocalPlayerChanged;
        }

        private void Start()
        {
            if (cinemachineCamera != null)
                targetZoom = cinemachineCamera.Lens.OrthographicSize;

            currentPlayer = ClientEvents.CurrentLocalPlayer;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null || mainCamera == null)
                return;

            HandleZoom(mouse);

            if (currentPlayer == null)
                return;

            if (keyboard.spaceKey.wasPressedThisFrame)
                jumpPressedThisFrame = true;

            var move = ReadMoveInput(keyboard);
            var lookTarget = ReadLookTarget(mouse.position.ReadValue());
            currentPlayer.SubmitMoveCommand(move, lookTarget, jumpPressedThisFrame);
            jumpPressedThisFrame = false;

            HandleActions(keyboard, mouse, currentPlayer, lookTarget);
        }

        private void HandleLocalPlayerChanged(PlayerRuntime player)
        {
            currentPlayer = player;
        }

        private void HandleZoom(Mouse mouse)
        {
            if (cinemachineCamera == null || mouse == null)
                return;

            var scrollInput = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollInput) > 0.01f)
            {
                targetZoom -= scrollInput * zoomSensitivity * 0.01f;
                targetZoom = Mathf.Clamp(targetZoom, zoomMin, zoomMax);
            }

            var currentSize = cinemachineCamera.Lens.OrthographicSize;
            var newSize = Mathf.Lerp(currentSize, targetZoom, Time.deltaTime * zoomAcceleration);
            if (!Mathf.Approximately(currentSize, newSize))
            {
                var lens = cinemachineCamera.Lens;
                lens.OrthographicSize = newSize;
                cinemachineCamera.Lens = lens;
            }
        }

        private void HandleActions(Keyboard keyboard, Mouse mouse, PlayerRuntime player, Vector3 lookTarget)
        {
            var phaseManager = PhaseManager.Instance;
            if (phaseManager == null)
                return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (phaseManager.CurrentPhase == GamePhase.Building)
                    placementController?.TryPlaceTower();
                else if (phaseManager.CurrentPhase == GamePhase.Combat)
                    player.SubmitFireCommand(lookTarget);
            }

            if (phaseManager.CurrentPhase == GamePhase.Combat)
                HandleWeaponSwitch(keyboard, player);
        }

        private Vector2 ReadMoveInput(Keyboard keyboard)
        {
            var x = 0f;
            var y = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;

            var raw = new Vector3(x, 0f, y);
            var isometric = isoMatrix.MultiplyPoint3x4(raw);
            var planar = new Vector2(isometric.x, isometric.z);
            return planar.sqrMagnitude > 1f ? planar.normalized : planar;
        }

        private Vector3 ReadLookTarget(Vector2 mousePosition)
        {
            var ray = mainCamera.ScreenPointToRay(new Vector3(mousePosition.x, mousePosition.y, 0f));
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out var distance))
                return ray.GetPoint(distance);

            return Vector3.zero;
        }

        private static void HandleWeaponSwitch(Keyboard keyboard, PlayerRuntime player)
        {
            if (keyboard.digit1Key.wasPressedThisFrame)
                player.SubmitSwitchWeaponCommand(0);
            else if (keyboard.digit2Key.wasPressedThisFrame)
                player.SubmitSwitchWeaponCommand(1);
            else if (keyboard.digit3Key.wasPressedThisFrame)
                player.SubmitSwitchWeaponCommand(2);
        }

        private static void HandlePlayerActionResultReceived(PlayerRuntime _, PlayerActionKind actionKind,
            PlayerActionResult result)
        {
            if (result == PlayerActionResult.Accepted)
                return;

            RuntimeLog.Entity.Warning(RuntimeLog.Code.EntityActionRejected,
                $"Server rejected action {actionKind}: {result}.");
        }

        public bool HasRequiredReferences(out string issue)
        {
            return ReferenceValidator.Validate(out issue,
                (mainCamera, nameof(mainCamera)),
                (cinemachineCamera, nameof(cinemachineCamera)),
                (placementController, nameof(placementController)));
        }
    }
}
