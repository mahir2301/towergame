using System.Collections.Generic;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "ClassType", menuName = "towergame/Class Type")]
    public class ClassType : ScriptableObject
    {
        [SerializeField]
        private string id;
        [SerializeField]
        private string displayName;
        [SerializeField]
        private List<EnergyType> compatibleEnergyTypes;

        public string Id => id;
        public string DisplayName => displayName;

        public bool CanConnectTo(EnergyType energyType)
        {
            return compatibleEnergyTypes.Contains(energyType);
        }
    }
}