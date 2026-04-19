using Shared.Entities;
using UnityEngine;

namespace Shared.Data
{
    [CreateAssetMenu(fileName = "EntityType", menuName = "towergame/Entity Type")]
    public class EntityType : ScriptableObject, IRegistryType
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private EntityKind kind = EntityKind.Unknown;
        [SerializeField] private EntityRuntime prefab;

        public string Id => id;
        public string DisplayName => displayName;
        public EntityKind Kind => kind;
        public EntityRuntime Prefab => prefab;
    }
}
