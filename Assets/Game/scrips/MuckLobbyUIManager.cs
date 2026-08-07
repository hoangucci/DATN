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

        [Header("Lobby Code Panel (Bottom-Right)")]
        [SerializeField] private GameObject lobbyJoinPanel;
        [SerializeField] private TMP_InputField ipInputField;
        [SerializeField] private Button copyIpButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Scene Config")]
        [SerializeField] private string gameMapScene = "Map";

        private int lastPlayerCount = -1;

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

            // Gán listener cho nút bấm
            if (startButton != null) startButton.onClick.AddListener(OnStartGameClicked);
            if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
            if (copyIpButton != null) copyIpButton.onClick.AddListener(OnCopyIpClicked);

            // Tự động vào phòng chờ sẵn (Auto In-Room)
            if (lobbyMembersPanel != null) lobbyMembersPanel.SetActive(true);
            if (lobbyJoinPanel != null) lobbyJoinPanel.SetActive(true);

            string myIp = GetLocalIPAddress();
            if (ipInputField != null)
            {
                ipInputField.text = myIp;
            }

            UpdatePlayerList();
        }

        private void Update()
        {
            if (lanController != null && statusText != null)
            {
                statusText.text = lanController.StatusText;
            }

            int currentCount = (lanController != null && lanController.IsSessionActive) ? lanController.ConnectedPlayerCount : 1;
            if (currentCount != lastPlayerCount)
            {
                UpdatePlayerList();
            }
        }

        public void OnStartGameClicked()
        {
            Debug.Log($"[MuckLobby] Bắt đầu trận đấu...");

            IntroStoryManager storyManager = IntroStoryManager.Instance ?? Object.FindFirstObjectByType<IntroStoryManager>(FindObjectsInactive.Include);
            if (storyManager != null)
            {
                storyManager.StartStorySequence();
            }
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
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

            Debug.Log("[MuckLobby] Bấm Back rời phòng...");
        }

        public void OnCopyIpClicked()
        {
            string ip = ipInputField != null && !string.IsNullOrEmpty(ipInputField.text) ? ipInputField.text : GetLocalIPAddress();
            GUIUtility.systemCopyBuffer = ip;
            SetStatus($"Đã chép Mã Phòng: {ip} vào Clipboard!");
        }

        private void UpdatePlayerList()
        {
            if (playerListContainer == null) return;

            int count = (lanController != null && lanController.IsSessionActive) ? lanController.ConnectedPlayerCount : 1;
            lastPlayerCount = count;

            for (int i = playerListContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = playerListContainer.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            string currentUserName = FirebaseAuthManager.Instance != null
                ? FirebaseAuthManager.Instance.GetCurrentUserDisplayName()
                : "Player";

            if (lanController != null && lanController.IsSessionActive)
            {
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
                // Mặc định hiển thị bản thân [Host] trong phòng chờ sẵn
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
            text.color = new Color(0.95f, 0.85f, 0.6f);
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
            return "192.168.1.100";
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
            Debug.Log($"[MuckLobby] {msg}");
        }
    }
}
