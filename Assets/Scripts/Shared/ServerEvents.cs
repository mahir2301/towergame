using System;
using Shared.Data;
using Shared.Runtime;
using Shared.Runtime.Placeables;
using UnityEngine;

namespace Shared
{
    public static class ServerEvents
    {
        public static event Action<ulong, PlaceableType, Vector2Int, Action<PlacementResponse>> PlaceablePlacementRequested;
        public static event Action<PlaceableBehavior> PlaceableSpawned;
        public static event Action<PlaceableBehavior> PlaceableDespawned;

        public static bool TryRaisePlaceablePlacementRequested(ulong requesterClientId, PlaceableType type, Vector2Int gridPos,
            out PlacementResponse response)
        {
            response = PlacementResponse.Create(type != null ? type.Id : string.Empty, gridPos, false, PlacementCodes.MissingDependencies);
            if (PlaceablePlacementRequested == null)
                return false;

            PlacementResponse? requestResult = null;
            PlaceablePlacementRequested.Invoke(requesterClientId, type, gridPos, r => requestResult = r);
            if (!requestResult.HasValue)
                return false;

            response = requestResult.Value;
            return true;
        }

        public static void RaisePlaceableSpawned(PlaceableBehavior placeable) => PlaceableSpawned?.Invoke(placeable);
        public static void RaisePlaceableDespawned(PlaceableBehavior placeable) => PlaceableDespawned?.Invoke(placeable);
    }
}
