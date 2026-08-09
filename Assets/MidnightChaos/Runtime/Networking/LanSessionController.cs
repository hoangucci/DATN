using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace MidnightChaos.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    [RequireComponent(typeof(UnityTransport))]
    public sealed class LanSessionController : MonoBehaviour
    {
        public const ushort CurrentProtocolVersion = 10;

        [Header("LAN")]
        [SerializeField] private ushort defaultPort = LanEndpointValidator.DefaultPort;
        [SerializeField, Range(1, 8)] private int maxPlayers = 8;
        [SerializeField] private float spawnSpacing = 2.5f;
        [SerializeField] private GameObject playerPrefab;

        private NetworkManager networkManager;
        private UnityTransport transport;
        private bool operationInProgress;

        public string StatusText { get; private set; } = "Idle";
        public string LastError { get; private set; } = string.Empty;
        public ushort DefaultPort => defaultPort;
        public bool OperationInProgress => operationInProgress;
        public bool IsSessionActive => networkManager != null && networkManager.IsListening;
        public bool IsHost => networkManager != null && networkManager.IsHost;
        public int ConnectedPlayerCount =>
            networkManager != null && networkManager.IsServer
                ? networkManager.ConnectedClientsIds.Count
                : (networkManager != null && networkManager.IsConnectedClient ? 1 : 0);

        public void Configure(GameObject configuredPlayerPrefab)
        {
            playerPrefab = configuredPlayerPrefab;
        }

        private void Awake()
        {
            CacheDependencies();
            Application.runInBackground = true;
        }

        private void OnEnable()
        {
            CacheDependencies();
            EnsureNetworkConfiguration();
            BindNetworkCallbacks();
        }

        private void BindNetworkCallbacks()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.ConnectionApprovalCallback = ApproveConnection;
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        public bool StartSinglePlayer(out string error)
        {
            return StartHost(defaultPort, out error);
        }

        public bool StartHost(ushort port, out string error)
        {
            if (!CanStartOperation(out error))
            {
                return false;
            }

            operationInProgress = true;
            LastError = string.Empty;
            StatusText = $"Starting Host on UDP {port}...";

            // The remote address is irrelevant while listening. 0.0.0.0 exposes
            // the Host on all local network adapters.
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");

            if (!networkManager.StartHost())
            {
                operationInProgress = false;
                error = "Không thể mở Host. Kiểm tra port hoặc NetworkManager.";
                SetError(error);
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool StartClient(string rawAddress, ushort port, out string error)
        {
            if (!CanStartOperation(out error))
            {
                return false;
            }

            if (!LanEndpointValidator.TryValidateIpv4(rawAddress, out string address, out error))
            {
                SetError(error);
                return false;
            }

            operationInProgress = true;
            LastError = string.Empty;
            StatusText = $"Connecting to {address}:{port}...";
            transport.SetConnectionData(address, port);

            if (!networkManager.StartClient())
            {
                operationInProgress = false;
                error = "Không thể bắt đầu Client. Session cũ có thể chưa được dọn sạch.";
                SetError(error);
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Shutdown()
        {
            if (networkManager == null)
            {
                return;
            }

            if (networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            operationInProgress = false;
            LastError = string.Empty;
            StatusText = "Idle";
        }

        private bool CanStartOperation(out string error)
        {
            if (networkManager == null || transport == null)
            {
                error = "Thiếu NetworkManager hoặc UnityTransport.";
                SetError(error);
                return false;
            }

            EnsureNetworkConfiguration();
            BindNetworkCallbacks();

            if (networkManager.NetworkConfig.NetworkTransport == null)
            {
                error = "NetworkManager chưa được gán UnityTransport.";
                SetError(error);
                return false;
            }

            if (networkManager.NetworkConfig.PlayerPrefab == null)
            {
                error =
                    "NetworkManager chưa có Player Prefab. " +
                    "Hãy chạy lại Create or Refresh LAN Test Scene.";
                SetError(error);
                return false;
            }

            if (operationInProgress || networkManager.IsListening)
            {
                error = "Một session hoặc thao tác kết nối đang hoạt động.";
                SetError(error);
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void CacheDependencies()
        {
            networkManager ??= GetComponent<NetworkManager>();
            transport ??= GetComponent<UnityTransport>();
        }

        private void EnsureNetworkConfiguration()
        {
            if (networkManager == null || transport == null || networkManager.IsListening)
            {
                return;
            }

            if (networkManager.NetworkConfig.NetworkTransport != transport)
            {
                networkManager.NetworkConfig.NetworkTransport = transport;
            }

            if (playerPrefab == null)
            {
                playerPrefab = networkManager.NetworkConfig.PlayerPrefab;
            }
            else if (networkManager.NetworkConfig.PlayerPrefab != playerPrefab)
            {
                networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
            }

            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.NetworkConfig.ProtocolVersion =
                CurrentProtocolVersion;
        }

        private void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            bool lobbyFull = networkManager.ConnectedClientsIds.Count >= maxPlayers;

            response.Approved = !lobbyFull;
            response.CreatePlayerObject = !lobbyFull;
            response.PlayerPrefabHash = null;
            response.Pending = false;
            response.Reason = lobbyFull ? "Lobby full" : string.Empty;

            if (!lobbyFull)
            {
                float angle = request.ClientNetworkId * 137.5f * Mathf.Deg2Rad;
                float radius = Mathf.Max(1f, request.ClientNetworkId) * spawnSpacing;
                response.Position = new Vector3(Mathf.Cos(angle) * radius, 1f, Mathf.Sin(angle) * radius);
                response.Rotation = Quaternion.identity;
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            operationInProgress = false;
            LastError = string.Empty;

            if (networkManager.IsHost)
            {
                StatusText = $"Host active - {networkManager.ConnectedClientsIds.Count}/{maxPlayers} players";
            }
            else
            {
                StatusText = $"Connected - Client ID {clientId}";
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            operationInProgress = false;

            if (networkManager.IsServer && networkManager.IsListening)
            {
                StatusText = $"Host active - {networkManager.ConnectedClientsIds.Count}/{maxPlayers} players";
                return;
            }

            string reason = networkManager.DisconnectReason;
            SetError(string.IsNullOrWhiteSpace(reason) ? "Mất kết nối với Host." : reason);
            StatusText = "Disconnected";
        }

        private void SetError(string message)
        {
            LastError = message ?? string.Empty;
        }

        private void OnDisable()
        {
            UnbindNetworkCallbacks();
        }

        private void UnbindNetworkCallbacks()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.ConnectionApprovalCallback = null;
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        private void OnDestroy()
        {
            UnbindNetworkCallbacks();
        }
    }
}
