using System.Collections.Generic;
using UnityEngine;

namespace Shared.Data
{
    [CreateAssetMenu(fileName = "ClassType", menuName = "towergame/Class Type")]
    public class ClassType : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private List<EnergyType> compatibleEnergyTypes;

        private HashSet<EnergyType> _compatibilitySet;

        public string Id => id;
        public string DisplayName => displayName;
        public IReadOnlyList<EnergyType> CompatibleEnergyTypes => compatibleEnergyTypes;

        public bool CanConnectTo(EnergyType energyType)
        {
            if (energyType == null) return false;
            if (_compatibilitySet == null)
                _compatibilitySet = new HashSet<EnergyType>(compatibleEnergyTypes ?? new List<EnergyType>());
            return _compatibilitySet.Contains(energyType);
        }
    }
}
