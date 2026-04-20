using System;
using Shared.Entities;
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
        public static event Action<PlayerRuntime, int> WeaponSwitched;
        public static event Action<PlayerRuntime, PlayerActionKind, PlayerActionResult> PlayerActionResultReceived;
        public static event Action<EntityRuntime> EntitySpawned;
        public static event Action<EntityRuntime> EntityDespawned;
        public static event Action<EntityRuntime, ulong> EntityOwnerAssigned;

        public static void RaiseEnergySpawned(EnergyRuntime energy) => EnergySpawned?.Invoke(energy);
        public static void RaiseEnergyDespawned(EnergyRuntime energy) => EnergyDespawned?.Invoke(energy);
        public static void RaiseTowerSpawned(TowerRuntime tower) => TowerSpawned?.Invoke(tower);
        public static void RaiseTowerDespawned(TowerRuntime tower) => TowerDespawned?.Invoke(tower);
        public static void RaisePhaseChanged(GamePhase phase) => PhaseChanged?.Invoke(phase);
        public static void RaiseWeaponFired(PlayerRuntime player, Vector3 target, string weaponId) => WeaponFired?.Invoke(player, target, weaponId);
        public static void RaiseWeaponSwitched(PlayerRuntime player, int weaponIndex) => WeaponSwitched?.Invoke(player, weaponIndex);
        public static void RaisePlayerActionResultReceived(PlayerRuntime player, PlayerActionKind kind, PlayerActionResult result) => PlayerActionResultReceived?.Invoke(player, kind, result);
        public static void RaiseEntitySpawned(EntityRuntime entity) => EntitySpawned?.Invoke(entity);
        public static void RaiseEntityDespawned(EntityRuntime entity) => EntityDespawned?.Invoke(entity);
        public static void RaiseEntityOwnerAssigned(EntityRuntime entity, ulong ownerClientId) => EntityOwnerAssigned?.Invoke(entity, ownerClientId);
    }
}
