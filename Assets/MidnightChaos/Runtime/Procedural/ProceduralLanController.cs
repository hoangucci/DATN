using MidnightChaos.Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace MidnightChaos.Procedural
{
    [DisallowMultipleComponent]
    public sealed class ProceduralLanController : MonoBehaviour
    {
        public const ushort ProtocolVersion = 12;

        [Header("LAN")]
        [Tooltip("Cổng UDP mặc định cho Host/Client trong procedural demo.")]
        [SerializeField] private ushort defaultPort =
            LanEndpointValidator.DefaultPort;

        private NetworkManager networkManager;
        private UnityTransport transport;

        public string StatusText { get; private set; } = "Offline";
        public string LastError { get; private set; } = string.Empty;
        public bool OperationInProgress { get; private set; }
        public bool IsSessionActive =>
            networkManager != null && networkManager.IsListening;
        public bool IsHost => networkManager != null && networkManager.IsHost;
        public ushort DefaultPort => defaultPort == 0
            ? LanEndpointValidator.DefaultPort
            : defaultPort;

        private void OnValidate()
        {
            if (defaultPort == 0)
            {
                defaultPort = LanEndpointValidator.DefaultPort;
            }
        }

        public void Initialize(
            NetworkManager configuredNetworkManager,
            UnityTransport configuredTransport)
        {
            networkManager = configuredNetworkManager;
            transport = configuredTransport;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            networkManager.OnServerStopped += HandleServerStopped;
        }

        public bool StartHost(ushort port, out string error)
        {
            if (!CanStart(out error))
            {
                return false;
            }

            OperationInProgress = true;
            LastError = string.Empty;
            StatusText = $"Starting Host on UDP {port}...";
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");

            if (!networkManager.StartHost())
            {
                OperationInProgress = false;
                error = "Không thể khởi động Host.";
                SetError(error);
                return false;
            }

            error = string.Empty;
            TriggerStorySequence();
            return true;
        }

        private void TriggerStorySequence()
        {
            var storyManager = Object.FindFirstObjectByType<MidnightChaos.UI.IntroStoryManager>(FindObjectsInactive.Include);
            if (storyManager != null)
            {
                storyManager.StartStorySequence();
            }
        }

        public bool StartClient(string rawAddress, ushort port, out string error)
        {
            if (!CanStart(out error))
            {
                return false;
            }
            if (!LanEndpointValidator.TryValidateIpv4(
                    rawAddress,
                    out string address,
                    out error))
            {
                SetError(error);
                return false;
            }

            OperationInProgress = true;
            LastError = string.Empty;
            StatusText = $"Connecting to {address}:{port}...";
            transport.SetConnectionData(address, port);

            if (!networkManager.StartClient())
            {
                OperationInProgress = false;
                error = "Không thể khởi động Client.";
                SetError(error);
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Shutdown()
        {
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            OperationInProgress = false;
            LastError = string.Empty;
            StatusText = "Offline";
        }

        private bool CanStart(out string error)
        {
            if (networkManager == null || transport == null)
            {
                error = "Thiếu NetworkManager hoặc UnityTransport.";
                SetError(error);
                return false;
            }
            if (OperationInProgress || networkManager.IsListening)
            {
                error = "Một LAN session đang hoạt động.";
                SetError(error);
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void HandleClientConnected(ulong clientId)
        {
            OperationInProgress = false;
            LastError = string.Empty;
            StatusText = networkManager.IsHost
                ? $"Host active - {networkManager.ConnectedClientsIds.Count} peer(s)"
                : $"Connected - Client {clientId}";
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            OperationInProgress = false;
            if (networkManager.IsServer && networkManager.IsListening)
            {
                StatusText =
                    $"Host active - {networkManager.ConnectedClientsIds.Count} peer(s)";
                return;
            }

            string reason = networkManager.DisconnectReason;
            SetError(string.IsNullOrWhiteSpace(reason)
                ? "Disconnected from Host."
                : reason);
            StatusText = "Offline";
        }

        private void HandleServerStopped(bool wasHost)
        {
            OperationInProgress = false;
            StatusText = "Offline";
        }

        private void SetError(string error)
        {
            LastError = error ?? string.Empty;
        }

        private void OnDestroy()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            networkManager.OnServerStopped -= HandleServerStopped;
        }
    }
}
