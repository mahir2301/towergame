using System;
using System.Collections.Generic;
using Shared.Data;
using Unity.Netcode;
using UnityEngine;

namespace Shared.Runtime
{
    public class PlayerRuntime : NetworkBehaviour
    {
        public static PlayerRuntime LocalPlayer { get; private set; }
        public static event Action<PlayerRuntime> LocalPlayerSpawned;
        public static event Action<PlayerRuntime> LocalPlayerDespawned;

        private readonly NetworkVariable<int> currentWeaponIndex = new(-1);
        private readonly NetworkVariable<ulong> connectedEnergyId = new(ulong.MaxValue);
        private readonly NetworkVariable<float> currentWeaponCooldown = new(0f);

        private readonly List<WeaponInstance> weaponInstances = new();

        public int CurrentWeaponIndex => currentWeaponIndex.Value;
        public ulong ConnectedEnergyId => connectedEnergyId.Value;
        public float CurrentWeaponCooldown => currentWeaponCooldown.Value;
        public IReadOnlyList<WeaponInstance> WeaponInstances => weaponInstances;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                LocalPlayer = this;
                LocalPlayerSpawned?.Invoke(this);
            }

            if (IsServer)
            {
                InitializeWeapons();
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (IsOwner && LocalPlayer == this)
            {
                LocalPlayer = null;
                LocalPlayerDespawned?.Invoke(this);
            }
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;

            foreach (var weapon in weaponInstances)
            {
                weapon.TickCooldown(Time.deltaTime);
            }

            var currentWeapon = GetCurrentWeapon();
            currentWeaponCooldown.Value = currentWeapon?.CurrentCooldown ?? 0f;
        }

        private void InitializeWeapons()
        {
            var registry = GameRegistry.Instance;
            if (registry == null) return;

            var weaponTypes = registry.WeaponTypes;

            for (var i = 0; i < weaponTypes.Count && i < 3; i++)
            {
                var weapon = weaponTypes[i];
                if (weapon == null) continue;

                var instance = new WeaponInstance(weapon);
                weaponInstances.Add(instance);
            }

            if (weaponInstances.Count > 0)
                currentWeaponIndex.Value = 0;
        }

        public WeaponInstance GetCurrentWeapon()
        {
            var index = currentWeaponIndex.Value;
            if (index < 0 || index >= weaponInstances.Count)
                return null;
            return weaponInstances[index];
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SwitchWeaponServerRpc(int index)
        {
            if (!IsServer) return;
            if (index < 0 || index >= weaponInstances.Count) return;

            currentWeaponIndex.Value = index;
            GameEvents.RaiseWeaponSwitched(this, index);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void FireWeaponServerRpc(Vector3 targetPosition)
        {
            if (!IsServer) return;

            var weapon = GetCurrentWeapon();
            if (weapon == null || !weapon.CanFire()) return;

            var config = weapon.GetConfig();
            if (config == null) return;

            if (!TryConsumeEnergy(config.Stats.energyCostPerShot, config.ClassType))
                return;

            weapon.StartCooldown();

            var direction = (targetPosition - transform.position).normalized;
            direction.y = 0f;

            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);

            SpawnProjectile(targetPosition, config);
            FireWeaponClientRpc(targetPosition, config.Id);
        }

        private void SpawnProjectile(Vector3 targetPosition, WeaponType config)
        {
            if (config.ProjectilePrefab == null) return;

            var startPos = transform.position + Vector3.up * 1.5f;
            var projectileObj = Instantiate(config.ProjectilePrefab, startPos, Quaternion.identity);
            var netObj = projectileObj.GetComponent<NetworkObject>();

            if (netObj != null)
            {
                netObj.Spawn();
                var projectile = projectileObj.GetComponent<Projectile>();
                projectile?.Initialize(targetPosition, config.Stats.projectileSpeed, config.Stats.damage);
            }
            else
            {
                Destroy(projectileObj);
            }
        }

        [ClientRpc]
        private void FireWeaponClientRpc(Vector3 targetPosition, string weaponId)
        {
            if (!IsOwner) return;
            GameEvents.RaiseWeaponFired(this, targetPosition, weaponId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ConnectToEnergyServerRpc(ulong energyId)
        {
            if (!IsServer) return;

            if (energyId == ulong.MaxValue)
            {
                connectedEnergyId.Value = ulong.MaxValue;
                return;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(energyId, out var obj))
                return;

            var energy = obj.GetComponent<EnergyRuntime>();
            if (energy == null || !energy.IsSpawned)
                return;

            connectedEnergyId.Value = energyId;
        }

        private bool TryConsumeEnergy(int amount, ClassType classType)
        {
            if (amount <= 0) return true;
            if (connectedEnergyId.Value == ulong.MaxValue) return false;

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(connectedEnergyId.Value, out var obj))
                return false;

            var energy = obj.GetComponent<EnergyRuntime>();
            if (energy == null || !energy.IsSpawned) return false;
            if (!energy.CanConnectClass(classType)) return false;
            if (!energy.HasCapacity(amount)) return false;

            return energy.TryConnectTower(NetworkObjectId, amount);
        }
    }
}
