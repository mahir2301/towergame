using UnityEngine;

namespace Data
{
    public class TowerData : MonoBehaviour
    {
        [SerializeField]
        private TowerConfig towerConfig;

        [Header("Connection")]
        private bool isPowered = false;
        private GameObject connectedEnergyNode = null;

        public TowerConfig Config => towerConfig;

        public void SetPowered(bool powered, GameObject energySourceNode)
        {
            isPowered = powered;
            connectedEnergyNode = energySourceNode;
        }
    }
}