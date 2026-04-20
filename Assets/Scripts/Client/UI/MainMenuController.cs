using Shared.Runtime;
using Shared.Utilities;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Client.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private UIDocument menuDocument;
        [SerializeField] private string gameSceneName = RuntimeSceneNames.Game;
        [SerializeField] private string defaultAddress = "127.0.0.1";
        [SerializeField] private int defaultPort = 7777;

        private Button hostButton;
        private Button joinButton;
        private Label statusLabel;
        private NetworkManager subscribedNetworkManager;

        private void Awake()
        {
            if (!RuntimeNet.ShouldRunMenuSystems())
            {
                enabled = false;
                return;
            }

            if (menuDocument == null)
            {
                RuntimeLog.Health.Error(RuntimeLog.Code.HealthMissingDependency,
                    "MainMenuController requires a menu UIDocument reference.");
                enabled = false;
            }
        }

        private void Start()
        {
            BindMenuUI();
            SubscribeToNetworkCallbacks();
            RefreshMenuState();
        }

        private void OnEnable()
        {
            if (hostButton == null)
                BindMenuUI();

            SubscribeToNetworkCallbacks();
            RefreshMenuState();
        }

        private void OnDisable()
        {
            UnsubscribeFromNetworkCallbacks();
            UnbindMenuUI();
        }

        private void OnDestroy()
        {
            UnsubscribeFromNetworkCallbacks();
            UnbindMenuUI();
        }

        private void BindMenuUI()
        {
            if (menuDocument == null)
                return;

            var root = menuDocument.rootVisualElement;
            if (root == null)
                return;

            hostButton = root.Q<Button>("host-button");
            joinButton = root.Q<Button>("join-button");
            statusLabel = root.Q<Label>("status-label");

            if (hostButton != null)
                hostButton.clicked += HandleHostClicked;

            if (joinButton != null)
                joinButton.clicked += HandleJoinClicked;

            SetStatus("Choose Host LAN to start or Join LAN to connect.");
        }

        private void UnbindMenuUI()
        {
            if (hostButton != null)
                hostButton.clicked -= HandleHostClicked;

            if (joinButton != null)
                joinButton.clicked -= HandleJoinClicked;

            hostButton = null;
            joinButton = null;
            statusLabel = null;
        }

        private void SubscribeToNetworkCallbacks()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || subscribedNetworkManager == networkManager)
                return;

            UnsubscribeFromNetworkCallbacks();
            subscribedNetworkManager = networkManager;
            subscribedNetworkManager.OnServerStarted += HandleServerStarted;
            subscribedNetworkManager.OnClientConnectedCallback += HandleClientConnected;
            subscribedNetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        private void UnsubscribeFromNetworkCallbacks()
        {
            if (subscribedNetworkManager == null)
                return;

            subscribedNetworkManager.OnServerStarted -= HandleServerStarted;
            subscribedNetworkManager.OnClientConnectedCallback -= HandleClientConnected;
            subscribedNetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            subscribedNetworkManager = null;
        }

        private void HandleHostClicked()
        {
            var port = GetPort();
            RuntimeLog.Menu.Info(RuntimeLog.Code.MenuHostRequested,
                $"Host requested for {defaultAddress}:{port}.");

            if (!TryGetNetworkManager(out var networkManager))
                return;

            if (networkManager.IsListening)
            {
                RuntimeLog.Menu.Warning(RuntimeLog.Code.MenuSessionAlreadyRunning,
                    "Ignored host request because a session is already running.");
                SetStatus("A session is already running.");
                RefreshMenuState();
                return;
            }

            if (!TryConfigureTransport(networkManager, defaultAddress, port, "0.0.0.0"))
                return;

            if (!networkManager.StartHost())
            {
                RuntimeLog.Menu.Error(RuntimeLog.Code.MenuStartHostFailed,
                    $"StartHost failed for {defaultAddress}:{port}.");
                SetStatus("StartHost failed. Check the console for details.");
                SetButtonsEnabled(true);
                return;
            }

            SetStatus("Host started. Loading game scene...");
            SetButtonsEnabled(false);
            RuntimeLog.Menu.Info(RuntimeLog.Code.MenuLoadGameScene,
                $"Host started; loading scene '{gameSceneName}'.");

            if (networkManager.SceneManager == null)
            {
                RuntimeLog.Menu.Warning(RuntimeLog.Code.MenuLoadGameScene,
                    $"NetworkSceneManager unavailable; loading '{gameSceneName}' locally.");
                SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
                return;
            }

            var status = networkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                RuntimeLog.Menu.Error(RuntimeLog.Code.MenuLoadGameScene,
                    $"Failed to load scene '{gameSceneName}' via NetworkSceneManager: {status}.");
                SetStatus($"Failed to load '{gameSceneName}'.");
                SetButtonsEnabled(true);
            }
        }

        private void HandleJoinClicked()
        {
            var port = GetPort();
            RuntimeLog.Menu.Info(RuntimeLog.Code.MenuJoinRequested,
                $"Join requested for {defaultAddress}:{port}.");

            if (!TryGetNetworkManager(out var networkManager))
                return;

            if (networkManager.IsListening)
            {
                RuntimeLog.Menu.Warning(RuntimeLog.Code.MenuSessionAlreadyRunning,
                    "Ignored join request because a session is already running.");
                SetStatus("A session is already running.");
                RefreshMenuState();
                return;
            }

            if (!TryConfigureTransport(networkManager, defaultAddress, port, "0.0.0.0"))
                return;

            if (!networkManager.StartClient())
            {
                RuntimeLog.Menu.Error(RuntimeLog.Code.MenuStartClientFailed,
                    $"StartClient failed for {defaultAddress}:{port}.");
                SetStatus("StartClient failed. Check the console for details.");
                SetButtonsEnabled(true);
                return;
            }

            SetStatus($"Joining {defaultAddress}:{port}...");
            SetButtonsEnabled(false);
        }

        private bool TryGetNetworkManager(out NetworkManager networkManager)
        {
            networkManager = NetworkManager.Singleton;
            if (networkManager != null)
                return true;

            RuntimeLog.Menu.Error(RuntimeLog.Code.MenuNetworkUnavailable,
                "NetworkManager.Singleton is missing.");
            SetStatus("NetworkManager.Singleton is missing.");
            return false;
        }

        private bool TryConfigureTransport(NetworkManager networkManager, string address, ushort port, string listenAddress)
        {
            if (networkManager.NetworkConfig?.NetworkTransport is not UnityTransport transport)
            {
                RuntimeLog.Menu.Error(RuntimeLog.Code.MenuMissingTransport,
                    "UnityTransport is not configured on NetworkManager.");
                SetStatus("UnityTransport is required for LAN host/join.");
                return false;
            }

            transport.SetConnectionData(address, port, listenAddress);
            RuntimeLog.Menu.Info(RuntimeLog.Code.MenuTransportConfigured,
                $"Configured transport: address={address}, port={port}, listen={listenAddress}.");
            return true;
        }

        private void HandleServerStarted()
        {
            RuntimeLog.Menu.Info(RuntimeLog.Code.MenuServerStarted,
                "Server started and is waiting for clients.");
            SetButtonsEnabled(false);
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (!RuntimeNet.IsLocalClient(clientId))
                return;

            RuntimeLog.Menu.Info(RuntimeLog.Code.MenuClientConnected,
                $"Local client connected with id {clientId}.");
            SetStatus(RuntimeNet.IsServer ? "Host started." : "Connected to host. Waiting for scene sync...");
            SetButtonsEnabled(false);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!RuntimeNet.IsLocalClient(clientId))
                return;

            RuntimeLog.Menu.Warning(RuntimeLog.Code.MenuClientDisconnected,
                $"Local client disconnected with id {clientId}.");
            SetStatus("Disconnected from session.");
            SetButtonsEnabled(true);
        }

        private void RefreshMenuState()
        {
            var canStart = !RuntimeNet.IsListening;
            SetButtonsEnabled(canStart);
        }

        private ushort GetPort()
        {
            var configuredPort = defaultPort;
            configuredPort = Mathf.Clamp(configuredPort, 1, 65535);

            return (ushort)configuredPort;
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
                statusLabel.text = message;
        }

        private void SetButtonsEnabled(bool isEnabled)
        {
            hostButton?.SetEnabled(isEnabled);
            joinButton?.SetEnabled(isEnabled);
        }
    }
}
