using System;
using Shared.Data;
using Shared.Grid;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shared.Runtime
{
    public enum RuntimeBootstrapState : byte
    {
        Initializing = 0,
        Ready = 1,
    }

    public static class RuntimeBootstrap
    {
        private static RuntimeBootstrapDriver driver;

        public static RuntimeBootstrapState State { get; private set; } = RuntimeBootstrapState.Initializing;
        public static bool IsReady => State == RuntimeBootstrapState.Ready;

        public static event Action<RuntimeBootstrapState> StateChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SetState(RuntimeBootstrapState.Initializing);
            EnsureDriver();
        }

        public static bool TryGetBlockingIssue(out string issue)
        {
            if (GameRegistry.Instance == null)
            {
                issue = "GameRegistry is missing at Resources/GameRegistry.";
                return true;
            }

            if (NetworkManager.Singleton == null)
            {
                issue = "NetworkManager.Singleton is missing.";
                return true;
            }

            if (GridManager.Instance == null)
            {
                issue = "GridManager.Instance is missing.";
                return true;
            }

            if (PhaseManager.Instance == null)
            {
                issue = "PhaseManager.Instance is missing.";
                return true;
            }

            if (PlaceableSpawnSystem.Instance == null)
            {
                issue = "PlaceableSpawnSystem.Instance is missing.";
                return true;
            }

            issue = null;
            return false;
        }

        internal static void Refresh()
        {
            var next = TryGetBlockingIssue(out _) ? RuntimeBootstrapState.Initializing : RuntimeBootstrapState.Ready;
            SetState(next);
        }

        private static void EnsureDriver()
        {
            if (driver != null)
                return;

            var go = new GameObject("RuntimeBootstrapDriver");
            UnityEngine.Object.DontDestroyOnLoad(go);
            driver = go.AddComponent<RuntimeBootstrapDriver>();
        }

        private static void SetState(RuntimeBootstrapState nextState)
        {
            if (State == nextState)
                return;

            State = nextState;
            StateChanged?.Invoke(State);
        }

        private sealed class RuntimeBootstrapDriver : MonoBehaviour
        {
            private void OnEnable()
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
            }

            private void OnDisable()
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
            }

            private void Update()
            {
                RuntimeBootstrap.Refresh();
            }

            private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                RuntimeBootstrap.SetState(RuntimeBootstrapState.Initializing);
            }
        }
    }
}
