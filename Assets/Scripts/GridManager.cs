using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    private readonly Dictionary<Vector2Int, GameObject> grid = new();


    public Vector2Int WorldToGrid(Vector3 vector)
    {
        return new Vector2Int(Mathf.RoundToInt(vector.x), Mathf.RoundToInt(vector.z));
    }

    public Vector3 GridToWorld(Vector2Int vector)
    {
        return new Vector3(vector.x, 0, vector.y);
    }

    public bool TryPlaceTower(Vector3 vector, GameObject tower)
    {
        var gridPosition = WorldToGrid(vector);
        
        if (grid.ContainsKey(gridPosition))
        {
            return false;
        }

        var worldPosition = GridToWorld(gridPosition);
        var instance = Instantiate(tower, worldPosition, Quaternion.identity);
        
        grid.Add(gridPosition, instance);

        return true;
    }

    public bool IsTileAvailable(Vector3 vector)
    {
        var gridPosition = WorldToGrid(vector);

        return !grid.ContainsKey(gridPosition);
    }
}
