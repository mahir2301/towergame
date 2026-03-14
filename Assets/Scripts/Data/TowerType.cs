using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "TowerType", menuName = "towergame/Tower Type")]
    public class TowerType : ScriptableObject
    {
        [SerializeField]
        private string id;
        [SerializeField]
        private string displayName;
        [SerializeField]
        private ClassType classType;
        [SerializeField]
        private GameObject prefab;
        [SerializeField]
        private Vector2Int size;

        private TowerStats stats;

        public string Id => id;
        public string DisplayName => displayName;
        public ClassType ClassType => classType;
        public GameObject Prefab => prefab;
        public Vector2Int Size => size;
    }

    public struct TowerStats
    {
        public float health;
        public float damage;
        public float range;
        public float fireRate;
    }
}