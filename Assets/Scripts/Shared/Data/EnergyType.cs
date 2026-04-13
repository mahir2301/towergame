using UnityEngine;

namespace Game.Shared.Data
{
    [CreateAssetMenu(fileName = "EnergyType", menuName = "towergame/Energy Type")]
    public class EnergyType : ScriptableObject
    {
        [SerializeField]
        private string id;
        [SerializeField]
        private string displayName;
        [SerializeField]
        private GameObject prefab;
        [SerializeField]
        private int energyRange = 20;

        public string Id => id;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public int EnergyRange => energyRange;
    }
}
