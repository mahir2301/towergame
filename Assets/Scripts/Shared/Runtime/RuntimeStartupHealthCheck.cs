using Shared.Data;
using Shared.Grid;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shared.Runtime
{
    public static class RuntimeStartupHealthCheck
    {
        private const string LogPrefix = "[Health]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            var hasError = false;

            if (GameRegistry.Instance == null)
            {
                Debug.LogError($"{LogPrefix} Missing GameRegistry at Resources/GameRegistry in scene '{sceneName}'.");
                hasError = true;
            }

            if (NetworkManager.Singleton == null)
            {
                Debug.LogError($"{LogPrefix} Missing NetworkManager.Singleton in scene '{sceneName}'.");
                hasError = true;
            }

            if (GridManager.Instance == null)
            {
                Debug.LogError($"{LogPrefix} Missing GridManager.Instance in scene '{sceneName}'.");
                hasError = true;
            }

            if (PhaseManager.Instance == null)
            {
                Debug.LogError($"{LogPrefix} Missing PhaseManager.Instance in scene '{sceneName}'.");
                hasError = true;
            }

            if (Object.FindFirstObjectByType<TowerSpawnSystem>() == null)
            {
                Debug.LogError($"{LogPrefix} Missing TowerSpawnSystem in scene '{sceneName}'.");
                hasError = true;
            }

            if (!hasError)
                Debug.Log($"{LogPrefix} Startup checks passed for scene '{sceneName}'.");
        }
    }
}
