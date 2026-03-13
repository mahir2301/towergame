using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "TowerConfig", menuName = "Tower Config")]
    public class TowerConfig : ScriptableObject
    {
        [Header("Basic Info")]
        public string towerName;
        public GameObject towerPrefab;

        [Header("Grid")]
        public Vector2Int gridSize = new(1, 1);

        [Header("Stats")]
        public int goldCost = 100;
        public float range = 5f;
        public float damage = 10f;
        public float fireRate = 1f;
        public float maxHealth = 100f;

        [Header("Energy")]
        public int energyConsumption = 1;

    }
}