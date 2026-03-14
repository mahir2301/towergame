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

        private WeaponStats stats;
        
        public string Id => id;
        public string DisplayName => displayName;
    }

    public struct WeaponStats
    {
        public float damage;
        public float range;
        public float fireRate;
    }
}