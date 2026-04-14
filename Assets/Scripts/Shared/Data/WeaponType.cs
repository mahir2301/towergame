using UnityEngine;

namespace Shared.Data
{
    [CreateAssetMenu(fileName = "WeaponType", menuName = "towergame/Weapon Type")]
    public class WeaponType : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;

        [Header("Classification")]
        [SerializeField] private ClassType classType;
        [SerializeField] private GameObject projectilePrefab;

        [Header("Stats")]
        [SerializeField] private WeaponStats stats;

        public string Id => id;
        public string DisplayName => displayName;
        public ClassType ClassType => classType;
        public GameObject ProjectilePrefab => projectilePrefab;
        public WeaponStats Stats => stats;
    }

    [System.Serializable]
    public struct WeaponStats
    {
        [Header("Combat")]
        public float damage;
        public float range;
        public float fireRate;
        public float projectileSpeed;

        [Header("Energy")]
        public int energyCostPerShot;
    }
}
