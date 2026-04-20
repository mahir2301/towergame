using Client.Visuals;
using Shared.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Controllers
{
    public static class ClientStartupHealthCheck
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            var sceneName = SceneManager.GetActiveScene().name;

            if (!RuntimeNet.ShouldRunClientSystems())
                return;

            var hasError = false;

            var resolver = Object.FindFirstObjectByType<LocalPlayerEntityResolver>();
            if (resolver == null)
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthMissingDependency,
                    $"Missing LocalPlayerEntityResolver in scene '{sceneName}'.");
                hasError = true;
            }

            var inputDriver = Object.FindFirstObjectByType<LocalPlayerInputDriver>();
            if (inputDriver == null)
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthMissingDependency,
                    $"Missing LocalPlayerInputDriver in scene '{sceneName}'.");
                hasError = true;
            }
            else if (!inputDriver.HasRequiredReferences(out var inputIssue))
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthConfigIssue,
                    $"LocalPlayerInputDriver configuration issue: {inputIssue}");
                hasError = true;
            }

            var cameraBinder = Object.FindFirstObjectByType<LocalPlayerCameraBinder>();
            if (cameraBinder == null)
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthMissingDependency,
                    $"Missing LocalPlayerCameraBinder in scene '{sceneName}'.");
                hasError = true;
            }
            else if (!cameraBinder.HasRequiredReferences(out var cameraIssue))
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthConfigIssue,
                    $"LocalPlayerCameraBinder configuration issue: {cameraIssue}");
                hasError = true;
            }

            var terrainRenderer = Object.FindFirstObjectByType<ClientWorldTerrainRenderer>();
            if (terrainRenderer == null)
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthMissingDependency,
                    $"Missing ClientWorldTerrainRenderer in scene '{sceneName}'.");
                hasError = true;
            }
            else if (!terrainRenderer.HasRequiredReferences(out var terrainIssue))
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthConfigIssue,
                    $"ClientWorldTerrainRenderer configuration issue: {terrainIssue}");
                hasError = true;
            }

            if (!hasError)
            {
                RuntimeLog.Health.Info(RuntimeLog.Code.HealthStartupOk,
                    $"Client startup checks passed for scene '{sceneName}'.");
            }
        }
    }
}
