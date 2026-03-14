using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "WeaponType", menuName = "towergame/Weapon Type")]
    public class WeaponType : ScriptableObject
    {
        [SerializeField]
        private string id;
        [SerializeField]
        private string displayName;
        [SerializeField]
        private ClassType classType;
        [SerializeField]
        private WeaponStats stats;

        public string Id => id;
        public string DisplayName => displayName;
        public ClassType ClassType => classType;
        public WeaponStats Stats => stats;
    }

    [System.Serializable]
    public struct WeaponStats
    {
        public float damage;
        public float range;
        public float fireRate;
    }
}