using System;
using Shared.Data;
using Shared.Grid;
using UnityEngine;

namespace Shared.Runtime
{
    public static class PlacementValidator
    {
        public static PlacementResult ValidatePlacement(Vector2Int gridPos, TowerType towerConfig, GridManager gridManager,
            PhaseManager phaseManager, Func<Vector2Int, ClassType, int, bool> energyRangeEvaluator)
        {
            if (gridManager == null || phaseManager == null || energyRangeEvaluator == null)
                return PlacementResult.MissingDependencies;

            if (towerConfig == null || towerConfig.Prefab == null || towerConfig.ClassType == null)
                return PlacementResult.InvalidTowerType;

            if (phaseManager.CurrentPhase != GamePhase.Building)
                return PlacementResult.OutOfBuildPhase;

            if (!gridManager.IsValidPosition(gridPos))
                return PlacementResult.InvalidGridPosition;

            if (!gridManager.IsCellAvailable(gridPos, towerConfig.Size, towerConfig.CanBePlacedOnWater))
                return PlacementResult.CellBlocked;

            var isInRange = energyRangeEvaluator(gridPos, towerConfig.ClassType, towerConfig.Stats.energyCost);
            return isInRange ? PlacementResult.Success : PlacementResult.OutOfEnergyRange;
        }

        public static bool IsPlacementAllowed(PlacementResult result, bool allowOutOfEnergyRange)
        {
            return result == PlacementResult.Success
                   || (allowOutOfEnergyRange && result == PlacementResult.OutOfEnergyRange);
        }
    }
}
