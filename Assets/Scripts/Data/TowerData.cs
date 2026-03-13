using UnityEngine;

namespace Data
{
    public class TowerData : MonoBehaviour
    {
        [SerializeField]
        private TowerConfig towerConfig;

        public TowerConfig Config => towerConfig;
    }
}