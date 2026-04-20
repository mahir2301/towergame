using Unity.Netcode;

namespace Shared.Utilities
{
    public static class RuntimeNet
    {
        public static bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        public static bool IsClient => NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient;
        public static bool IsListening => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        public static bool ShouldRunClientSystems()
        {
            return !IsListening || IsClient;
        }

        public static bool IsLocalClient(ulong clientId)
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == clientId;
        }
    }
}
