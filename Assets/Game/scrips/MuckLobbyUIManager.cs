using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using MidnightChaos.Networking;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    public class MuckLobbyUIManager : MonoBehaviour
    {
        public static MuckLobbyUIManager Instance { get; private set; }

        [Header("Networking References")]
        [SerializeField] private LanSessionController lanController;

        [Header("Lobby Members Panel (Top-Left)")]
        [SerializeField] private GameObject lobbyMembersPanel;
        [SerializeField] private Transform playerListContainer;
        [SerializeField] private GameObject playerListItemPrefab;
        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;

        [Header("Lobby Join / Host Panel (Bottom-Right)")]
        [SerializeField] private GameObject lobbyJoinPanel;
        [SerializeField] private TMP_InputField ipInputField;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button copyIpButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text localIpDisplayText;

        [Header("Scene Config")]
        [SerializeField] private string gameMapScene = "Map";

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            if (lanController == null)
            {
                lanController = FindFirstObjectByType<LanSessionController>();
            }

            // Gán các listener cho nút bấm
            if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
            if (startButton != null) startButton.onClick.AddListener(OnStartGameClicked);
            if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
            if (copyIpButton != null) copyIpButton.onClick.AddListener(OnCopyIpClicked);

            // Mặc định IP ô nhập là 127.0.0.1
            if (ipInputField != null && string.IsNullOrEmpty(ipInputField.text))
            {
                ipInputField.text = "127.0.0.1";
            }

            string myIp = GetLocalIPAddress();
            if (localIpDisplayText != null)
            {
                localIpDisplayText.text = $"IP của bạn: {myIp}";
            }

            UpdateUIState();
        }

        private void Update()
        {
            if (lanController != null && statusText != null)
            {
                statusText.text = lanController.StatusText;
            }

            UpdatePlayerList();
        }

        public void OnHostClicked()
        {
            if (lanController == null)
            {
                SetStatus("Lỗi: Không tìm thấy LanSessionController!");
                return;
            }

            if (lanController.StartHost(lanController.DefaultPort, out string error))
            {
                SetStatus("Đã mở phòng Host LAN thành công!");
                if (lobbyMembersPanel != null) lobbyMembersPanel.SetActive(true);
            }
            else
            {
                SetStatus($"Không thể Host: {error}");
            }

            UpdateUIState();
        }

        public void OnJoinClicked()
        {
            if (lanController == null)
            {
                SetStatus("Lỗi: Không tìm thấy LanSessionController!");
                return;
            }

            string targetIp = ipInputField != null ? ipInputField.text.Trim() : "127.0.0.1";
            if (string.IsNullOrEmpty(targetIp)) targetIp = "127.0.0.1";

            if (lanController.StartClient(targetIp, lanController.DefaultPort, out string error))
            {
                SetStatus($"Đang kết nối tới IP {targetIp}...");
                if (lobbyMembersPanel != null) lobbyMembersPanel.SetActive(true);
            }
            else
            {
                SetStatus($"Kết nối thất bại: {error}");
            }

            UpdateUIState();
        }

        public void OnStartGameClicked()
        {
            if (lanController != null && !lanController.IsHost)
            {
                SetStatus("Chỉ Host mới có quyền bấm Start!");
                return;
            }

            Debug.Log($"[MuckLobby] Host đang bắt đầu trận đấu, tải scene: {gameMapScene}");
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(gameMapScene, LoadSceneMode.Single);
            }
            else
            {
                SceneManager.LoadScene(gameMapScene);
            }
        }

        public void OnBackClicked()
        {
            if (lanController != null)
            {
                lanController.Shutdown();
            }

            if (lobbyMembersPanel != null) lobbyMembersPanel.SetActive(false);
            SetStatus("Đã ngắt kết nối.");
            UpdateUIState();
        }

        public void OnCopyIpClicked()
        {
            string ip = GetLocalIPAddress();
            GUIUtility.systemCopyBuffer = ip;
            SetStatus($"Đã chép IP: {ip} vào Clipboard!");
        }

        private void UpdateUIState()
        {
            bool isSessionActive = lanController != null && lanController.IsSessionActive;
            bool isHost = lanController != null && lanController.IsHost;

            if (startButton != null)
            {
                startButton.gameObject.SetActive(isHost);
            }
        }

        private void UpdatePlayerList()
        {
            if (playerListContainer == null) return;

            // Xoá danh sách cũ
            foreach (Transform child in playerListContainer)
            {
                Destroy(child.gameObject);
            }

            string currentUserName = FirebaseAuthManager.Instance != null
                ? FirebaseAuthManager.Instance.GetCurrentUserDisplayName()
                : "Player";

            if (lanController != null && lanController.IsSessionActive)
            {
                int count = lanController.ConnectedPlayerCount;
                for (int i = 0; i < count; i++)
                {
                    GameObject item = Instantiate(playerListItemPrefab != null ? playerListItemPrefab : CreateDefaultMemberItem(), playerListContainer);
                    TMP_Text txt = item.GetComponentInChildren<TMP_Text>();
                    if (txt != null)
                    {
                        string hostTag = (i == 0 && lanController.IsHost) ? " [Host]" : "";
                        txt.text = $"{currentUserName}_{(i + 1)}{hostTag}";
                    }
                }
            }
            else
            {
                // Mặc định hiển thị bản thân khi chưa vào phòng
                GameObject item = Instantiate(playerListItemPrefab != null ? playerListItemPrefab : CreateDefaultMemberItem(), playerListContainer);
                TMP_Text txt = item.GetComponentInChildren<TMP_Text>();
                if (txt != null)
                {
                    txt.text = $"{currentUserName} [Host]";
                }
            }
        }

        private GameObject CreateDefaultMemberItem()
        {
            GameObject item = new GameObject("MemberItem", typeof(RectTransform));
            TMP_Text text = item.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18;
            text.color = new Color(0.95f, 0.85f, 0.6f); // Wooden gold text
            text.alignment = TextAlignmentOptions.Left;
            return item;
        }

        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
            Debug.Log($"[MuckLobby] {msg}");
        }
    }
}
