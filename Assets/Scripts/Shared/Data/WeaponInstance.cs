using System;
using UnityEngine;

namespace Shared.Data
{
    [Serializable]
    public class WeaponInstance
    {
        [SerializeField] private string configId;
        [SerializeField] private float currentCooldown;

        public string ConfigId => configId;
        public float CurrentCooldown => currentCooldown;

        public WeaponInstance(WeaponType config)
        {
            configId = config.Id;
            currentCooldown = 0f;
        }

        public WeaponType GetConfig()
        {
            return GameRegistry.Instance?.GetWeaponType(configId);
        }

        public bool CanFire()
        {
            return currentCooldown <= 0f;
        }

        public void StartCooldown()
        {
            var config = GetConfig();
            if (config != null && config.Stats.fireRate > 0)
                currentCooldown = 1f / config.Stats.fireRate;
            else
                currentCooldown = 0.5f;
        }

        public void TickCooldown(float deltaTime)
        {
            if (currentCooldown > 0f)
                currentCooldown = Mathf.Max(0f, currentCooldown - deltaTime);
        }
    }
}
