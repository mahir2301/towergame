using UnityEngine;

namespace Shared.Data
{
    [CreateAssetMenu(fileName = "NexusType", menuName = "towergame/Nexus Type")]
    public class NexusType : ScriptableObject, IRegistryType
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private GameObject prefab;
        [SerializeField] private float maxHealth = 1000f;

        public string Id => id;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public float MaxHealth => maxHealth;
    }
}
