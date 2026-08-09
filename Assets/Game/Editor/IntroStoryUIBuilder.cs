using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class IntroStoryUIBuilder
    {
        [MenuItem("Game Utility/Build Intro Story Panel (Dẫn Dắt Cốt Truyện)")]
        public static void BuildIntroStoryUI()
        {
            // Tự động tìm hoặc tạo Canvas nếu Scene hiện tại (như Map.unity) chưa có Canvas UI
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            // Xử lý EventSystem tương thích 100% với New Input System
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject esObj = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = esObj.GetComponent<EventSystem>();
            }

            ConfigureEventSystemForNewInputSystem(eventSystem.gameObject);

            string currentSceneName = EditorSceneManager.GetActiveScene().name;
            bool isMapScene = currentSceneName.Equals("Map", System.StringComparison.OrdinalIgnoreCase);

            // 1. Tạo GameObject chứa Manager & Component
            GameObject storyManagerObj = GameObject.Find("[IntroStoryManager]");
            if (storyManagerObj == null)
            {
                storyManagerObj = new GameObject("[IntroStoryManager]");
            }

            IntroStoryManager storyManager = storyManagerObj.GetComponent<IntroStoryManager>() ?? storyManagerObj.AddComponent<IntroStoryManager>();

            // 2. Dựng Khung Giao Diện Intro Cốt Truyện (Full-screen Dark Cutscene Panel)
            Transform oldPanel = canvas.transform.Find("[IntroStoryPanel]");
            if (oldPanel != null) Object.DestroyImmediate(oldPanel.gameObject);

            GameObject panelObj = new GameObject("[IntroStoryPanel]", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panelObj.transform.SetParent(canvas.transform, false);

            RectTransform rect = panelObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image bgImg = panelObj.GetComponent<Image>();
            bgImg.color = new Color(0.04f, 0.04f, 0.06f, 0.98f); // Đen điện ảnh sâu thẫm

            CanvasGroup canvasGroup = panelObj.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;

            // 3. Tiêu đề Khung Cốt Truyện
            GameObject titleObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(panelObj.transform, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -60f);
            titleRect.sizeDelta = new Vector2(800f, 60f);

            TMP_Text titleTxt = titleObj.GetComponent<TextMeshProUGUI>();
            titleTxt.text = "MIDNIGHT CHAOS";
            titleTxt.fontSize = 32;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = new Color(0.75f, 0.35f, 1.0f); // Tím phát sáng
            titleTxt.alignment = TextAlignmentOptions.Center;

            // 4. Ô Hiển thị Nội Dung Cốt Truyện (Typewriter Story Text Box)
            GameObject contentObj = new GameObject("StoryText", typeof(RectTransform), typeof(TextMeshProUGUI));
            contentObj.transform.SetParent(panelObj.transform, false);
            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = new Vector2(0f, 20f);
            contentRect.sizeDelta = new Vector2(850f, 320f);

            TMP_Text contentTxt = contentObj.GetComponent<TextMeshProUGUI>();
            contentTxt.text = "Chuyến bay gặp sự cố bất thường do từ trường mạnh...\n\nMáy bay mất kiểm soát và rơi tự do xuống một hòn đảo bí ẩn bị bao phủ bởi Năng Lượng Hỗn Mang.";
            contentTxt.fontSize = 22;
            contentTxt.lineSpacing = 15;
            contentTxt.color = new Color(0.95f, 0.92f, 0.85f);
            contentTxt.alignment = TextAlignmentOptions.Center;

            // 5. Dòng Chữ Nhắc Nhở Tiếp Tục (Continue Hint Text)
            GameObject hintObj = new GameObject("ContinueHintText", typeof(RectTransform), typeof(TextMeshProUGUI));
            hintObj.transform.SetParent(panelObj.transform, false);
            RectTransform hintRect = hintObj.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0f);
            hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 50f);
            hintRect.sizeDelta = new Vector2(600f, 40f);

            TMP_Text hintTxt = hintObj.GetComponent<TextMeshProUGUI>();
            hintTxt.text = "Bấm chuột hoặc Space để tiếp tục >>";
            hintTxt.fontSize = 16;
            hintTxt.color = new Color(0.7f, 0.65f, 0.55f);
            hintTxt.alignment = TextAlignmentOptions.Center;

            // 6. Nút Bỏ Qua (Skip Button) ở góc trên bên phải
            GameObject skipBtnObj = new GameObject("SkipButton", typeof(RectTransform), typeof(Image), typeof(Button));
            skipBtnObj.transform.SetParent(panelObj.transform, false);
            RectTransform skipRect = skipBtnObj.GetComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(1f, 1f);
            skipRect.anchorMax = new Vector2(1f, 1f);
            skipRect.pivot = new Vector2(1f, 1f);
            skipRect.anchoredPosition = new Vector2(-40f, -40f);
            skipRect.sizeDelta = new Vector2(130f, 40f);

            Image skipImg = skipBtnObj.GetComponent<Image>();
            skipImg.color = new Color(0.2f, 0.15f, 0.12f, 0.9f);

            Outline skipOutline = skipBtnObj.AddComponent<Outline>();
            skipOutline.effectColor = new Color(0.5f, 0.35f, 0.2f, 0.8f);
            skipOutline.effectDistance = new Vector2(2f, -2f);

            GameObject skipTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            skipTxtObj.transform.SetParent(skipBtnObj.transform, false);
            RectTransform skipTxtRect = skipTxtObj.GetComponent<RectTransform>();
            skipTxtRect.anchorMin = Vector2.zero;
            skipTxtRect.anchorMax = Vector2.one;
            skipTxtRect.sizeDelta = Vector2.zero;

            TMP_Text skipTxt = skipTxtObj.GetComponent<TextMeshProUGUI>();
            skipTxt.text = "BỎ QUA >>";
            skipTxt.fontSize = 14;
            skipTxt.fontStyle = FontStyles.Bold;
            skipTxt.color = new Color(0.9f, 0.8f, 0.65f);
            skipTxt.alignment = TextAlignmentOptions.Center;

            // Thêm Button toàn màn hình cho panelObj để click chuyển câu chuyện
            Button panelBtn = panelObj.GetComponent<Button>() ?? panelObj.AddComponent<Button>();
            panelBtn.transition = Selectable.Transition.None;

            // 7. Liên kết Serialized Properties cho IntroStoryManager
            SerializedObject managerSO = new SerializedObject(storyManager);
            managerSO.FindProperty("storyPanel").objectReferenceValue = panelObj;
            managerSO.FindProperty("storyCanvasGroup").objectReferenceValue = canvasGroup;
            managerSO.FindProperty("storyText").objectReferenceValue = contentTxt;
            managerSO.FindProperty("continueHintText").objectReferenceValue = hintTxt;
            managerSO.FindProperty("skipButton").objectReferenceValue = skipBtnObj.GetComponent<Button>();
            managerSO.FindProperty("panelClickButton").objectReferenceValue = panelBtn;
            managerSO.FindProperty("backgroundBlackOverlay").objectReferenceValue = bgImg;
            managerSO.FindProperty("targetSceneOnComplete").stringValue = "ProceduralCombatDemo";

            // Nếu đang dựng ở Scene Map -> Tự động phát cốt truyện khi vừa mở Đảo (triggerOnStart = true)
            // Nếu ở Scene Login -> Chờ bấm Start mới phát (triggerOnStart = false)
            managerSO.FindProperty("triggerOnStart").boolValue = isMapScene;

            managerSO.ApplyModifiedProperties();

            storyManagerObj.SetActive(true);
            panelObj.SetActive(isMapScene); // Trong Map.unity thì hiện ngay lúc đầu, trong Login thì ẩn chờ Start

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorUtility.DisplayDialog("Hoàn tất!",
                $"Đã cài đặt thành công Bảng Giới Thiệu Cốt Truyện cho Scene '{currentSceneName}':\n\n" +
                "• Đã sửa lỗi EventSystem New Input System 100%.\n" +
                "• Thay thế ký tự mũi tên chuẩn không gây warning phông chữ.", "OK");
        }

        private static void ConfigureEventSystemForNewInputSystem(GameObject esObj)
        {
            // Xoá StandaloneInputModule cũ nếu có
            StandaloneInputModule oldModule = esObj.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
            {
                Object.DestroyImmediate(oldModule);
            }

            // Gán InputSystemUIInputModule từ New Input System Package
            System.Type inputSystemModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModuleType != null)
            {
                if (esObj.GetComponent(inputSystemModuleType) == null)
                {
                    esObj.AddComponent(inputSystemModuleType);
                }
            }
            else
            {
                // Fallback nếu không dùng New Input System package
                if (esObj.GetComponent<StandaloneInputModule>() == null)
                {
                    esObj.AddComponent<StandaloneInputModule>();
                }
            }
        }
    }
}
