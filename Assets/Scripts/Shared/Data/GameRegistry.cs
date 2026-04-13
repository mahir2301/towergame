using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Shared.Data
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

        private Dictionary<string, TowerType> _towerLookup;
        private Dictionary<string, EnergyType> _energyLookup;
        private Dictionary<string, ClassType> _classLookup;
        private Dictionary<string, WeaponType> _weaponLookup;

        public IReadOnlyList<TowerType> TowerTypes => towerTypes;
        public IReadOnlyList<EnergyType> EnergyTypes => energyTypes;
        public IReadOnlyList<ClassType> ClassTypes => classTypes;
        public IReadOnlyList<WeaponType> WeaponTypes => weaponTypes;

        private void EnsureInitialized()
        {
            if (_towerLookup != null) return;
            _towerLookup = BuildLookup(towerTypes);
            _energyLookup = BuildLookup(energyTypes);
            _classLookup = BuildLookup(classTypes);
            _weaponLookup = BuildLookup(weaponTypes);
        }

        private static Dictionary<string, T> BuildLookup<T>(List<T> items) where T : ScriptableObject
        {
            var lookup = new Dictionary<string, T>(items.Count);
            foreach (var item in items)
            {
                if (item == null) continue;
                var id = GetId(item);
                if (!string.IsNullOrEmpty(id))
                    lookup[id] = item;
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
                _ => null
            };
        }

        public TowerType GetTowerType(string id)
        {
            EnsureInitialized();
            return _towerLookup.TryGetValue(id, out var type) ? type : null;
        }

        public EnergyType GetEnergyType(string id)
        {
            EnsureInitialized();
            return _energyLookup.TryGetValue(id, out var type) ? type : null;
        }

        public ClassType GetClassType(string id)
        {
            EnsureInitialized();
            return _classLookup.TryGetValue(id, out var type) ? type : null;
        }

        public WeaponType GetWeaponType(string id)
        {
            EnsureInitialized();
            return _weaponLookup.TryGetValue(id, out var type) ? type : null;
        }

        public bool HasId(string id)
        {
            EnsureInitialized();
            return _towerLookup.ContainsKey(id)
                   || _energyLookup.ContainsKey(id)
                   || _classLookup.ContainsKey(id)
                   || _weaponLookup.ContainsKey(id);
        }

#if UNITY_EDITOR
        [ContextMenu("Collect Assets")]
        private void CollectAssets()
        {
            energyTypes = FindAssets<EnergyType>();
            classTypes = FindAssets<ClassType>();
            towerTypes = FindAssets<TowerType>();
            weaponTypes = FindAssets<WeaponType>();
            _towerLookup = null;
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
