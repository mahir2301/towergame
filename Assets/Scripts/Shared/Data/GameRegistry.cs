using System.Collections.Generic;
using System.Linq;
using System;
using Shared.Entities;
using UnityEngine;

namespace Shared.Data
{
    [CreateAssetMenu(fileName = "GameRegistry", menuName = "towergame/Game Registry")]
    public class GameRegistry : ScriptableObject
    {
        private static GameRegistry _instance;
        public static GameRegistry Instance => _instance ??= Resources.Load<GameRegistry>("GameRegistry");

        [SerializeField] private List<EnergyType> energyTypes = new();
        [SerializeField] private List<ClassType> classTypes = new();
        [SerializeField] private List<TowerType> towerTypes = new();
        [SerializeField] private List<WeaponType> weaponTypes = new();
        [SerializeField] private List<EntityType> entityTypes = new();
        [SerializeField] private List<NexusType> nexusTypes = new();

        private Dictionary<string, TowerType> _towerLookup;
        private Dictionary<string, EnergyType> _energyLookup;
        private Dictionary<string, ClassType> _classLookup;
        private Dictionary<string, WeaponType> _weaponLookup;
        private Dictionary<string, EntityType> _entityLookup;
        private Dictionary<EntityKind, EntityType> _entityKindLookup;
        private Dictionary<string, NexusType> _nexusLookup;

        public IReadOnlyList<TowerType> TowerTypes => towerTypes;
        public IReadOnlyList<EnergyType> EnergyTypes => energyTypes;
        public IReadOnlyList<ClassType> ClassTypes => classTypes;
        public IReadOnlyList<WeaponType> WeaponTypes => weaponTypes;
        public IReadOnlyList<EntityType> EntityTypes => entityTypes;
        public IReadOnlyList<NexusType> NexusTypes => nexusTypes;

        private void EnsureInitialized()
        {
            if (_towerLookup != null) return;
            _towerLookup = BuildLookup(towerTypes);
            _energyLookup = BuildLookup(energyTypes);
            _classLookup = BuildLookup(classTypes);
            _weaponLookup = BuildLookup(weaponTypes);
            _entityLookup = BuildLookup(entityTypes);
            _entityKindLookup = BuildEntityKindLookup(entityTypes);
            _nexusLookup = BuildLookup(nexusTypes);
        }

        private static Dictionary<string, T> BuildLookup<T>(List<T> items) where T : ScriptableObject, IRegistryType
        {
            var lookup = new Dictionary<string, T>(items.Count, System.StringComparer.Ordinal);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;
                var id = item.Id;
                if (!string.IsNullOrEmpty(id))
                {
                    if (lookup.TryGetValue(id, out var existing) && existing != item)
                        throw new InvalidOperationException($"Duplicate registry id '{id}' detected while building {typeof(T).Name} lookup.");

                    lookup[id] = item;
                }
            }
            return lookup;
        }

        private static Dictionary<EntityKind, EntityType> BuildEntityKindLookup(List<EntityType> items)
        {
            var lookup = new Dictionary<EntityKind, EntityType>();
            for (var i = 0; i < items.Count; i++)
            {
                var type = items[i];
                if (type == null || type.Kind == EntityKind.Unknown)
                    continue;

                if (lookup.TryGetValue(type.Kind, out var existing) && existing != type)
                    throw new InvalidOperationException($"Duplicate EntityType kind '{type.Kind}' detected while building EntityKind lookup.");

                lookup[type.Kind] = type;
            }

            return lookup;
        }

        public TowerType GetTowerType(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            EnsureInitialized();
            return _towerLookup.TryGetValue(id, out var type) ? type : null;
        }

        public EnergyType GetEnergyType(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            EnsureInitialized();
            return _energyLookup.TryGetValue(id, out var type) ? type : null;
        }

        public ClassType GetClassType(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            EnsureInitialized();
            return _classLookup.TryGetValue(id, out var type) ? type : null;
        }

        public WeaponType GetWeaponType(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            EnsureInitialized();
            return _weaponLookup.TryGetValue(id, out var type) ? type : null;
        }

        public EntityType GetEntityType(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            EnsureInitialized();
            return _entityLookup.TryGetValue(id, out var type) ? type : null;
        }

        public EntityType GetEntityType(EntityKind kind)
        {
            if (kind == EntityKind.Unknown)
                return null;

            EnsureInitialized();
            return _entityKindLookup.TryGetValue(kind, out var type) ? type : null;
        }

        public NexusType GetNexusType(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            EnsureInitialized();
            return _nexusLookup.TryGetValue(id, out var type) ? type : null;
        }

        public NexusType GetNexusType()
        {
            EnsureInitialized();
            for (var i = 0; i < nexusTypes.Count; i++)
            {
                if (nexusTypes[i] != null)
                    return nexusTypes[i];
            }
            return null;
        }

        public bool HasId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            EnsureInitialized();
            return _towerLookup.ContainsKey(id)
                   || _energyLookup.ContainsKey(id)
                   || _classLookup.ContainsKey(id)
                   || _weaponLookup.ContainsKey(id)
                   || _entityLookup.ContainsKey(id)
                   || _nexusLookup.ContainsKey(id);
        }

        public bool ValidateAllTypes(out string issue)
        {
            if (!ValidateEnergyTypes(out issue))
                return false;

            if (!ValidateClassTypes(out issue))
                return false;

            if (!ValidateTowerTypes(out issue))
                return false;

            if (!ValidateWeaponTypes(out issue))
                return false;

            if (!ValidateEntityTypes(out issue))
                return false;

            if (!ValidateRequiredEntityKinds(out issue))
                return false;

            if (!ValidateNexusTypes(out issue))
                return false;

            issue = null;
            return true;
        }

        public bool ValidateRequiredEntityKinds(out string issue)
        {
            EnsureInitialized();

            if (GetEntityType(EntityKind.Player) == null)
            {
                issue = "Missing required EntityType for kind Player.";
                return false;
            }

            if (GetEntityType(EntityKind.Projectile) == null)
            {
                issue = "Missing required EntityType for kind Projectile.";
                return false;
            }

            issue = null;
            return true;
        }

        public bool ValidateTowerTypes(out string issue)
        {
            return ValidateTypeList(
                towerTypes,
                "TowerTypes",
                "TowerType",
                (TowerType type, out string localIssue) =>
                {
                    if (type.Prefab == null)
                    {
                        localIssue = $"TowerType '{type.Id}' is missing a prefab.";
                        return false;
                    }

                    if (type.ClassType == null)
                    {
                        localIssue = $"TowerType '{type.Id}' is missing a class type.";
                        return false;
                    }

                    if (type.Size.x <= 0 || type.Size.y <= 0)
                    {
                        localIssue = $"TowerType '{type.Id}' has invalid size ({type.Size.x}, {type.Size.y}).";
                        return false;
                    }

                    localIssue = null;
                    return true;
                },
                out issue);
        }

        public bool ValidateEnergyTypes(out string issue)
        {
            return ValidateTypeList(
                energyTypes,
                "EnergyTypes",
                "EnergyType",
                (EnergyType type, out string localIssue) =>
                {
                    if (type.Prefab == null)
                    {
                        localIssue = $"EnergyType '{type.Id}' is missing a prefab.";
                        return false;
                    }

                    if (type.EnergyRange <= 0)
                    {
                        localIssue = $"EnergyType '{type.Id}' has invalid range {type.EnergyRange}.";
                        return false;
                    }

                    localIssue = null;
                    return true;
                },
                out issue);
        }

        public bool ValidateClassTypes(out string issue)
        {
            return ValidateTypeList(
                classTypes,
                "ClassTypes",
                "ClassType",
                (ClassType type, out string localIssue) =>
                {
                    var compatibleEnergyTypes = type.CompatibleEnergyTypes;
                    if (compatibleEnergyTypes == null)
                    {
                        localIssue = $"ClassType '{type.Id}' has no compatible energy types list assigned.";
                        return false;
                    }

                    for (var j = 0; j < compatibleEnergyTypes.Count; j++)
                    {
                        if (compatibleEnergyTypes[j] != null)
                            continue;

                        localIssue = $"ClassType '{type.Id}' has a null compatible energy entry at index {j}.";
                        return false;
                    }

                    localIssue = null;
                    return true;
                },
                out issue);
        }

        public bool ValidateWeaponTypes(out string issue)
        {
            return ValidateTypeList(
                weaponTypes,
                "WeaponTypes",
                "WeaponType",
                (WeaponType type, out string localIssue) =>
                {
                    if (type.ClassType == null)
                    {
                        localIssue = $"WeaponType '{type.Id}' is missing a class type.";
                        return false;
                    }

                    localIssue = null;
                    return true;
                },
                out issue);
        }

        public bool ValidateEntityTypes(out string issue)
        {
            if (!ValidateTypeList(
                    entityTypes,
                    "EntityTypes",
                    "EntityType",
                    (EntityType type, out string localIssue) =>
                    {
                        if (type.Kind == EntityKind.Unknown)
                        {
                            localIssue = $"EntityType '{type.Id}' has kind Unknown.";
                            return false;
                        }

                        if (type.Prefab == null)
                        {
                            localIssue = $"EntityType '{type.Id}' is missing a prefab.";
                            return false;
                        }

                        if (type.Prefab.GetComponent<EntityRuntime>() == null)
                        {
                            localIssue = $"EntityType '{type.Id}' prefab does not include EntityRuntime.";
                            return false;
                        }

                        localIssue = null;
                        return true;
                    },
                    out issue))
            {
                return false;
            }

            var seenKinds = new HashSet<EntityKind>();
            for (var i = 0; i < entityTypes.Count; i++)
            {
                var type = entityTypes[i];
                if (type == null || type.Kind == EntityKind.Unknown)
                    continue;

                if (seenKinds.Add(type.Kind))
                    continue;

                issue = $"Duplicate EntityType kind '{type.Kind}'. GetEntityType(EntityKind) requires unique kinds.";
                return false;
            }

            issue = null;
            return true;
        }

        public bool ValidateNexusTypes(out string issue)
        {
            if (!ValidateTypeList(
                    nexusTypes,
                    "NexusTypes",
                    "NexusType",
                    (NexusType type, out string localIssue) =>
                    {
                        if (type.Prefab == null)
                        {
                            localIssue = $"NexusType '{type.Id}' is missing a prefab.";
                            return false;
                        }

                        if (type.MaxHealth <= 0f)
                        {
                            localIssue = $"NexusType '{type.Id}' has invalid maxHealth {type.MaxHealth}.";
                            return false;
                        }

                        localIssue = null;
                        return true;
                    },
                    out issue))
            {
                return false;
            }

            var count = 0;
            for (var i = 0; i < nexusTypes.Count; i++)
            {
                if (nexusTypes[i] != null)
                    count++;
            }

            if (count != 1)
            {
                issue = $"Expected exactly 1 NexusType but found {count}. Only one nexus is allowed per game.";
                return false;
            }

            issue = null;
            return true;
        }

        private bool ValidateTypeList<T>(List<T> items, string listName, string typeName,
            ValidationRule<T> validationRule, out string issue)
            where T : ScriptableObject, IRegistryType
        {
            EnsureInitialized();

            var seenIds = new HashSet<string>(System.StringComparer.Ordinal);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    issue = $"{listName} contains a null entry at index {i}.";
                    return false;
                }

                var id = item.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    issue = $"{typeName} '{item.name}' has an empty id.";
                    return false;
                }

                if (!seenIds.Add(id))
                {
                    issue = $"Duplicate {typeName} id '{id}'.";
                    return false;
                }

                if (validationRule(item, out issue))
                    continue;

                return false;
            }

            issue = null;
            return true;
        }

        private delegate bool ValidationRule<in T>(T item, out string issue);

#if UNITY_EDITOR
        [ContextMenu("Validate Registry")]
        private void ValidateRegistry()
        {
            if (ValidateAllTypes(out var issue))
            {
                Debug.Log("[GameRegistry] Validation passed.", this);
                return;
            }

            Debug.LogError($"[GameRegistry] Validation failed: {issue}", this);
        }

        [ContextMenu("Collect Assets")]
        private void CollectAssets()
        {
            energyTypes = FindAssets<EnergyType>();
            classTypes = FindAssets<ClassType>();
            towerTypes = FindAssets<TowerType>();
            weaponTypes = FindAssets<WeaponType>();
            entityTypes = FindAssets<EntityType>();
            nexusTypes = FindAssets<NexusType>();
            _towerLookup = null;
            _energyLookup = null;
            _classLookup = null;
            _weaponLookup = null;
            _entityLookup = null;
            _entityKindLookup = null;
            _nexusLookup = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private static List<T> FindAssets<T>() where T : ScriptableObject
        {
            var assetGuids = UnityEditor.AssetDatabase.FindAssetGUIDs($"t:{typeof(T).Name}");
            return assetGuids
                .Select(UnityEditor.AssetDatabase.LoadAssetByGUID<T>)
                .ToList();
        }
#endif
    }
}
