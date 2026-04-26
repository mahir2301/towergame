using System.Collections.Generic;
using UnityEngine;

namespace Shared.Data
{
    [CreateAssetMenu(fileName = "PlayerLoadout", menuName = "towergame/Player Loadout")]
    public class PlayerLoadout : ScriptableObject
    {
        [SerializeField] private int maxSlots = 8;
        [SerializeField] private TagType requiredEligibilityTag;
        [SerializeField] private List<PlaceableType> selectedPlaceables = new();

        public int MaxSlots => maxSlots;
        public IReadOnlyList<PlaceableType> SelectedPlaceables => selectedPlaceables;

        public bool Validate(out string issue)
        {
            if (selectedPlaceables == null)
            {
                issue = "Selected placeables list is null.";
                return false;
            }

            if (maxSlots > 0 && selectedPlaceables.Count > maxSlots)
            {
                issue = $"Loadout exceeds max slots ({selectedPlaceables.Count}/{maxSlots}).";
                return false;
            }

            var seen = new HashSet<PlaceableType>();
            for (var i = 0; i < selectedPlaceables.Count; i++)
            {
                var placeable = selectedPlaceables[i];
                if (placeable == null)
                {
                    issue = $"Loadout has null placeable at index {i}.";
                    return false;
                }

                if (!seen.Add(placeable))
                {
                    issue = $"Loadout contains duplicate placeable '{placeable.Id}'.";
                    return false;
                }

                if (requiredEligibilityTag != null && !placeable.HasTag(requiredEligibilityTag))
                {
                    issue = $"Placeable '{placeable.Id}' is missing required tag '{requiredEligibilityTag.Id}'.";
                    return false;
                }
            }

            issue = null;
            return true;
        }
    }
}
