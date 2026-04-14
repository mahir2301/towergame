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

        [Header("Projectiles")]
        [SerializeField] private GameObject projectilePrefab;

        private readonly NetworkList<ulong> weaponNetIds = new();
        private readonly NetworkVariable<int> currentWeaponIndex = new(-1);
        private readonly NetworkVariable<ulong> connectedEnergyId = new(ulong.MaxValue);

        private readonly List<WeaponInstance> weaponInstances = new();

        public int CurrentWeaponIndex => currentWeaponIndex.Value;
        public ulong ConnectedEnergyId => connectedEnergyId.Value;
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
            if (!IsServer) return;

            foreach (var weapon in weaponInstances)
            {
                var before = weapon.CurrentCooldown;
                weapon.TickCooldown(Time.deltaTime);
                if (before > 0)
                    Debug.Log($"[PlayerRuntime] TickCooldown: {before:F2} -> {weapon.CurrentCooldown:F2}");
            }
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
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void FireWeaponServerRpc(Vector3 targetPosition)
        {
            Debug.Log($"[PlayerRuntime] FireWeaponServerRpc called, IsServer: {IsServer}");

            if (!IsServer) return;

            var weapon = GetCurrentWeapon();
            Debug.Log($"[PlayerRuntime] Weapon: {weapon?.ConfigId}, CanFire: {weapon?.CanFire()}");

            if (weapon == null || !weapon.CanFire())
            {
                Debug.Log("[PlayerRuntime] Cannot fire - weapon null or on cooldown");
                return;
            }

            var config = weapon.GetConfig();
            if (config == null)
            {
                Debug.Log("[PlayerRuntime] Config null");
                return;
            }

            Debug.Log($"[PlayerRuntime] Energy cost: {config.Stats.energyCostPerShot}");

            if (!TryConsumeEnergy(config.Stats.energyCostPerShot, config.ClassType))
            {
                Debug.Log("[PlayerRuntime] Energy consumption failed");
                return;
            }

            weapon.StartCooldown();
            Debug.Log($"[PlayerRuntime] Cooldown started: {weapon.CurrentCooldown}");

            var direction = (targetPosition - transform.position).normalized;
            direction.y = 0f;

            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);

            Debug.Log("[PlayerRuntime] Calling SpawnProjectile");
            SpawnProjectile(targetPosition, config.Stats.damage, config.Stats.range);
            FireWeaponClientRpc(targetPosition, config.Id);
        }

        private void SpawnProjectile(Vector3 targetPosition, float damage, float range)
        {
            Debug.Log($"[PlayerRuntime] SpawnProjectile called, prefab: {projectilePrefab != null}");

            if (projectilePrefab == null)
            {
                Debug.Log("[PlayerRuntime] No projectilePrefab assigned!");
                return;
            }

            var startPos = transform.position + Vector3.up * 1.5f;
            var projectileObj = Instantiate(projectilePrefab, startPos, Quaternion.identity);
            Debug.Log($"[PlayerRuntime] Projectile instantiated: {projectileObj.name}");

            var netObj = projectileObj.GetComponent<NetworkObject>();
            Debug.Log($"[PlayerRuntime] NetworkObject found: {netObj != null}");

            if (netObj != null)
            {
                Debug.Log($"[PlayerRuntime] Spawning NetworkObject...");
                netObj.Spawn();
                Debug.Log($"[PlayerRuntime] Spawned, IsSpawned: {netObj.IsSpawned}");

                var projectile = projectileObj.GetComponent<Projectile>();
                Debug.Log($"[PlayerRuntime] Projectile component found: {projectile != null}");

                if (projectile != null)
                {
                    projectile.Initialize(targetPosition, 30f, damage);
                }
            }
            else
            {
                Debug.Log("[PlayerRuntime] No NetworkObject, destroying in 3s");
                Destroy(projectileObj, 3f);
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
            if (amount <= 0)
                return true;

            if (connectedEnergyId.Value == ulong.MaxValue)
                return false;

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(connectedEnergyId.Value, out var obj))
                return false;

            var energy = obj.GetComponent<EnergyRuntime>();
            if (energy == null || !energy.IsSpawned)
                return false;

            if (!energy.CanConnectClass(classType))
                return false;

            if (!energy.HasCapacity(amount))
                return false;

            return energy.TryConnectTower(NetworkObjectId, amount);
        }
    }
}
