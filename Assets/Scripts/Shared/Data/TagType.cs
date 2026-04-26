using UnityEngine;

namespace Shared.Data
{
    [CreateAssetMenu(fileName = "TagType", menuName = "towergame/Tag Type")]
    public class TagType : ScriptableObject, IRegistryType
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea]
        [SerializeField] private string description;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
    }
}
