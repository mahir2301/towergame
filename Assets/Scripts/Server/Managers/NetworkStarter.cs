using Unity.Netcode;
using UnityEngine;

namespace Server.Managers
{
    public class NetworkStarter : MonoBehaviour
    {
        private const string LogPrefix = "[Network]";

        [SerializeField] private bool autoStartHost = true;

        private void Start()
        {
            if (!autoStartHost)
                return;

            if (NetworkManager.Singleton == null)
            {
                Debug.LogError($"{LogPrefix} Cannot auto-start host because NetworkManager.Singleton is missing.");
                return;
            }

            if (NetworkManager.Singleton.IsListening)
                return;

            var started = NetworkManager.Singleton.StartHost();
            if (!started)
                Debug.LogError($"{LogPrefix} StartHost() failed.");
        }
    }
}
