using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class MuckStyleUIBuilder
    {
        [MenuItem("Game Utility/Build Muck-Style UI (Streamlined Auto-Room & Copy ID)")]
        public static void BuildMuckUI()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy Canvas trong Scene hiện tại!", "OK");
                return;
            }

            // 1. Tự động tìm & gán Logo Game để ẩn đi khi Đăng Nhập thành công
            GameObject logoObj = GameObject.Find("LogoGame") ?? GameObject.Find("logo game") ?? GameObject.Find("Logo Game") ?? GameObject.Find("Logo");

            AuthUIManager authManager = Object.FindFirstObjectByType<AuthUIManager>();
            if (authManager != null && logoObj != null)
            {
                SerializedObject authSO = new SerializedObject(authManager);
                SerializedProperty logoProp = authSO.FindProperty("gameLogoObject");
                if (logoProp != null)
                {
                    logoProp.objectReferenceValue = logoObj;
                    authSO.ApplyModifiedProperties();
                }
            }

            MainMenuManager mainManager = Object.FindFirstObjectByType<MainMenuManager>();
            if (mainManager != null && logoObj != null)
            {
                SerializedObject mainSO = new SerializedObject(mainManager);
                SerializedProperty logoProp = mainSO.FindProperty("gameLogoObject");
                if (logoProp != null)
                {
                    logoProp.objectReferenceValue = logoObj;
                    mainSO.ApplyModifiedProperties();
                }
            }

            // 2. Tạo Lớp Phủ Tối Mờ Toàn Màn Hình (Settings Modal Overlay)
            GameObject overlayObj = GameObject.Find("[SettingsModalOverlay]");
            if (overlayObj == null)
            {
                overlayObj = new GameObject("[SettingsModalOverlay]", typeof(RectTransform), typeof(Image), typeof(Button));
                overlayObj.transform.SetParent(canvas.transform, false);
            }

            RectTransform overlayRect = overlayObj.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image overlayImg = overlayObj.GetComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.75f);
            overlayImg.raycastTarget = true;
            overlayObj.SetActive(false);

            if (mainManager != null)
            {
                SerializedObject mainSO = new SerializedObject(mainManager);
                SerializedProperty overlayProp = mainSO.FindProperty("settingsOverlayBackdrop");
                if (overlayProp != null)
                {
                    overlayProp.objectReferenceValue = overlayObj;
                    mainSO.ApplyModifiedProperties();
                }
            }

            Button overlayBtn = overlayObj.GetComponent<Button>();
            overlayBtn.onClick.RemoveAllListeners();
            overlayBtn.onClick.AddListener(() =>
            {
                if (mainManager != null) mainManager.CloseSettings();
            });

            // 3. Dựng / Cập nhật Khung Top-Left: Lobby Members Panel (Ở trong phòng sẵn)
            GameObject membersPanel = CreateOrResetPanel(canvas.transform, "[Muck_LobbyMembersPanel]",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(240f, 320f));

            CreateMuckButton(membersPanel.transform, "BackButton", "Back",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(200f, 40f));

            GameObject listContainer = CreatePlayerListArea(membersPanel.transform);
            CreateDummyMemberItem(listContainer.transform, "Player [Host]");

            CreateMuckButton(membersPanel.transform, "StartButton", "Start",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 25f), new Vector2(200f, 40f));

            // 4. Dựng / Cập nhật Khung Bottom-Right: Lobby Code & Copy Panel (Chỉ hiển thị ID và Copy)
            GameObject joinPanel = CreateOrResetPanel(canvas.transform, "[Muck_LobbyJoinPanel]",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(300f, 140f));

            CreateLabel(joinPanel.transform, "TitleLabel", "LOBBY ID: (send to friend)", 15, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(270f, 25f));

            GameObject inputFieldObj = CreateInputField(joinPanel.transform, "IpInputField", "192.168.1.100",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(260f, 36f));

            GameObject copyBtnObj = CreateMuckButton(joinPanel.transform, "CopyIpButton", "Copy to clipboard",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(260f, 36f));

            // Status Text
            CreateLabel(joinPanel.transform, "StatusText", "", 12, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(260f, 20f));

            // 5. Cấu hình Settings Panel & Nút Đăng Xuất / Thoát Game
            GameObject settingsPanel = GameObject.Find("SettingsPanel") ?? GameObject.Find("Settings") ?? GameObject.Find("settingsPanel");
            if (settingsPanel != null)
            {
                Canvas settingsCanvas = settingsPanel.GetComponent<Canvas>() ?? settingsPanel.AddComponent<Canvas>();
                settingsCanvas.overrideSorting = true;
                settingsCanvas.sortingOrder = 100;

                GraphicRaycaster raycaster = settingsPanel.GetComponent<GraphicRaycaster>() ?? settingsPanel.AddComponent<GraphicRaycaster>();
                raycaster.enabled = true;

                CanvasGroup group = settingsPanel.GetComponent<CanvasGroup>() ?? settingsPanel.AddComponent<CanvasGroup>();
                group.alpha = 1.0f;
                group.interactable = true;
                group.blocksRaycasts = true;

                // Thêm Nút Đăng Xuất (Logout)
                Transform oldLogout = settingsPanel.transform.Find("LogoutButton");
                if (oldLogout != null) Object.DestroyImmediate(oldLogout.gameObject);

                GameObject logoutBtnObj = CreateMuckButton(settingsPanel.transform, "LogoutButton", "ĐĂNG XUẤT",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-115f, 25f), new Vector2(170f, 42f));

                Button logoutBtn = logoutBtnObj.GetComponent<Button>();
                logoutBtn.onClick.RemoveAllListeners();
                logoutBtn.onClick.AddListener(() =>
                {
                    if (FirebaseAuthManager.Instance != null) FirebaseAuthManager.Instance.SignOut();
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Login");
                });

                // Thêm Nút Thoát Game (Quit)
                Transform oldQuit = settingsPanel.transform.Find("QuitButton");
                if (oldQuit != null) Object.DestroyImmediate(oldQuit.gameObject);

                GameObject quitBtnObj = CreateMuckButton(settingsPanel.transform, "QuitButton", "THOÁT GAME",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(115f, 25f), new Vector2(170f, 42f));

                Button quitBtn = quitBtnObj.GetComponent<Button>();
                quitBtn.onClick.RemoveAllListeners();
                quitBtn.onClick.AddListener(() =>
                {
                    Debug.Log("Thoát Game...");
                    Application.Quit();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                });

                SynchronizeSettingsButtonsStyle(settingsPanel);
            }

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorUtility.DisplayDialog("Hoàn tất!",
                "Đã tinh chỉnh lại Giao diện Muck theo đúng yêu cầu mới:\n\n" +
                "1. Vào game ở sẵn trong phòng chờ luôn (Auto In-Room).\n" +
                "2. Khung góc dưới bên phải tinh giản chỉ còn ô hiển thị LOBBY ID và Nút 'Copy to clipboard' để chép mã gửi bạn bè!", "OK");
        }

        private static void SynchronizeSettingsButtonsStyle(GameObject settingsPanel)
        {
            Button[] allButtons = settingsPanel.GetComponentsInChildren<Button>(true);

            Color woodNormal = new Color(0.48f, 0.3f, 0.16f, 1f);
            Color woodHover = new Color(0.65f, 0.42f, 0.24f, 1f);
            Color woodPressed = new Color(0.32f, 0.19f, 0.09f, 1f);
            Color woodDisabled = new Color(0.25f, 0.2f, 0.18f, 0.6f);

            foreach (Button btn in allButtons)
            {
                btn.transition = Selectable.Transition.ColorTint;
                ColorBlock colors = btn.colors;
                colors.normalColor = woodNormal;
                colors.highlightedColor = woodHover;
                colors.pressedColor = woodPressed;
                colors.selectedColor = woodHover;
                colors.disabledColor = woodDisabled;
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.1f;
                btn.colors = colors;

                if (btn.TryGetComponent<Image>(out var img))
                {
                    img.color = woodNormal;
                }

                Outline outline = btn.GetComponent<Outline>() ?? btn.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.22f, 0.13f, 0.06f, 0.9f);
                outline.effectDistance = new Vector2(2f, -2f);

                TMP_Text tmpText = btn.GetComponentInChildren<TMP_Text>();
                if (tmpText != null)
                {
                    tmpText.fontStyle = FontStyles.Bold;
                    tmpText.color = new Color(0.98f, 0.92f, 0.78f);
                    tmpText.alignment = TextAlignmentOptions.Center;
                }
                else
                {
                    Text legacyText = btn.GetComponentInChildren<Text>();
                    if (legacyText != null)
                    {
                        legacyText.fontStyle = FontStyle.Bold;
                        legacyText.color = new Color(0.98f, 0.92f, 0.78f);
                        legacyText.alignment = TextAnchor.MiddleCenter;
                    }
                }
            }
        }

        private static GameObject CreateOrResetPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            Transform old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            Image img = panel.GetComponent<Image>();
            img.color = new Color(0.18f, 0.11f, 0.07f, 0.92f);

            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.6f, 0.42f, 0.22f, 0.8f);
            outline.effectDistance = new Vector2(3f, -3f);

            return panel;
        }

        private static GameObject CreateMuckButton(Transform parent, string name, string labelText, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            Image img = btnObj.GetComponent<Image>();
            img.color = new Color(0.48f, 0.3f, 0.16f, 1f);

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.22f, 0.13f, 0.06f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(btnObj.transform, false);

            RectTransform txtRect = txtObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;

            TMP_Text txt = txtObj.GetComponent<TextMeshProUGUI>();
            txt.text = labelText;
            txt.fontSize = 16;
            txt.fontStyle = FontStyles.Bold;
            txt.color = new Color(0.98f, 0.92f, 0.78f);
            txt.alignment = TextAlignmentOptions.Center;

            return btnObj;
        }

        private static GameObject CreatePlayerListArea(Transform parent)
        {
            GameObject container = new GameObject("PlayerListContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            container.transform.SetParent(parent, false);

            RectTransform rect = container.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(15f, 75f);
            rect.offsetMax = new Vector2(-15f, -75f);

            VerticalLayoutGroup layout = container.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            return container;
        }

        private static void CreateDummyMemberItem(Transform parent, string memberName)
        {
            GameObject item = new GameObject("MemberItem", typeof(RectTransform));
            item.transform.SetParent(parent, false);
            TMP_Text text = item.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18;
            text.text = memberName;
            text.color = new Color(0.95f, 0.85f, 0.6f);
            text.alignment = TextAlignmentOptions.Left;
        }

        private static GameObject CreateInputField(Transform parent, string name, string placeholderText, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            GameObject fieldObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            fieldObj.transform.SetParent(parent, false);

            RectTransform rect = fieldObj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            Image img = fieldObj.GetComponent<Image>();
            img.color = new Color(0.12f, 0.08f, 0.05f, 0.95f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(fieldObj.transform, false);
            RectTransform areaRect = textArea.GetComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(10f, 5f);
            areaRect.offsetMax = new Vector2(-10f, -5f);

            GameObject phObj = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            phObj.transform.SetParent(textArea.transform, false);
            RectTransform phRect = phObj.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.sizeDelta = Vector2.zero;
            TMP_Text phTxt = phObj.GetComponent<TextMeshProUGUI>();
            phTxt.text = placeholderText;
            phTxt.fontSize = 15;
            phTxt.color = new Color(0.6f, 0.5f, 0.4f);
            phTxt.alignment = TextAlignmentOptions.Left;

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(textArea.transform, false);
            RectTransform txtRect = textObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;
            TMP_Text txt = textObj.GetComponent<TextMeshProUGUI>();
            txt.fontSize = 15;
            txt.color = new Color(0.95f, 0.88f, 0.75f);
            txt.alignment = TextAlignmentOptions.Left;

            TMP_InputField inputField = fieldObj.GetComponent<TMP_InputField>();
            inputField.textViewport = areaRect;
            inputField.textComponent = txt;
            inputField.placeholder = phTxt;

            return fieldObj;
        }

        private static GameObject CreateLabel(Transform parent, string name, string textContent, float fontSize, TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            TMP_Text txt = obj.GetComponent<TextMeshProUGUI>();
            txt.text = textContent;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyles.Bold;
            txt.color = new Color(0.95f, 0.85f, 0.6f);
            txt.alignment = align;

            return obj;
        }
    }
}
