using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "EnergyNodeConfig", menuName = "Energy Node Config")]
    public class EnergyNodeConfig: ScriptableObject
    {
        [Header("Basic Info")]
        public ENERGY_NODE_TYPE energyType;
        public GameObject energyNodePrefab;

        [Header("Grid")]
        public Vector2Int gridSize = new(1, 1);

        [Header("Energy")]
        public int capacity = 10;
        public int usedCapacity = 0;
    }

    public enum ENERGY_NODE_TYPE
    {
        WATER,
        MAGIC,
        TECHNO,
        MEDIVAL
    }
}