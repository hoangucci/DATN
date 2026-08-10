using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MidnightChaos.UI
{
    public class InGameSettingsController : MonoBehaviour
    {
        [Header("UI Panel")]
        [SerializeField] private GameObject settingsOverlay;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;

        [Header("Audio Controls")]
        [SerializeField] private Slider masterVolumeSlider;

        private bool isSettingsOpen = false;

        private void Start()
        {
            if (settingsOverlay != null)
            {
                settingsOverlay.SetActive(false);
            }

            SetupButtonListeners();
            SetupAudioListeners();
        }

        private void SetupButtonListeners()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(CloseSettings);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseSettings);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveAllListeners();
                mainMenuButton.onClick.AddListener(ReturnToMainMenu);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(QuitGame);
            }
        }

        private void SetupAudioListeners()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = AudioListener.volume;
                masterVolumeSlider.onValueChanged.AddListener((val) =>
                {
                    AudioListener.volume = val;
                });
            }
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame ||
                    UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame)
                {
                    ToggleSettings();
                }
            }
        }

        public void ToggleSettings()
        {
            if (isSettingsOpen)
            {
                CloseSettings();
            }
            else
            {
                OpenSettings();
            }
        }

        public void OpenSettings()
        {
            isSettingsOpen = true;
            if (settingsOverlay != null)
            {
                settingsOverlay.SetActive(true);
            }

            // Unlock mouse cursor for UI interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseSettings()
        {
            isSettingsOpen = false;
            if (settingsOverlay != null)
            {
                settingsOverlay.SetActive(false);
            }

            // Relock mouse cursor for TPS/FPS gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void ReturnToMainMenu()
        {
            Debug.Log("[InGameSettingsController] Quay trở về Menu chính...");

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SceneManager.LoadScene("Login");
        }

        public void QuitGame()
        {
            Debug.Log("[InGameSettingsController] Thoát ứng dụng Game...");
            Application.Quit();
        }
    }
}
