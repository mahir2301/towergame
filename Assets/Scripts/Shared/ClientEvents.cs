using System;
using Shared.Runtime;
using UnityEngine;

namespace Shared
{
    public static class ClientEvents
    {
        public static event Action<string, Vector2Int, PlacementResult> PlacementResultReceived;
        public static event Action<PlayerRuntime> LocalPlayerChanged;

        public static PlayerRuntime CurrentLocalPlayer { get; private set; }

        public static void RaisePlacementResultReceived(string towerConfigId, Vector2Int gridPos, PlacementResult result)
            => PlacementResultReceived?.Invoke(towerConfigId, gridPos, result);

        public static void RaiseLocalPlayerChanged(PlayerRuntime player)
        {
            if (CurrentLocalPlayer == player)
                return;

            CurrentLocalPlayer = player;
            LocalPlayerChanged?.Invoke(player);
        }
    }
}
