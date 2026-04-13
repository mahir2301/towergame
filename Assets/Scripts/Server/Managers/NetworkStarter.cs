using Unity.Netcode;
using UnityEngine;

namespace Server.Managers
{
    public class NetworkStarter : MonoBehaviour
    {
        private void Start()
        {
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.StartHost();
            }
        }
    }
}
