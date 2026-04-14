using System;
using Shared.Runtime;
using UnityEngine;

namespace Shared
{
    public static class GameEvents
    {
        public static event Action<EnergyRuntime> EnergySpawned;
        public static event Action<EnergyRuntime> EnergyDespawned;
        public static event Action<TowerRuntime> TowerSpawned;
        public static event Action<TowerRuntime> TowerDespawned;
        public static event Action<GamePhase> PhaseChanged;
        public static event Action<PlayerRuntime, Vector3, string> WeaponFired;

        public static void RaiseEnergySpawned(EnergyRuntime energy) => EnergySpawned?.Invoke(energy);
        public static void RaiseEnergyDespawned(EnergyRuntime energy) => EnergyDespawned?.Invoke(energy);
        public static void RaiseTowerSpawned(TowerRuntime tower) => TowerSpawned?.Invoke(tower);
        public static void RaiseTowerDespawned(TowerRuntime tower) => TowerDespawned?.Invoke(tower);
        public static void RaisePhaseChanged(GamePhase phase) => PhaseChanged?.Invoke(phase);
        public static void RaiseWeaponFired(PlayerRuntime player, Vector3 target, string weaponId) => WeaponFired?.Invoke(player, target, weaponId);
    }
}
