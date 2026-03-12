using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private Camera mainCamera;
    [SerializeField]
    private float movementSpeed;
    [SerializeField]
    private float movementAcceleration;
    [SerializeField]
    private float rotationSpeed;

    private Rigidbody rigidBody;
    private Vector3 targetMovementVector;
    private Vector3 currentMovementVector;

    private Matrix4x4 IsoMatrix => Matrix4x4.Rotate(Quaternion.Euler(0, 45, 0));

    private void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
        rigidBody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void OnMove(InputValue input)
    {
        var inputVector = input.Get<Vector2>();
        var rawInput = new Vector3(inputVector.x, 0, inputVector.y);

        targetMovementVector = IsoMatrix.MultiplyPoint3x4(rawInput);
    }

    private void FixedUpdate()
    {
        HandleMovement();
        HandleRotation();
    }

    private void HandleMovement()
    {
        currentMovementVector = Vector3.Lerp(currentMovementVector, targetMovementVector, movementAcceleration * Time.fixedDeltaTime);

        var targetPosition = rigidBody.position + movementSpeed * Time.fixedDeltaTime * currentMovementVector;
        rigidBody.MovePosition(targetPosition);
    }

    private void HandleRotation()
    {
        var mousePos = Mouse.current.position.ReadValue();
        var ray = mainCamera.ScreenPointToRay(mousePos);
        var groundPlane = new Plane(Vector3.up, rigidBody.position);

        if (groundPlane.Raycast(ray, out float rayDistance))
        {
            var targetPoint = ray.GetPoint(rayDistance);
            var lookDir = targetPoint - rigidBody.position;

            lookDir.y = 0;

            if (lookDir != Vector3.zero)
            {
                var targetRotation = Quaternion.LookRotation(lookDir);
                rigidBody.MoveRotation(Quaternion.Slerp(rigidBody.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
            }
        }
    }
}
