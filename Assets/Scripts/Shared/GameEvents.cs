using System;
using Shared.Runtime;

namespace Shared
{
    public static class GameEvents
    {
        public static event Action<EnergyRuntime> EnergySpawned;
        public static event Action<EnergyRuntime> EnergyDespawned;
        public static event Action<TowerRuntime> TowerSpawned;
        public static event Action<TowerRuntime> TowerDespawned;

        public static void RaiseEnergySpawned(EnergyRuntime energy) => EnergySpawned?.Invoke(energy);
        public static void RaiseEnergyDespawned(EnergyRuntime energy) => EnergyDespawned?.Invoke(energy);
        public static void RaiseTowerSpawned(TowerRuntime tower) => TowerSpawned?.Invoke(tower);
        public static void RaiseTowerDespawned(TowerRuntime tower) => TowerDespawned?.Invoke(tower);
    }
}
