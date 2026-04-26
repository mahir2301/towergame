using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Shared.Data
{
    [CreateAssetMenu(fileName = "GameRegistry", menuName = "towergame/Game Registry")]
    public class GameRegistry : ScriptableObject
    {
        private static GameRegistry _instance;
        public static GameRegistry Instance => _instance ??= Resources.Load<GameRegistry>("GameRegistry");

        [SerializeField] private ClassTypeRegistry classTypes = new();
        [SerializeField] private WeaponTypeRegistry weaponTypes = new();
        [SerializeField] private EntityTypeRegistry entityTypes = new();

        [SerializeField] private PlaceableTypeRegistry placeableTypes = new();
        [SerializeField] private TagTypeRegistry tagTypes = new();
        [SerializeField] private TileTypeRegistry tileTypes = new();

        [Header("Required Entity Types")]
        [SerializeField] private EntityType requiredPlayerEntityType;
        [SerializeField] private EntityType requiredProjectileEntityType;

        public IReadOnlyList<ClassType> ClassTypes => classTypes.Items;
        public IReadOnlyList<WeaponType> WeaponTypes => weaponTypes.Items;
        public IReadOnlyList<EntityType> EntityTypes => entityTypes.Items;
        public IReadOnlyList<PlaceableType> PlaceableTypes => placeableTypes.Items;
        public IReadOnlyList<TagType> Tags => tagTypes.Items;
        public IReadOnlyList<TileType> TileTypes => tileTypes.Items;
        public EntityType RequiredPlayerEntityType => requiredPlayerEntityType;
        public EntityType RequiredProjectileEntityType => requiredProjectileEntityType;
        public string RequiredPlayerEntityTypeId => requiredPlayerEntityType != null ? requiredPlayerEntityType.Id : string.Empty;
        public string RequiredProjectileEntityTypeId => requiredProjectileEntityType != null ? requiredProjectileEntityType.Id : string.Empty;

        public ClassType GetClassType(string id) => classTypes.Get(id);
        public WeaponType GetWeaponType(string id) => weaponTypes.Get(id);
        public EntityType GetEntityType(string id) => entityTypes.Get(id);
        public PlaceableType GetPlaceableType(string id) => placeableTypes.Get(id);
        public TileType GetTileType(string id) => tileTypes.Get(id);

        public void GetPlaceablesWithTag(TagType tag, List<PlaceableType> results)
            => placeableTypes.GetWithTag(tag, results);

        public bool HasId(string id)
        {
            return classTypes.HasId(id)
                   || weaponTypes.HasId(id)
                   || entityTypes.HasId(id)
                   || placeableTypes.HasId(id)
                   || tagTypes.HasId(id)
                   || tileTypes.HasId(id);
        }

        public bool ValidateAllTypes(out string issue)
        {
            if (!classTypes.Validate(out issue))
                return false;

            if (!weaponTypes.Validate(out issue))
                return false;

            if (!entityTypes.Validate(out issue))
                return false;

            if (!ValidateRequiredEntityTypes(out issue))
                return false;

            if (!tagTypes.Validate(out issue))
                return false;

            if (!tileTypes.Validate(out issue))
                return false;

            if (!placeableTypes.Validate(out issue))
                return false;

            issue = null;
            return true;
        }

        private bool ValidateRequiredEntityTypes(out string issue)
        {
            if (requiredPlayerEntityType == null)
            {
                issue = "Missing required player EntityType reference.";
                return false;
            }

            if (requiredProjectileEntityType == null)
            {
                issue = "Missing required projectile EntityType reference.";
                return false;
            }

            issue = null;
            return true;
        }

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
            classTypes.SetItems(FindAssets<ClassType>());
            weaponTypes.SetItems(FindAssets<WeaponType>());
            entityTypes.SetItems(FindAssets<EntityType>());

            placeableTypes.SetItems(FindAssets<PlaceableType>());
            tagTypes.SetItems(FindAssets<TagType>());
            tileTypes.SetItems(FindAssets<TileType>());

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
