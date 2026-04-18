using Shared.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace Server.Managers
{
    public class NetworkStarter : MonoBehaviour
    {
        [SerializeField] private bool autoStartHost = true;

        private void Start()
        {
            if (!autoStartHost)
                return;

            if (NetworkManager.Singleton == null)
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthMissingDependency,
                    "Cannot auto-start host because NetworkManager.Singleton is missing.");
                return;
            }

            if (NetworkManager.Singleton.IsListening)
                return;

            var started = NetworkManager.Singleton.StartHost();
            if (!started)
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthConfigIssue,
                    "StartHost() failed.");
        }
    }
}
