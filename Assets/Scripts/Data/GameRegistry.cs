using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "GameRegistry", menuName = "towergame/Game Registry")]
    public class GameRegistry : ScriptableObject
    {
        private static GameRegistry _instance;
        public static GameRegistry Instance => _instance ??= Resources.Load<GameRegistry>("GameRegistry");

        [SerializeField]
        private List<EnergyType> energyTypes = new();
        [SerializeField]
        private List<ClassType> classTypes = new();
        [SerializeField]
        private List<TowerType> towerTypes = new();
        [SerializeField]
        private List<WeaponType> weaponTypes = new();

        [ContextMenu("Collect Assets")]
        private void CollectAssets()
        {
            energyTypes = FindAssets<EnergyType>();
            classTypes = FindAssets<ClassType>();
            towerTypes = FindAssets<TowerType>();
            weaponTypes = FindAssets<WeaponType>();
        }

        private List<T> FindAssets<T>() where T : ScriptableObject
        {
            var assetGuids = UnityEditor.AssetDatabase.FindAssetGUIDs($"t:{typeof(T).Name}");
            return assetGuids
                .Select(UnityEditor.AssetDatabase.LoadAssetByGUID<T>)
                .ToList();
        }

        public TowerType GetTowerType(string id)
        {
            return towerTypes.Find(t => t.Id == id);
        }

        public EnergyType GetEnergyType(string id)
        {
            return energyTypes.Find(e => e.Id == id);
        }

        public ClassType GetClassType(string id)
        {
            return classTypes.Find(c => c.Id == id);
        }

        public WeaponType GetWeaponType(string id)
        {
            return weaponTypes.Find(w => w.Id == id);
        }
    }
}