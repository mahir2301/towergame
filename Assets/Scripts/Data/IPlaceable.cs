using UnityEngine;

namespace Data
{
    public interface IPlaceable
    {
        Vector2Int GridPosition { get; set; }
        Vector2Int Size { get; }
        bool CanBePlacedOnWater { get; }
        void Initialize(Vector2Int gridPos);
    }
}