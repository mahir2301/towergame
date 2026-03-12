using UnityEngine;

public class Tower : MonoBehaviour
{
    private new Renderer renderer;

    private void Start()
    {
        renderer = GetComponent<Renderer>();
    }

    public void SetValid(bool isValid)
    {
        renderer.material.color = isValid ? Color.green : Color.red;
    }
}
