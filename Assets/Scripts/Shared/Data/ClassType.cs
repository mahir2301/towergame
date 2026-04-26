using System.Collections.Generic;
using UnityEngine;

namespace Shared.Data
{
    [CreateAssetMenu(fileName = "ClassType", menuName = "towergame/Class Type")]
    public class ClassType : ScriptableObject, IRegistryType
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private List<PlaceableType> compatibleEnergyPlaceables;

        private HashSet<PlaceableType> _compatibilitySet;

        public string Id => id;
        public string DisplayName => displayName;
        public IReadOnlyList<PlaceableType> CompatibleEnergyPlaceables => compatibleEnergyPlaceables;

        public bool CanConnectTo(PlaceableType energyPlaceable)
        {
            if (energyPlaceable == null)
                return false;

            if (_compatibilitySet == null)
                _compatibilitySet = new HashSet<PlaceableType>(compatibleEnergyPlaceables ?? new List<PlaceableType>());

            return _compatibilitySet.Contains(energyPlaceable);
        }
    }
}
