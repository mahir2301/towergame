using System.Collections.Generic;
using UnityEngine;

namespace Shared.Data
{
    [CreateAssetMenu(fileName = "TileType", menuName = "towergame/Tile Type")]
    public class TileType : ScriptableObject, IRegistryType, ITaggableType
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private List<TagType> tags = new();
        [SerializeField] private Color debugColor = Color.white;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public IReadOnlyList<TagType> Tags => tags;
        public Color DebugColor => debugColor;

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
    }
}
