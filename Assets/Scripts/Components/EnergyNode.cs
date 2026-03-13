using Data;
using Managers;
using UnityEngine;

namespace Components
{
    public class EnergyNode : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private EnergyNodeConfig energyNodeConfig;

        public int UsedCapacity => energyNodeConfig.usedCapacity;
        public int AvailableCapacity => energyNodeConfig.capacity - energyNodeConfig.usedCapacity;


        // TODO all the functions will need to be rewriten probably but I will leave it there as placeholders (we will need to do some coilder magic for the nodes and towers and connections)
        public bool HasCapacity(int amount)
        {
            return AvailableCapacity >= amount;
        }

        public void ConsumeCapacity(int amount)
        {
            energyNodeConfig.usedCapacity += amount;
        }

        public void ReleaseCapacity(int amount)
        {
            energyNodeConfig.usedCapacity = Mathf.Max(0, energyNodeConfig.usedCapacity - amount);
        }

        public void ResetCapacity()
        {
            energyNodeConfig.usedCapacity = 0;
        }

        public void ApplyEnergySpike(int spikeAmount, float duration)
        {
            var originalCapacity = energyNodeConfig.capacity;
            energyNodeConfig.capacity = Mathf.Max(0, energyNodeConfig.capacity - spikeAmount);


            Invoke(nameof(RestoreCapacity), duration);
        }

        private void RestoreCapacity()
        {
            energyNodeConfig.capacity = 10;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 1f);
            
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireCube(transform.position, new Vector3(1, 0.5f, 1));
        }
    }
}
