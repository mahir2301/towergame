using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Controllers
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField]
        private CinemachineCamera mainCamera;
        [SerializeField]
        private float movementSpeed;
        [SerializeField]
        private float movementAcceleration;
        [SerializeField]
        private float rotationSpeed;
        [SerializeField]
        private float zoomMin;
        [SerializeField]
        private float zoomMax;
        [SerializeField]
        private float zoomSensitivity;
        [SerializeField]
        private float zoomAcceleration;

        [SerializeField]
        private TowerPlacementController placementController;

        private Rigidbody rigidBody;
        private Camera realCamera;
        private Vector3 targetMovementVector;
        private Vector3 currentMovementVector;
        private float targetZoom;

        private readonly Matrix4x4 isoMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, 45, 0));

        private void Start()
        {
            rigidBody = GetComponent<Rigidbody>();
            realCamera = Camera.main;
            targetZoom = mainCamera.Lens.OrthographicSize;
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            var inputVector = context.ReadValue<Vector2>();
            var rawInput = new Vector3(inputVector.x, 0, inputVector.y);

            targetMovementVector = isoMatrix.MultiplyPoint3x4(rawInput);
        }

        public void OnPlaceTower(InputAction.CallbackContext context)
        {
            placementController.OnPlaceTower(context);
        }

        private void FixedUpdate()
        {
            HandleMovement();
            HandleRotation();
        }

        private void Update()
        {
            HandleZoom();
        }

        private void HandleMovement()
        {
            currentMovementVector = Vector3.Lerp(currentMovementVector, targetMovementVector,
                movementAcceleration * Time.fixedDeltaTime);

            var targetPosition = rigidBody.position + movementSpeed * Time.fixedDeltaTime * currentMovementVector;
            rigidBody.MovePosition(targetPosition);
        }

        private void HandleRotation()
        {
            var mousePos = Mouse.current.position.ReadValue();
            var ray = realCamera.ScreenPointToRay(mousePos);
            var groundPlane = new Plane(Vector3.up, rigidBody.position);

            if (!groundPlane.Raycast(ray, out var rayDistance))
            {
                return;
            }

            var targetPoint = ray.GetPoint(rayDistance);
            var lookDir = targetPoint - rigidBody.position;

            lookDir.y = 0;

            if (lookDir == Vector3.zero)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(lookDir);
            rigidBody.MoveRotation(
                Quaternion.Slerp(rigidBody.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        }

        private void HandleZoom()
        {
            var scrollInput = Mouse.current.scroll.ReadValue().y;

            if (Mathf.Abs(scrollInput) > 0.01f)
            {
                targetZoom -= scrollInput * zoomSensitivity * 0.01f;
                targetZoom = Mathf.Clamp(targetZoom, zoomMin, zoomMax);
            }

            mainCamera.Lens.OrthographicSize = Mathf.Lerp(mainCamera.Lens.OrthographicSize, targetZoom,
                Time.deltaTime * zoomAcceleration);
        }
    }
}