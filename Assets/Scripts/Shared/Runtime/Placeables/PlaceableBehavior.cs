using Shared.Data;
using Shared.Grid;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Runtime.Placeables
{
    public abstract class PlaceableBehavior : NetworkBehaviour
    {
        [SerializeField] private string placeableTypeId;

        private Vector2Int gridPosition;

        public string PlaceableTypeId => placeableTypeId;
        public Vector2Int GridPosition => gridPosition;
        public PlaceableType Type => GameRegistry.Instance?.GetPlaceableType(placeableTypeId);

        public virtual void Initialize(PlaceableType type, Vector2Int gridPos)
        {
            placeableTypeId = type != null ? type.Id : string.Empty;
            gridPosition = gridPos;
        }

        public virtual bool CanPlace(GridManager grid, Vector2Int gridPos, out string code)
        {
            code = PlacementCodes.Success;
            return true;
        }

        public virtual void OnPlaced()
        {
        }

        public virtual void OnRemoved()
        {
        }

        public virtual void ServerTick(float deltaTime)
        {
        }
    }
}
