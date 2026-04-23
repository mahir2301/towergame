using System;
using Shared.Data;
using Shared.Runtime;
using UnityEngine;

namespace Shared
{
    public static class ServerEvents
    {
        public static event Action<ulong, TowerType, Vector2Int, Action<PlacementResult>> PlaceTowerRequested;
        public static event Action<TowerRuntime> TowerSpawned;
        public static event Action<TowerRuntime> TowerDespawned;
        public static event Action<EnergyRuntime> EnergySpawned;
        public static event Action<EnergyRuntime> EnergyDespawned;
        public static event Action<NexusRuntime> NexusSpawned;
        public static event Action<NexusRuntime> NexusDespawned;

        public static bool TryRaisePlaceTowerRequested(ulong requesterClientId, TowerType config, Vector2Int gridPos,
            out PlacementResult result)
        {
            result = PlacementResult.MissingDependencies;
            if (PlaceTowerRequested == null)
                return false;

            PlacementResult? requestResult = null;
            PlaceTowerRequested.Invoke(requesterClientId, config, gridPos, r => requestResult = r);
            if (!requestResult.HasValue)
                return false;

            result = requestResult.Value;
            return true;
        }

        public static void RaiseTowerSpawned(TowerRuntime tower) => TowerSpawned?.Invoke(tower);
        public static void RaiseTowerDespawned(TowerRuntime tower) => TowerDespawned?.Invoke(tower);
        public static void RaiseEnergySpawned(EnergyRuntime energy) => EnergySpawned?.Invoke(energy);
        public static void RaiseEnergyDespawned(EnergyRuntime energy) => EnergyDespawned?.Invoke(energy);
        public static void RaiseNexusSpawned(NexusRuntime nexus) => NexusSpawned?.Invoke(nexus);
        public static void RaiseNexusDespawned(NexusRuntime nexus) => NexusDespawned?.Invoke(nexus);
    }
}
