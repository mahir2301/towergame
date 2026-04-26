using System.Collections.Generic;
using UnityEngine;

namespace Shared.Data
{
    [CreateAssetMenu(fileName = "PlaceableType", menuName = "towergame/Placeable Type")]
    public class PlaceableType : ScriptableObject, IRegistryType, ITaggableType
    {
        [Header("Identity")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;

        [Header("Runtime")]
        [SerializeField] private GameObject prefab;

        [Header("Placement")]
        [SerializeField] private Vector2Int size = Vector2Int.one;
        [SerializeField] private List<TileType> allowedTileTypes = new();
        [SerializeField] private Vector3 placementOffset;

        [Header("Economy")]
        [SerializeField] private int buildCost;
        [SerializeField] private int sellValue;

        [Header("Metadata")]
        [SerializeField] private List<TagType> tags = new();

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public GameObject Prefab => prefab;
        public Vector2Int Size => size;
        public IReadOnlyList<TileType> AllowedTileTypes => allowedTileTypes;
        public Vector3 PlacementOffset => placementOffset;
        public int BuildCost => buildCost;
        public int SellValue => sellValue;
        public IReadOnlyList<TagType> Tags => tags;

        public bool HasTag(TagType tag)
        {
            if (tag == null || tags == null)
                return false;

            for (var i = 0; i < tags.Count; i++)
            {
                if (tags[i] == tag)
                    return true;
            }

            return false;
        }

        public bool IsAllowedOn(TileType tileType)
        {
            if (tileType == null || allowedTileTypes == null)
                return false;

            for (var i = 0; i < allowedTileTypes.Count; i++)
            {
                if (allowedTileTypes[i] == tileType)
                    return true;
            }

            return false;
        }
    }
}
