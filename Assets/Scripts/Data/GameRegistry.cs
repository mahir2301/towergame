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
    }
}