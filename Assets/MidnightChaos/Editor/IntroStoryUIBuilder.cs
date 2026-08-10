using MidnightChaos.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MidnightChaos.Editor
{
    public static class IntroStoryUIBuilder
    {
        [MenuItem("Game Utility/Build Intro Story Panel (Dẫn Dắt Cốt Truyện)")]
        public static void BuildIntroStoryUI()
        {
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

            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject esObj = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = esObj.GetComponent<EventSystem>();
            }

            ConfigureEventSystemForNewInputSystem(eventSystem.gameObject);

            string currentSceneName = EditorSceneManager.GetActiveScene().name;

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
            bgImg.color = new Color(0.04f, 0.04f, 0.06f, 0.98f);

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
            titleTxt.color = new Color(0.75f, 0.35f, 1.0f);
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
            GameObject skipObj = new GameObject("SkipButton", typeof(RectTransform), typeof(Image), typeof(Button));
            skipObj.transform.SetParent(panelObj.transform, false);
            RectTransform skipRect = skipObj.GetComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(1f, 1f);
            skipRect.anchorMax = new Vector2(1f, 1f);
            skipRect.pivot = new Vector2(1f, 1f);
            skipRect.anchoredPosition = new Vector2(-40f, -40f);
            skipRect.sizeDelta = new Vector2(140f, 45f);

            Image skipImg = skipObj.GetComponent<Image>();
            skipImg.color = new Color(0.2f, 0.2f, 0.25f, 0.8f);

            GameObject skipTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            skipTxtObj.transform.SetParent(skipObj.transform, false);
            RectTransform skipTxtRect = skipTxtObj.GetComponent<RectTransform>();
            skipTxtRect.anchorMin = Vector2.zero;
            skipTxtRect.anchorMax = Vector2.one;
            skipTxtRect.sizeDelta = Vector2.zero;

            TMP_Text skipTxt = skipTxtObj.GetComponent<TextMeshProUGUI>();
            skipTxt.text = "BỎ QUA >>";
            skipTxt.fontSize = 14;
            skipTxt.fontStyle = FontStyles.Bold;
            skipTxt.alignment = TextAlignmentOptions.Center;
            skipTxt.color = new Color(0.9f, 0.9f, 0.9f);

            // Nút bấm phủ toàn màn hình để click chuột đọc câu tiếp theo
            GameObject clickObj = new GameObject("PanelClickButton", typeof(RectTransform), typeof(Button));
            clickObj.transform.SetParent(panelObj.transform, false);
            clickObj.transform.SetAsFirstSibling();
            RectTransform clickRect = clickObj.GetComponent<RectTransform>();
            clickRect.anchorMin = Vector2.zero;
            clickRect.anchorMax = Vector2.one;
            clickRect.sizeDelta = Vector2.zero;

            // 7. Gán Reference vào Manager bằng SerializedObject
            SerializedObject so = new SerializedObject(storyManager);
            so.FindProperty("storyPanel").objectReferenceValue = panelObj;
            so.FindProperty("storyCanvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("storyText").objectReferenceValue = contentTxt;
            so.FindProperty("continueHintText").objectReferenceValue = hintTxt;
            so.FindProperty("skipButton").objectReferenceValue = skipObj.GetComponent<Button>();
            so.FindProperty("panelClickButton").objectReferenceValue = clickObj.GetComponent<Button>();
            so.FindProperty("backgroundBlackOverlay").objectReferenceValue = bgImg;

            so.ApplyModifiedProperties();

            panelObj.SetActive(false);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        }

        private static void ConfigureEventSystemForNewInputSystem(GameObject eventSystemObj)
        {
            Component legacyInputModule = eventSystemObj.GetComponent("StandaloneInputModule");
            if (legacyInputModule != null)
            {
                Object.DestroyImmediate(legacyInputModule);
            }

            Component newInputModule = eventSystemObj.GetComponent("InputSystemUIInputModule");
            if (newInputModule == null)
            {
                System.Type inputSystemModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputSystemModuleType != null)
                {
                    eventSystemObj.AddComponent(inputSystemModuleType);
                }
            }
        }
    }
}
