using System;
using Shared.Data;
using Shared.Grid;
using UnityEngine;

namespace Shared.Runtime
{
    public static class PlacementValidator
    {
        public static PlacementResponse ValidatePlacement(Vector2Int gridPos, PlaceableType placeableType, GridManager gridManager,
            PhaseManager phaseManager, Func<Vector2Int, PlaceableType, bool> rangeEvaluator)
        {
            var typeId = placeableType != null ? placeableType.Id : string.Empty;

            if (gridManager == null || phaseManager == null || rangeEvaluator == null)
                return PlacementResponse.Create(typeId, gridPos, false, PlacementCodes.MissingDependencies);

            if (placeableType == null || placeableType.Prefab == null)
                return PlacementResponse.Create(typeId, gridPos, false, PlacementCodes.InvalidType);

            if (phaseManager.CurrentPhase != GamePhase.Building)
                return PlacementResponse.Create(typeId, gridPos, false, PlacementCodes.PhaseBlocked);

            if (!gridManager.IsValidPosition(gridPos))
                return PlacementResponse.Create(typeId, gridPos, false, PlacementCodes.InvalidGridPosition);

            if (!gridManager.IsCellAvailable(gridPos, placeableType.Size, placeableType.AllowedTileTypes))
                return PlacementResponse.Create(typeId, gridPos, false, PlacementCodes.CellBlocked);

            var isInRange = rangeEvaluator(gridPos, placeableType);
            return isInRange
                ? PlacementResponse.Create(typeId, gridPos, true, PlacementCodes.Success)
                : PlacementResponse.Create(typeId, gridPos, false, PlacementCodes.OutOfRange);
        }

        public static bool IsPlacementAllowed(PlacementResponse response, bool allowOutOfRange)
        {
            return response.Accepted
                   || (allowOutOfRange && response.Code.ToString() == PlacementCodes.OutOfRange);
        }
    }
}
