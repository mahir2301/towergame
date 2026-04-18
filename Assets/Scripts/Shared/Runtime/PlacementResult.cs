namespace Shared.Runtime
{
    public enum PlacementResult : byte
    {
        Success = 0,
        ServerUnavailable = 1,
        MissingDependencies = 2,
        InvalidTowerType = 3,
        OutOfBuildPhase = 4,
        InvalidGridPosition = 5,
        CellBlocked = 6,
        OutOfEnergyRange = 7,
        InvalidPrefab = 8,
    }
}
