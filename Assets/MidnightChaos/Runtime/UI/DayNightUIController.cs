using System.Collections;
using UnityEngine;
using MidnightChaos.Resources;

namespace MidnightChaos.UI
{
    [DisallowMultipleComponent]
    public class DayNightUIController : MonoBehaviour
    {
        [Header("UI Controller References")]
        [SerializeField] private DayNightCycleController cycleController;

        [Header("Display Settings")]
        [SerializeField] private bool useEnglishText = true;
        [SerializeField] private float displayDurationSeconds = 3.0f;

        [Header("Font Style & Color (Thống nhất kiểu dáng & Màu sắc)")]
        [SerializeField] private int bannerFontSize = 52;
        [SerializeField] private Color bannerTextColor = new Color(1.0f, 0.92f, 0.45f, 1.0f); // Màu Vàng Nắng rực rỡ chuẩn game Muck

        private int currentDay = 1;
        private bool isDaytime = true;

        private string bannerMessage = "";
        private float bannerAlpha = 0f;
        private Coroutine bannerCoroutine;

        private void Awake()
        {
            if (cycleController == null)
            {
                cycleController = FindFirstObjectByType<DayNightCycleController>();
            }
        }

        private void Start()
        {
            if (cycleController != null)
            {
                currentDay = cycleController.CurrentDayNumber;
                isDaytime = cycleController.IsDaytime;
            }

            // Tự động hiển thị ngay chữ "DAY 1" to đùng giữa màn hình khi mới vào game (Chuẩn Muck)
            if (isDaytime)
            {
                string msg = useEnglishText ? $"DAY {currentDay}" : $"NGÀY {currentDay}";
                ShowBanner(msg);
            }
        }

        private void OnEnable()
        {
            if (cycleController != null)
            {
                cycleController.OnDayChanged += HandleDayChanged;
                cycleController.OnDayNightStateChanged += HandleDayNightStateChanged;
            }
        }

        private void OnDisable()
        {
            if (cycleController != null)
            {
                cycleController.OnDayChanged -= HandleDayChanged;
                cycleController.OnDayNightStateChanged -= HandleDayNightStateChanged;
            }
        }

        private void HandleDayChanged(int newDay)
        {
            currentDay = newDay;
        }

        private void HandleDayNightStateChanged(bool isDay)
        {
            isDaytime = isDay;

            if (isDay)
            {
                // Khi sáng: Hiện chữ "DAY 2", "DAY 3"... to giữa màn hình vài giây rồi biến mất
                string msg = useEnglishText 
                    ? $"DAY {currentDay}" 
                    : $"NGÀY {currentDay}";
                ShowBanner(msg);
            }
            else
            {
                // Khi tối: Hiện chữ "NIGHT IS COMING" to giữa màn hình vài giây rồi biến mất
                string msg = useEnglishText 
                    ? "NIGHT IS COMING" 
                    : "ĐÊM ĐÃ ĐẾN...";
                ShowBanner(msg);
            }
        }

        private void ShowBanner(string msg)
        {
            if (bannerCoroutine != null) StopCoroutine(bannerCoroutine);
            bannerCoroutine = StartCoroutine(BannerSequence(msg));
        }

        private IEnumerator BannerSequence(string msg)
        {
            bannerMessage = msg;
            bannerAlpha = 0f;

            // 1. Hiện dần (Fade In - 0.4s)
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                bannerAlpha = Mathf.Clamp01(t / 0.4f);
                yield return null;
            }

            // 2. Giữ hiển thị giữa màn hình vài giây (Hold - 2.5s)
            yield return new WaitForSeconds(displayDurationSeconds);

            // 3. Mờ dần rồi biến mất hoàn toàn (Fade Out - 0.8s)
            t = 0f;
            while (t < 0.8f)
            {
                t += Time.deltaTime;
                bannerAlpha = Mathf.Clamp01(1f - (t / 0.8f));
                yield return null;
            }

            bannerAlpha = 0f;
            bannerMessage = "";
        }

        private void OnGUI()
        {
            // Chỉ hiển thị khi có dòng chữ cảnh báo (Banner) và mờ dần rồi biến mất hoàn toàn
            if (bannerAlpha > 0f && !string.IsNullOrEmpty(bannerMessage))
            {
                // Phong cách chữ To, Đậm, Giữa màn hình chính xác như game Muck
                GUIStyle bannerStyle = new GUIStyle(GUI.skin.label);
                bannerStyle.fontSize = bannerFontSize; // Thống nhất phông chữ cỡ 52px cho cả Ngày & Đêm
                bannerStyle.fontStyle = FontStyle.Bold;
                bannerStyle.alignment = TextAnchor.MiddleCenter;

                // Thống nhất 1 màu duy nhất (Màu Vàng Nắng ấm rực rỡ) cho cả Ban ngày và Ban đêm
                Color finalColor = new Color(bannerTextColor.r, bannerTextColor.g, bannerTextColor.b, bannerAlpha);
                bannerStyle.normal.textColor = finalColor;

                float bannerW = 800f;
                float bannerH = 120f;
                float bannerX = (Screen.width - bannerW) * 0.5f;
                float bannerY = (Screen.height - bannerH) * 0.45f; // Chính giữa màn hình

                // Vẽ bóng chữ nhẹ đằng sau để chữ nổi bật rực rỡ trên mọi khung cảnh
                GUIStyle shadowStyle = new GUIStyle(bannerStyle);
                shadowStyle.normal.textColor = new Color(0f, 0f, 0f, bannerAlpha * 0.7f);
                GUI.Label(new Rect(bannerX + 3f, bannerY + 3f, bannerW, bannerH), bannerMessage, shadowStyle);

                // Vẽ dòng chữ chính to giữa màn hình
                GUI.Label(new Rect(bannerX, bannerY, bannerW, bannerH), bannerMessage, bannerStyle);
            }
        }
    }
}
