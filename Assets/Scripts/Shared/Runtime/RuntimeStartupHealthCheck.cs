using Shared.Data;
using Shared.Grid;
using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shared.Runtime
{
    public static class RuntimeStartupHealthCheck
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (sceneName != RuntimeSceneNames.Game)
                return;

            var hasError = false;

            if (GameRegistry.Instance == null)
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthMissingDependency,
                    $"Missing GameRegistry at Resources/GameRegistry in scene '{sceneName}'.");
                hasError = true;
            }
            else if (!GameRegistry.Instance.ValidateAllTypes(out var registryIssue))
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthConfigIssue,
                    $"GameRegistry validation failed: {registryIssue}");
                hasError = true;
            }

            if (NetworkManager.Singleton == null)
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthMissingDependency,
                    $"Missing NetworkManager.Singleton in scene '{sceneName}'.");
                hasError = true;
            }

            if (GridManager.Instance == null)
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthMissingDependency,
                    $"Missing GridManager.Instance in scene '{sceneName}'.");
                hasError = true;
            }

            if (PhaseManager.Instance == null)
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthMissingDependency,
                    $"Missing PhaseManager.Instance in scene '{sceneName}'.");
                hasError = true;
            }

            if (Object.FindFirstObjectByType<PlaceableSpawnSystem>() == null)
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthMissingDependency,
                    $"Missing PlaceableSpawnSystem in scene '{sceneName}'.");
                hasError = true;
            }

            if (!hasError)
                RuntimeLog.Health.Info(RuntimeLog.Code.HealthStartupOk,
                    $"Startup checks passed for scene '{sceneName}'.");
        }
    }
}
