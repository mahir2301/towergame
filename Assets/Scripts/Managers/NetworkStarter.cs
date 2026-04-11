using Unity.Netcode;
using UnityEngine;

namespace Managers
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