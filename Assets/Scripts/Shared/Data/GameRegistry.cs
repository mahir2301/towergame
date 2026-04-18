using System.Collections.Generic;
using System.Linq;
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

        private Dictionary<string, TowerType> _towerLookup;
        private Dictionary<string, EnergyType> _energyLookup;
        private Dictionary<string, ClassType> _classLookup;
        private Dictionary<string, WeaponType> _weaponLookup;
        private Dictionary<string, EntityType> _entityLookup;
        private Dictionary<EntityKind, EntityType> _entityKindLookup;

        public IReadOnlyList<TowerType> TowerTypes => towerTypes;
        public IReadOnlyList<EnergyType> EnergyTypes => energyTypes;
        public IReadOnlyList<ClassType> ClassTypes => classTypes;
        public IReadOnlyList<WeaponType> WeaponTypes => weaponTypes;
        public IReadOnlyList<EntityType> EntityTypes => entityTypes;

        private void EnsureInitialized()
        {
            if (_towerLookup != null) return;
            _towerLookup = BuildLookup(towerTypes);
            _energyLookup = BuildLookup(energyTypes);
            _classLookup = BuildLookup(classTypes);
            _weaponLookup = BuildLookup(weaponTypes);
            _entityLookup = BuildLookup(entityTypes);
            _entityKindLookup = BuildEntityKindLookup(entityTypes);
        }

        private static Dictionary<string, T> BuildLookup<T>(List<T> items) where T : ScriptableObject
        {
            var lookup = new Dictionary<string, T>(items.Count, System.StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item == null) continue;
                var id = GetId(item);
                if (!string.IsNullOrEmpty(id))
                    lookup[id] = item;
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

                if (!lookup.ContainsKey(type.Kind))
                    lookup[type.Kind] = type;
            }

            return lookup;
        }

        private static string GetId(ScriptableObject so)
        {
            return so switch
            {
                TowerType t => t.Id,
                EnergyType e => e.Id,
                ClassType c => c.Id,
                WeaponType w => w.Id,
                EntityType et => et.Id,
                _ => null
            };
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

        public bool HasId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            EnsureInitialized();
            return _towerLookup.ContainsKey(id)
                   || _energyLookup.ContainsKey(id)
                   || _classLookup.ContainsKey(id)
                   || _weaponLookup.ContainsKey(id)
                   || _entityLookup.ContainsKey(id);
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

            issue = null;
            return true;
        }

        public bool ValidateTowerTypes(out string issue)
        {
            return ValidateTypeList(
                towerTypes,
                "TowerTypes",
                "TowerType",
                GetId,
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
                GetId,
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
                GetId,
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
                GetId,
                (WeaponType type, out string localIssue) =>
                {
                    if (type.ProjectilePrefab == null)
                    {
                        localIssue = $"WeaponType '{type.Id}' is missing a projectile prefab.";
                        return false;
                    }

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
                    GetId,
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

        private bool ValidateTypeList<T>(List<T> items, string listName, string typeName,
            System.Func<T, string> getId, ValidationRule<T> validationRule, out string issue)
            where T : ScriptableObject
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

                var id = getId(item);
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
            _towerLookup = null;
            _energyLookup = null;
            _classLookup = null;
            _weaponLookup = null;
            _entityLookup = null;
            _entityKindLookup = null;
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
