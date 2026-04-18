using UnityEngine;
using UnityEngine.SceneManagement;

namespace Server.Managers
{
    public static class ServerStartupHealthCheck
    {
        private const string LogPrefix = "[Health]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            var sceneName = SceneManager.GetActiveScene().name;

            var spawnManager = Object.FindFirstObjectByType<ServerSpawnManager>();
            if (spawnManager == null)
            {
                Debug.LogError($"{LogPrefix} Missing ServerSpawnManager in scene '{sceneName}'.");
            }
            else if (!spawnManager.HasRequiredReferences(out var spawnIssue))
            {
                Debug.LogError($"{LogPrefix} ServerSpawnManager configuration issue: {spawnIssue}");
            }

            var worldGenManager = Object.FindFirstObjectByType<WorldGenerationManager>();
            if (worldGenManager == null)
            {
                Debug.LogError($"{LogPrefix} Missing WorldGenerationManager in scene '{sceneName}'.");
            }
            else if (!worldGenManager.HasRequiredReferences(out var worldIssue))
            {
                Debug.LogError($"{LogPrefix} WorldGenerationManager configuration issue: {worldIssue}");
            }
        }
    }
}
