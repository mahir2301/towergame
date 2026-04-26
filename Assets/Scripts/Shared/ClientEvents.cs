using System;
using Shared.Runtime;
using UnityEngine;

namespace Shared
{
    public static class ClientEvents
    {
        public static event Action<PlacementResponse> PlacementResponseReceived;
        public static event Action<PlayerRuntime> LocalPlayerChanged;

        public static PlayerRuntime CurrentLocalPlayer { get; private set; }

        public static void RaisePlacementResponseReceived(PlacementResponse response)
            => PlacementResponseReceived?.Invoke(response);

        public static void RaiseLocalPlayerChanged(PlayerRuntime player)
        {
            if (CurrentLocalPlayer == player)
                return;

            CurrentLocalPlayer = player;
            LocalPlayerChanged?.Invoke(player);
        }
    }
}
