using System.Collections.Generic;
using Data;
using Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class EnergyNetworkManager : NetworkBehaviour
    {
        public static EnergyNetworkManager Instance { get; private set; }

        private readonly List<EnergyRuntime> energyRuntimes = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void RegisterEnergyRuntime(EnergyRuntime runtime)
        {
            energyRuntimes.Add(runtime);
        }

        public void UnregisterEnergyRuntime(EnergyRuntime runtime)
        {
            energyRuntimes.Remove(runtime);
        }

        public EnergyRuntime GetNearestCompatibleEnergy(Vector2Int position, ClassType classType, int energyCost)
        {
            EnergyRuntime nearest = null;
            var nearestDistance = float.MaxValue;

            foreach (var runtime in energyRuntimes)
            {
                if (!runtime.CanConnectClass(classType) || !runtime.HasCapacity(energyCost))
                {
                    continue;
                }

                var distance = Vector2Int.Distance(position, runtime.GridPosition);

                if (!(distance < nearestDistance))
                {
                    continue;
                }

                nearestDistance = distance;
                nearest = runtime;
            }

            return nearest;
        }

        public bool TryConnectTowerToEnergy(TowerRuntime tower, int energyCost)
        {
            if (!IsServer)
            {
                return false;
            }

            var config = tower.GetConfig();
            var energyRuntime = GetNearestCompatibleEnergy(tower.GridPosition, config.ClassType, energyCost);
            if (energyRuntime == null)
            {
                return false;
            }

            if (!energyRuntime.TryConnectTower(tower.NetworkObjectId, energyCost))
            {
                return false;
            }
            
            tower.SetPowered(true);
            return true;
        }
    }
}