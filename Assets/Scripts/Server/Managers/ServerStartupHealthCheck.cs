using Shared.Runtime;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Server.Managers
{
    public static class ServerStartupHealthCheck
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (sceneName != RuntimeSceneNames.Game)
                return;

            if (!RuntimeNet.IsServer)
                return;

            var spawnManager = Object.FindFirstObjectByType<ServerSpawnManager>();
            if (spawnManager == null)
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthMissingDependency,
                    $"Missing ServerSpawnManager in scene '{sceneName}'.");
            }
            else if (!spawnManager.HasRequiredReferences(out var spawnIssue))
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthConfigIssue,
                    $"ServerSpawnManager configuration issue: {spawnIssue}");
            }

            var worldGenManager = Object.FindFirstObjectByType<WorldGenerationManager>();
            if (worldGenManager == null)
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthMissingDependency,
                    $"Missing WorldGenerationManager in scene '{sceneName}'.");
            }
            else if (!worldGenManager.HasRequiredReferences(out var worldIssue))
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthConfigIssue,
                    $"WorldGenerationManager configuration issue: {worldIssue}");
            }

            if (Object.FindFirstObjectByType<EnergyNetworkManager>() == null)
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthMissingDependency,
                    $"Missing EnergyNetworkManager in scene '{sceneName}'.");
            }

            if (Object.FindFirstObjectByType<ServerEntityBootstrap>() == null)
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthMissingDependency,
                    $"Missing ServerEntityBootstrap in scene '{sceneName}'.");
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.NetworkConfig != null && networkManager.NetworkConfig.PlayerPrefab != null)
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthConfigIssue,
                    "NetworkManager.NetworkConfig.PlayerPrefab must be null; player spawning is server-managed by ServerEntityBootstrap.");
            }
        }
    }
}
