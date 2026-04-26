using Shared.Entities;
using System.Collections.Generic;
using UnityEngine;

namespace Shared.Data
{
    [CreateAssetMenu(fileName = "EntityType", menuName = "towergame/Entity Type")]
    public class EntityType : ScriptableObject, IRegistryType, ITaggableType
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private List<TagType> tags = new();
        [SerializeField] private EntityRuntime prefab;

        public string Id => id;
        public string DisplayName => displayName;
        public IReadOnlyList<TagType> Tags => tags;
        public EntityRuntime Prefab => prefab;

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
