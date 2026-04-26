using System;
using UnityEngine;

namespace Shared.Data
{
    [Serializable]
    public class WeaponInstance
    {
        [SerializeField] private WeaponType config;
        [SerializeField] private float currentCooldown;

        public WeaponType Config => config;
        public float CurrentCooldown => currentCooldown;

        public WeaponInstance(WeaponType weaponConfig)
        {
            config = weaponConfig;
            currentCooldown = 0f;
        }

        public WeaponType GetConfig()
        {
            return config;
        }

        public bool CanFire()
        {
            return currentCooldown <= 0f;
        }

        public void StartCooldown()
        {
            var config = GetConfig();
            if (config != null && config.Stats.fireRate > 0)
            {
                currentCooldown = 1f / config.Stats.fireRate;
            }
            else
            {
                currentCooldown = 0.5f;
            }
        }

        public void TickCooldown(float deltaTime)
        {
            if (currentCooldown > 0f)
            {
                currentCooldown = Mathf.Max(0f, currentCooldown - deltaTime);
            }
        }
    }
}
