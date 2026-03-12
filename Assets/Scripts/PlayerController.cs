using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Rigidbody rigidBody;

    private float movementX;
    private float movementY;

    [SerializeField]
    private float speed;

    private void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        var movementVector = new Vector3(movementX, 0, movementY);
        rigidBody.AddForce(movementVector * speed);
    }

    private void OnMove(InputValue input)
    {
        var movementVector = input.Get<Vector2>();
        movementX = movementVector.x;
        movementY = movementVector.y;
    }
}
