using System.Collections.Generic;
using UnityEngine;
using Shared.Entities;

namespace Shared.Data
{
    [System.Serializable]
    public sealed class ClassTypeRegistry : TypeRegistry<ClassType>
    {
        protected override string ListName => "ClassTypes";
        protected override string TypeName => "ClassType";

        protected override bool ValidateItem(ClassType item, out string issue)
        {
            var compatibleEnergyPlaceables = item.CompatibleEnergyPlaceables;
            if (compatibleEnergyPlaceables == null)
            {
                issue = $"ClassType '{item.Id}' has no compatible energy placeables list assigned.";
                return false;
            }

            for (var i = 0; i < compatibleEnergyPlaceables.Count; i++)
            {
                if (compatibleEnergyPlaceables[i] != null)
                    continue;

                issue = $"ClassType '{item.Id}' has a null compatible energy placeable at index {i}.";
                return false;
            }

            issue = null;
            return true;
        }
    }

    [System.Serializable]
    public sealed class WeaponTypeRegistry : TypeRegistry<WeaponType>
    {
        protected override string ListName => "WeaponTypes";
        protected override string TypeName => "WeaponType";

        protected override bool ValidateItem(WeaponType item, out string issue)
        {
            if (item.ClassType == null)
            {
                issue = $"WeaponType '{item.Id}' is missing a class type.";
                return false;
            }

            issue = null;
            return true;
        }
    }

    [System.Serializable]
    public sealed class EntityTypeRegistry : TypeRegistry<EntityType>
    {
        protected override string ListName => "EntityTypes";
        protected override string TypeName => "EntityType";

        protected override bool ValidateItem(EntityType item, out string issue)
        {
            if (item.Prefab == null)
            {
                issue = $"EntityType '{item.Id}' is missing a prefab.";
                return false;
            }

            if (item.Prefab.GetComponent<EntityRuntime>() == null)
            {
                issue = $"EntityType '{item.Id}' prefab does not include EntityRuntime.";
                return false;
            }

            issue = null;
            return true;
        }
    }


    [System.Serializable]
    public sealed class PlaceableTypeRegistry : TypeRegistry<PlaceableType>
    {
        protected override string ListName => "PlaceableTypes";
        protected override string TypeName => "PlaceableType";

        protected override bool ValidateItem(PlaceableType item, out string issue)
        {
            if (item.Prefab == null)
            {
                issue = $"PlaceableType '{item.Id}' is missing a prefab.";
                return false;
            }

            if (item.Prefab.GetComponent<Shared.Runtime.Placeables.PlaceableBehavior>() == null)
            {
                issue = $"PlaceableType '{item.Id}' prefab does not include a PlaceableBehavior.";
                return false;
            }

            if (item.Size.x <= 0 || item.Size.y <= 0)
            {
                issue = $"PlaceableType '{item.Id}' has invalid size ({item.Size.x}, {item.Size.y}).";
                return false;
            }

            var allowedTiles = item.AllowedTileTypes;
            if (allowedTiles == null || allowedTiles.Count == 0)
            {
                issue = $"PlaceableType '{item.Id}' must allow at least one TileType.";
                return false;
            }

            var seenTiles = new HashSet<TileType>();
            for (var i = 0; i < allowedTiles.Count; i++)
            {
                var tile = allowedTiles[i];
                if (tile == null)
                {
                    issue = $"PlaceableType '{item.Id}' contains a null allowed tile at index {i}.";
                    return false;
                }

                if (seenTiles.Add(tile))
                    continue;

                issue = $"PlaceableType '{item.Id}' contains duplicate allowed tile '{tile.Id}'.";
                return false;
            }

            issue = null;
            return true;
        }
    }

    [System.Serializable]
    public sealed class TagTypeRegistry : TypeRegistry<TagType>
    {
        protected override string ListName => "TagTypes";
        protected override string TypeName => "TagType";
    }

    [System.Serializable]
    public sealed class TileTypeRegistry : TypeRegistry<TileType>
    {
        protected override string ListName => "TileTypes";
        protected override string TypeName => "TileType";
    }
}
