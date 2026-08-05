using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AuthUIManager : MonoBehaviour
{
    public static AuthUIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }
    [Header("Panels")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject registerPanel;
    [SerializeField] private GameObject forgotPasswordPanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject gameLogoObject; // Object Logo Game (sẽ ẩn đi khi đăng nhập)

    [Header("Scene Transition")]
    [SerializeField] private bool loadSceneOnLogin = true;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Login Form Fields")]
    [SerializeField] private TMP_InputField loginEmailInput;
    [SerializeField] private TMP_InputField loginPasswordInput;
    [SerializeField] private Toggle rememberMeToggle;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button goToRegisterButton;
    [SerializeField] private Button goToForgotPasswordButton;

    [Header("Register Form Fields")]
    [SerializeField] private TMP_InputField registerEmailInput;
    [SerializeField] private TMP_InputField registerPasswordInput;
    [SerializeField] private TMP_InputField registerConfirmPasswordInput;
    [SerializeField] private Button registerButton;
    [SerializeField] private Button registerBackToLoginButton;

    [Header("Forgot Password Form Fields")]
    [SerializeField] private TMP_InputField forgotEmailInput;
    [SerializeField] private Button sendResetButton;
    [SerializeField] private Button forgotBackToLoginButton;

    [Header("Status Feedback UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject loadingSpinner; // Icon xoay hoặc hiệu ứng loading (tùy chọn)

    private const string PREF_EMAIL = "RememberedEmail";
    private const string PREF_PASSWORD = "RememberedPassword";
    private const string PREF_REMEMBER = "RememberMeState";

    private void Start()
    {
        // Gán listener cho các button
        if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
        if (registerButton != null) registerButton.onClick.AddListener(OnRegisterClicked);
        if (sendResetButton != null) sendResetButton.onClick.AddListener(OnForgotPasswordClicked);

        if (goToRegisterButton != null) goToRegisterButton.onClick.AddListener(ShowRegisterPanel);
        if (goToForgotPasswordButton != null) goToForgotPasswordButton.onClick.AddListener(ShowForgotPasswordPanel);
        if (registerBackToLoginButton != null) registerBackToLoginButton.onClick.AddListener(ShowLoginPanel);
        if (forgotBackToLoginButton != null) forgotBackToLoginButton.onClick.AddListener(ShowLoginPanel);

        // Nạp thông tin "Nhớ đăng nhập" nếu có
        LoadRememberedCredentials();

        // Ban đầu hiển thị bảng Đăng nhập
        ShowLoginPanel();
    }

    #region Panel Switcher
    public void ShowLoginPanel()
    {
        SetPanelActive(loginPanel, true);
        SetPanelActive(registerPanel, false);
        SetPanelActive(forgotPasswordPanel, false);
        if (gameLogoObject != null) gameLogoObject.SetActive(true);
        ClearStatus();
    }

    public void ShowRegisterPanel()
    {
        SetPanelActive(loginPanel, false);
        SetPanelActive(registerPanel, true);
        SetPanelActive(forgotPasswordPanel, false);
        ClearStatus();
    }

    public void ShowForgotPasswordPanel()
    {
        SetPanelActive(loginPanel, false);
        SetPanelActive(registerPanel, false);
        SetPanelActive(forgotPasswordPanel, true);
        ClearStatus();
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }
    #endregion

    #region Form Handlers
    private async void OnLoginClicked()
    {
        string email = loginEmailInput != null ? loginEmailInput.text.Trim() : "";
        string password = loginPasswordInput != null ? loginPasswordInput.text : "";

        SetLoading(true, "Đang đăng nhập...");

        var (success, message) = await FirebaseAuthManager.Instance.LoginAsync(email, password);

        SetLoading(false);

        if (success)
        {
            ShowStatus(message, Color.green);

            // Xử lý "Nhớ đăng nhập"
            if (rememberMeToggle != null && rememberMeToggle.isOn)
            {
                PlayerPrefs.SetString(PREF_EMAIL, email);
                PlayerPrefs.SetString(PREF_PASSWORD, password);
                PlayerPrefs.SetInt(PREF_REMEMBER, 1);
            }
            else
            {
                PlayerPrefs.DeleteKey(PREF_EMAIL);
                PlayerPrefs.DeleteKey(PREF_PASSWORD);
                PlayerPrefs.SetInt(PREF_REMEMBER, 0);
            }
            PlayerPrefs.Save();

            // TO DO: Chuyển Scene game chính hoặc kích hoạt Main Menu Panel
            Debug.Log("[AuthUIManager] Đăng nhập thành công! Chuyển tới Main Menu...");

            if (loadSceneOnLogin && !string.IsNullOrEmpty(mainMenuSceneName))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
            }
            else
            {
                // Ẩn tất cả các panel đăng nhập & Ẩn luôn Logo Game
                SetPanelActive(loginPanel, false);
                SetPanelActive(registerPanel, false);
                SetPanelActive(forgotPasswordPanel, false);

                if (gameLogoObject != null)
                {
                    gameLogoObject.SetActive(false);
                }

                // Hiển thị Main Menu Panel
                if (mainMenuPanel != null)
                {
                    mainMenuPanel.SetActive(true);
                }

                // Cập nhật thông tin trên MainMenuManager nếu có
                if (MainMenuManager.Instance != null)
                {
                    MainMenuManager.Instance.OpenMainMenu();
                }
            }
        }
        else
        {
            ShowStatus(message, Color.red);
        }
    }

    private async void OnRegisterClicked()
    {
        string email = registerEmailInput != null ? registerEmailInput.text.Trim() : "";
        string password = registerPasswordInput != null ? registerPasswordInput.text : "";
        string confirmPassword = registerConfirmPasswordInput != null ? registerConfirmPasswordInput.text : "";

        if (password != confirmPassword)
        {
            ShowStatus("Mật khẩu nhập lại không khớp!", Color.red);
            return;
        }

        SetLoading(true, "Đang đăng ký tài khoản...");

        var (success, message) = await FirebaseAuthManager.Instance.RegisterAsync(email, password);

        SetLoading(false);

        if (success)
        {
            ShowStatus(message, Color.green);
            // Tự động điền email sang trang Đăng nhập và mở bảng Đăng nhập
            if (loginEmailInput != null) loginEmailInput.text = email;
            if (loginPasswordInput != null) loginPasswordInput.text = password;
            
            StartCoroutine(DelaySwitchToLogin(2f));
        }
        else
        {
            ShowStatus(message, Color.red);
        }
    }

    private async void OnForgotPasswordClicked()
    {
        string email = forgotEmailInput != null ? forgotEmailInput.text.Trim() : "";

        SetLoading(true, "Đang gửi yêu cầu khôi phục mật khẩu...");

        var (success, message) = await FirebaseAuthManager.Instance.ResetPasswordAsync(email);

        SetLoading(false);

        if (success)
        {
            ShowStatus(message, Color.green);
            StartCoroutine(DelaySwitchToLogin(3f));
        }
        else
        {
            ShowStatus(message, Color.red);
        }
    }
    #endregion

    #region Helpers & Remember Me
    private void LoadRememberedCredentials()
    {
        bool remember = PlayerPrefs.GetInt(PREF_REMEMBER, 0) == 1;
        if (rememberMeToggle != null) rememberMeToggle.isOn = remember;

        if (remember)
        {
            if (loginEmailInput != null) loginEmailInput.text = PlayerPrefs.GetString(PREF_EMAIL, "");
            if (loginPasswordInput != null) loginPasswordInput.text = PlayerPrefs.GetString(PREF_PASSWORD, "");
        }
    }

    private void SetLoading(bool isLoading, string statusMsg = "")
    {
        if (loadingSpinner != null) loadingSpinner.SetActive(isLoading);
        if (loginButton != null) loginButton.interactable = !isLoading;
        if (registerButton != null) registerButton.interactable = !isLoading;
        if (sendResetButton != null) sendResetButton.interactable = !isLoading;

        if (isLoading)
        {
            ShowStatus(statusMsg, Color.yellow);
        }
    }

    private void ShowStatus(string msg, Color color)
    {
        if (statusText != null)
        {
            statusText.text = msg;
            statusText.color = color;
            statusText.gameObject.SetActive(true);
        }
    }

    private void ClearStatus()
    {
        if (statusText != null)
        {
            statusText.text = "";
            statusText.gameObject.SetActive(false);
        }
    }

    private IEnumerator DelaySwitchToLogin(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowLoginPanel();
    }
    #endregion
}
