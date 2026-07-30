using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMP_Text volumeValueText;

    [Header("Graphics Settings")]
    [SerializeField] private TMP_Dropdown graphicsDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    private const string PREF_VOLUME = "MasterVolume";
    private const string PREF_GRAPHICS = "GraphicsQuality";
    private const string PREF_FULLSCREEN = "IsFullscreen";

    private void Start()
    {
        // Nạp các thiết lập đã lưu
        LoadSettings();

        // Gán sự kiện khi thay đổi UI
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (graphicsDropdown != null)
        {
            graphicsDropdown.onValueChanged.AddListener(SetGraphicsQuality);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
    }

    public void LoadSettings()
    {
        // 1. Âm lượng (Default: 0.8 / 80%)
        float volume = PlayerPrefs.GetFloat(PREF_VOLUME, 0.8f);
        if (masterVolumeSlider != null) masterVolumeSlider.value = volume;
        SetMasterVolume(volume);

        // 2. Đồ họa (Default: High = index 2 hoặc tùy Unity Quality level)
        int graphicsIndex = PlayerPrefs.GetInt(PREF_GRAPHICS, QualitySettings.GetQualityLevel());
        if (graphicsDropdown != null) graphicsDropdown.value = graphicsIndex;
        SetGraphicsQuality(graphicsIndex);

        // 3. Màn hình (Default: Fullscreen = true)
        bool isFullscreen = PlayerPrefs.GetInt(PREF_FULLSCREEN, 1) == 1;
        if (fullscreenToggle != null) fullscreenToggle.isOn = isFullscreen;
        SetFullscreen(isFullscreen);
    }

    public void SetMasterVolume(float volume)
    {
        AudioListener.volume = volume;
        if (volumeValueText != null)
        {
            volumeValueText.text = $"{Mathf.RoundToInt(volume * 100)}%";
        }
        PlayerPrefs.SetFloat(PREF_VOLUME, volume);
        PlayerPrefs.Save();
    }

    public void SetGraphicsQuality(int index)
    {
        QualitySettings.SetQualityLevel(index, true);
        PlayerPrefs.SetInt(PREF_GRAPHICS, index);
        PlayerPrefs.Save();
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(PREF_FULLSCREEN, isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }
}
