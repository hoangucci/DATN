using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    public static MainMenuManager Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject settingsOverlayBackdrop;
    [SerializeField] private GameObject authCanvasGroup; // Hoặc Panel chứa toàn bộ Auth UI
    [SerializeField] private GameObject gameLogoObject;

    [Header("User Profile UI")]
    [SerializeField] private TMP_Text userNameText;
    [SerializeField] private TMP_Text userEmailText;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button closeSettingsButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private Button quitButton;

    [Header("Scene Config")]
    [SerializeField] private string gameSceneName = "Map"; // Scene khi bấm Play
    [SerializeField] private string loginSceneName = "Login"; // Scene màn hình Đăng Nhập khi bấm Logout

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        if (string.IsNullOrEmpty(gameSceneName) || gameSceneName.Equals("SampleScene", System.StringComparison.OrdinalIgnoreCase))
        {
            gameSceneName = "Map";
        }
    }

    private void Start()
    {
        // Gán sự kiện nút bấm
        if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(CloseSettings);
        if (logoutButton != null) logoutButton.onClick.AddListener(OnLogoutClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

        // Ban đầu đóng Settings Panel
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Tự động nạp thông tin người dùng từ Firebase khi mở Scene MainMenu
        UpdateUserProfileUI();
    }

    public void OpenMainMenu()
    {
        // Thêm dòng này để ẩn khung đăng nhập đi ngay khi mở Main Menu
        if (authCanvasGroup != null) authCanvasGroup.SetActive(false);
        SetLogoActive(false);

        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Nạp tên & email người dùng từ Firebase
        UpdateUserProfileUI();
    }

    public void SetLogoActive(bool active)
    {
        if (gameLogoObject != null)
        {
            gameLogoObject.SetActive(active);
        }

        // Quét toàn bộ Canvas để tìm LogoGame, Fire, logo_purple_glow ngay cả khi đang lồng trong AuthManager
        Canvas canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            Transform[] allTransforms = canvas.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                string tName = t.name.ToLower();
                if (tName.Equals("logogame") || tName.Equals("logo game") || tName.Equals("logo") || tName.Equals("logo_purple_glow") || tName.Equals("fire"))
                {
                    t.gameObject.SetActive(active);
                }
            }
        }
    }

    public void UpdateUserProfileUI()
    {
        if (FirebaseAuthManager.Instance != null)
        {
            string email = FirebaseAuthManager.Instance.GetCurrentUserEmail();
            string displayName = FirebaseAuthManager.Instance.GetCurrentUserDisplayName();

            if (userEmailText != null) userEmailText.text = email;
            if (userNameText != null) userNameText.text = displayName;
        }
    }

    private void OnPlayClicked()
    {
        if (string.IsNullOrEmpty(gameSceneName) || gameSceneName.Equals("SampleScene", System.StringComparison.OrdinalIgnoreCase))
        {
            gameSceneName = "Map";
        }

        Debug.Log($"[MainMenuManager] Đang tải Scene game: {gameSceneName}...");

        Game.UI.IntroStoryManager storyManager = Game.UI.IntroStoryManager.Instance ?? Object.FindFirstObjectByType<Game.UI.IntroStoryManager>(FindObjectsInactive.Include);
        if (storyManager != null)
        {
            storyManager.StartStorySequence();
        }
        else
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    private void OnLogoutClicked()
    {
        Debug.Log("[MainMenuManager] Thực hiện Đăng xuất...");
        
        if (FirebaseAuthManager.Instance != null)
        {
            FirebaseAuthManager.Instance.SignOut();
        }

        // Nếu có khai báo loginSceneName thì load lại Scene Login
        if (!string.IsNullOrEmpty(loginSceneName))
        {
            SceneManager.LoadScene(loginSceneName);
            return;
        }

        // Hoặc ẩn Main Menu Panel nếu chạy cùng 1 Scene
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        SetLogoActive(true);

        if (authCanvasGroup != null)
        {
            authCanvasGroup.SetActive(true);
        }

        if (AuthUIManager.Instance != null)
        {
            AuthUIManager.Instance.ShowLoginPanel();
        }
    }

    private void OnQuitClicked()
    {
        Debug.Log("[MainMenuManager] Thoát game...");
        Application.Quit();
        
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
