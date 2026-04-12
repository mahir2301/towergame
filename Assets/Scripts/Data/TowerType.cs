using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "TowerType", menuName = "towergame/Tower Type")]
    public class TowerType : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;

        [Header("Classification")]
        [SerializeField] private ClassType classType;
        [SerializeField] private GameObject prefab;

        [Header("Placement")]
        [SerializeField] private Vector2Int size = Vector2Int.one;
        [SerializeField] private bool canBePlacedOnWater;
        [SerializeField] private Vector3 placementOffset;

        [Header("Stats")]
        [SerializeField] private TowerStats stats;

        public string Id => id;
        public string DisplayName => displayName;
        public ClassType ClassType => classType;
        public GameObject Prefab => prefab;
        public Vector2Int Size => size;
        public bool CanBePlacedOnWater => canBePlacedOnWater;
        public Vector3 PlacementOffset => placementOffset;
        public TowerStats Stats => stats;
        public bool IsAntenna => stats.antennaRange > 0;

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(id)
                   && prefab != null
                   && size.x > 0 && size.y > 0
                   && stats.maxHealth > 0;
        }
    }

    [System.Serializable]
    public struct TowerStats
    {
        [Header("Combat")]
        public float maxHealth;
        public float damage;
        public float range;
        public float fireRate;

        [Header("Energy")]
        public int energyCost;
        public int antennaRange;

        [Header("Economy")]
        public int buildCost;
        public int upgradeCost;
    }
}
