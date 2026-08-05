using System;
using System.Threading.Tasks;
using UnityEngine;

// Đã mở khóa thư viện Firebase
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;

public class FirebaseAuthManager : MonoBehaviour
{
    public static FirebaseAuthManager Instance { get; private set; }

    [Header("Firebase Config")]
    [SerializeField] private bool useMockModeForTesting = false; // Bật true để test UI khi chưa import Firebase SDK

    // Firebase Auth variables đã được mở khóa
    private FirebaseAuth auth;
    private FirebaseUser user;

    private bool isInitialized = false;
    public bool IsInitialized => isInitialized;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Chỉ áp dụng DontDestroyOnLoad nếu là GameObject gốc (Root) độc lập và không chứa Canvas/UI
            if (transform.parent == null && GetComponent<Canvas>() == null && GetComponent<RectTransform>() == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else
        {
            // Chỉ xoá bớt Component trùng lặp, không xoá cả GameObject để tránh mất Canvas/UI
            Destroy(this);
        }
    }

    private void Start()
    {
        InitializeFirebase();
    }

    private void InitializeFirebase()
    {
        if (useMockModeForTesting)
        {
            Debug.Log("[FirebaseAuthManager] Đang dùng Mock Mode (Chế độ giả lập để test UI)");
            isInitialized = true;
            return;
        }

        // Đã mở khóa phần khởi tạo Firebase
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                auth.StateChanged += AuthStateChanged;
                AuthStateChanged(this, null);
                isInitialized = true;
                Debug.Log("[FirebaseAuthManager] Khởi tạo Firebase Authentication thành công!");
            }
            else
            {
                Debug.LogError($"[FirebaseAuthManager] Không thể khởi tạo Firebase dependencies: {dependencyStatus}");
            }
        });

        isInitialized = true;
    }

    // Đã mở khóa hàm theo dõi trạng thái đăng nhập
    private void AuthStateChanged(object sender, EventArgs eventArgs)
    {
        if (auth.CurrentUser != user)
        {
            bool signedIn = user != auth.CurrentUser && auth.CurrentUser != null;
            if (!signedIn && user != null)
            {
                Debug.Log("[FirebaseAuthManager] Người dùng đã đăng xuất: " + user.UserId);
            }
            user = auth.CurrentUser;
            if (signedIn)
            {
                Debug.Log("[FirebaseAuthManager] Người dùng đã đăng nhập: " + user.DisplayName + " (" + user.Email + ")");
            }
        }
    }

    /// <summary>
    /// Đăng ký tài khoản mới với Email và Password
    /// </summary>
    public async Task<(bool success, string message)> RegisterAsync(string email, string password)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            return (false, "Vui lòng nhập đầy đủ Email và Mật khẩu!");
        }

        if (password.Length < 6)
        {
            return (false, "Mật khẩu phải có ít nhất 6 ký tự!");
        }

        if (useMockModeForTesting)
        {
            await Task.Delay(1000);
            return (true, "Đăng ký thành công (Mock Mode)!");
        }

        // Đã mở khóa chức năng Đăng Ký thật
        try
        {
            var authResult = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
            FirebaseUser newUser = authResult.User;
            return (true, $"Đăng ký thành công tài khoản: {newUser.Email}");
        }
        catch (Exception ex)
        {
            return (false, GetFirebaseErrorMessage(ex));
        }
    }

    /// <summary>
    /// Đăng nhập tài khoản với Email và Password
    /// </summary>
    public async Task<(bool success, string message)> LoginAsync(string email, string password)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            return (false, "Vui lòng nhập đầy đủ Email và Mật khẩu!");
        }

        if (useMockModeForTesting)
        {
            await Task.Delay(1000);
            return (true, "Đăng nhập thành công (Mock Mode)!");
        }

        // Đã mở khóa chức năng Đăng Nhập thật
        try
        {
            var authResult = await auth.SignInWithEmailAndPasswordAsync(email, password);
            FirebaseUser signedInUser = authResult.User;
            return (true, $"Chào mừng trở lại, {signedInUser.Email}!");
        }
        catch (Exception ex)
        {
            return (false, GetFirebaseErrorMessage(ex));
        }
    }

    /// <summary>
    /// Gửi email khôi phục mật khẩu
    /// </summary>
    public async Task<(bool success, string message)> ResetPasswordAsync(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return (false, "Vui lòng nhập Email để khôi phục mật khẩu!");
        }

        if (useMockModeForTesting)
        {
            await Task.Delay(1000);
            return (true, $"Đã gửi hướng dẫn khôi phục mật khẩu tới {email} (Mock Mode)!");
        }

        // Đã mở khóa chức năng Quên Mật Khẩu thật
        try
        {
            await auth.SendPasswordResetEmailAsync(email);
            return (true, $"Link khôi phục mật khẩu đã được gửi đến {email}. Hãy kiểm tra hộp thư!");
        }
        catch (Exception ex)
        {
            return (false, GetFirebaseErrorMessage(ex));
        }
    }

    /// <summary>
    /// Đăng xuất người dùng hiện tại
    /// </summary>
    public void SignOut()
    {
        // Đã mở khóa chức năng Đăng Xuất thật
        if (auth != null)
        {
            auth.SignOut();
        }

        PlayerPrefs.DeleteKey("RememberedEmail");
        PlayerPrefs.DeleteKey("RememberedPassword");
        PlayerPrefs.Save();
        Debug.Log("[FirebaseAuthManager] Đã đăng xuất!");
    }

    /// <summary>
    /// Lấy Email của người dùng đang đăng nhập
    /// </summary>
    public string GetCurrentUserEmail()
    {
        if (useMockModeForTesting)
        {
            return PlayerPrefs.GetString("RememberedEmail", "player@midnightchaos.com");
        }

        if (auth != null && auth.CurrentUser != null)
        {
            return auth.CurrentUser.Email;
        }

        return "Người chơi";
    }

    /// <summary>
    /// Lấy Tên hiển thị (DisplayName) của người dùng
    /// </summary>
    public string GetCurrentUserDisplayName()
    {
        if (auth != null && auth.CurrentUser != null && !string.IsNullOrEmpty(auth.CurrentUser.DisplayName))
        {
            return auth.CurrentUser.DisplayName;
        }

        string email = GetCurrentUserEmail();
        if (!string.IsNullOrEmpty(email) && email.Contains("@"))
        {
            return email.Split('@')[0];
        }

        return "Player";
    }

    /// <summary>
    /// Kiểm tra người dùng đã đăng nhập chưa
    /// </summary>
    public bool IsSignedIn()
    {
        if (useMockModeForTesting) return true;
        return auth != null && auth.CurrentUser != null;
    }

    // Đã mở khóa bộ dịch lỗi Firebase
    private string GetFirebaseErrorMessage(Exception exception)
    {
        FirebaseException firebaseEx = exception.GetBaseException() as FirebaseException;
        if (firebaseEx != null)
        {
            AuthError errorCode = (AuthError)firebaseEx.ErrorCode;
            switch (errorCode)
            {
                case AuthError.InvalidEmail:
                    return "Định dạng Email không hợp lệ!";
                case AuthError.WrongPassword:
                    return "Mật khẩu không chính xác!";
                case AuthError.UserNotFound:
                    return "Tài khoản không tồn tại!";
                case AuthError.EmailAlreadyInUse:
                    return "Email này đã được sử dụng!";
                case AuthError.WeakPassword:
                    return "Mật khẩu quá yếu! Phải có từ 6 ký tự trở lên.";
                case AuthError.MissingEmail:
                    return "Vui lòng nhập địa chỉ Email!";
                default:
                    return $"Lỗi đăng nhập: {firebaseEx.Message}";
            }
        }
        return exception.Message;
    }
}