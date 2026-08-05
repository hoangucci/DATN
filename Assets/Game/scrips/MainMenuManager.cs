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
    [SerializeField] private GameObject settingsOverlayBackdrop; // Lớp phủ tối mờ khoá màn hình phía sau khi mở Settings
    [SerializeField] private GameObject authCanvasGroup; // Hoặc Panel chứa toàn bộ Auth UI
    [SerializeField] private GameObject gameLogoObject;   // Logo Game (tự động ẩn khi vào MainMenu)

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
    [SerializeField] private string gameSceneName = "SampleScene"; // Scene khi bấm Play
    [SerializeField] private string loginSceneName = "Login";       // Scene màn hình Đăng Nhập khi bấm Logout

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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
        // Ẩn khung đăng nhập & Ẩn luôn Logo Game khi vào Main Menu
        if (authCanvasGroup != null) authCanvasGroup.SetActive(false);
        if (gameLogoObject != null) gameLogoObject.SetActive(false);

        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Nạp tên & email người dùng từ Firebase
        UpdateUserProfileUI();
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

    public void OnPlayClicked()
    {
        // Nếu Inspector cũ còn lưu giá trị "SampleScene", tự động sửa thành "Map"
        if (string.IsNullOrEmpty(gameSceneName) || gameSceneName.Equals("SampleScene", System.StringComparison.OrdinalIgnoreCase))
        {
            gameSceneName = "Map";
        }

        Debug.Log($"[MainMenuManager] Đang tải Scene game: {gameSceneName}...");
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenSettings()
    {
        if (settingsOverlayBackdrop != null)
        {
            settingsOverlayBackdrop.SetActive(true);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            settingsPanel.transform.SetAsLastSibling(); // Đưa Settings Panel lên trên cùng
        }
    }

    public void CloseSettings()
    {
        if (settingsOverlayBackdrop != null)
        {
            settingsOverlayBackdrop.SetActive(false);
        }

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
        if (gameLogoObject != null) gameLogoObject.SetActive(true);

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
