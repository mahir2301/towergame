using UnityEngine;

namespace Shared.Utilities
{
    public static class RuntimeLog
    {
        public static readonly LogChannel Placement = new("[Placement]");
        public static readonly LogChannel WorldGen = new("[WorldGen]");
        public static readonly LogChannel Water = new("[Water]");
        public static readonly LogChannel Health = new("[Health]");
        public static readonly LogChannel Phase = new("[Phase]");
        public static readonly LogChannel Overlay = new("[Overlay]");
        public static readonly LogChannel Visual = new("[Visual]");
        public static readonly LogChannel Entity = new("[Entity]");

        public static class Code
        {
            public const string PlacementClientBlocked = "PL-001";
            public const string PlacementInvalidTower = "PL-002";
            public const string PlacementNoHandler = "PL-003";
            public const string PlacementAccepted = "PL-004";
            public const string PlacementRejected = "PL-005";
            public const string PlacementServerResult = "PL-006";
            public const string WorldGenStart = "WG-001";
            public const string WorldGenTerrainDone = "WG-002";
            public const string WorldGenTerrainFailed = "WG-003";
            public const string WorldGenEnergyDone = "WG-004";
            public const string WorldGenEnergyFailed = "WG-005";
            public const string WorldGenStatePublished = "WG-006";
            public const string WaterRebuilt = "WT-001";
            public const string WaterMissingGrid = "WT-002";
            public const string HealthMissingDependency = "HL-001";
            public const string HealthConfigIssue = "HL-002";
            public const string HealthStartupOk = "HL-003";
            public const string PhaseChanged = "PH-001";
            public const string OverlayMissingDocument = "OV-001";
            public const string OverlayMissingEnergyLabel = "OV-002";
            public const string OverlayMissingTowerIndicator = "OV-003";
            public const string VisualMissingGroundShader = "VS-001";
            public const string EntitySpawned = "EN-001";
            public const string EntityDespawned = "EN-002";
            public const string EntityPlayerAssigned = "EN-003";
            public const string EntityOwnershipRejected = "EN-004";
            public const string EntityInvalidCommandPayload = "EN-005";
            public const string EntityMissingForCommand = "EN-006";
            public const string EntityProjectileHitResolved = "EN-007";
            public const string EntityDisconnectCleanup = "EN-008";
            public const string EntityMissingTypeDefinition = "EN-009";
            public const string EntityMissingTypeId = "EN-010";
            public const string EntitySpawnFailed = "EN-011";
        }

        public readonly struct LogChannel
        {
            private readonly string category;

            public LogChannel(string categoryName)
            {
                category = categoryName;
            }

            public void Info(string code, string message)
            {
                Debug.Log($"{category}[{code}] {message}");
            }

            public void Warning(string code, string message)
            {
                Debug.LogWarning($"{category}[{code}] {message}");
            }

            public void Error(string code, string message)
            {
                Debug.LogError($"{category}[{code}] {message}");
            }
        }
    }
}
