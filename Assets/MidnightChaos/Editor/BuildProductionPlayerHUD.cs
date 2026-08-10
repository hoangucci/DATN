using System.Collections.Generic;
using MidnightChaos.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MidnightChaos.Editor
{
    public static class BuildProductionPlayerHUD
    {
        [MenuItem("Midnight Chaos/UI/Build Production Hotbar HUD (Tự Động Dựng Hotbar HUD 100%)")]
        public static void BuildHUD()
        {
            Selection.activeObject = null;

            // Find or Create Main HUD Canvas
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("PlayerHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            else
            {
                canvas.gameObject.name = "PlayerHUD";
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            // Clean old HotbarPanel if exists
            Transform oldPanel = canvas.transform.Find("HotbarPanel");
            if (oldPanel != null)
            {
                Object.DestroyImmediate(oldPanel.gameObject);
            }

            // 1. Create HotbarPanel
            GameObject hotbarPanel = new GameObject("HotbarPanel", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            hotbarPanel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = hotbarPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 20f);
            panelRect.sizeDelta = new Vector2(900f, 85f);

            Image panelBg = hotbarPanel.GetComponent<Image>();
            Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/nen.png") ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/pannel.png");
            if (bgSprite != null)
            {
                panelBg.sprite = bgSprite;
                panelBg.type = Image.Type.Sliced;
                panelBg.color = Color.white;
            }
            else
            {
                panelBg.color = new Color(0.1f, 0.12f, 0.16f, 0.9f);
            }

            HorizontalLayoutGroup layout = hotbarPanel.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Ensure Icon Textures are imported as Sprites
            EnsureSpriteImport("Assets/Asset/UI/Icons/icon_wood.png");
            EnsureSpriteImport("Assets/Asset/UI/Icons/icon_rock.png");
            EnsureSpriteImport("Assets/Asset/UI/Icons/icon_ore.png");
            EnsureSpriteImport("Assets/Asset/UI/Icons/icon_chaos_shard.png");
            EnsureSpriteImport("Assets/Asset/UI/Icons/icon_workbench.png");

            Sprite slotNormalSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/kenney_ui-pack/PNG/Yellow/Double/button_square_flat.png");
            Sprite slotHighlightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/kenney_ui-pack/PNG/Yellow/Double/button_square_border.png");

            Sprite woodSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/Icons/icon_wood.png");
            Sprite rockSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/Icons/icon_rock.png");
            Sprite oreSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/Icons/icon_ore.png");
            Sprite chaosShardSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/Icons/icon_chaos_shard.png");
            Sprite workbenchSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/Icons/icon_workbench.png");

            HotbarUGUIView hotbarView = canvas.GetComponent<HotbarUGUIView>() ?? canvas.gameObject.AddComponent<HotbarUGUIView>();
            SerializedObject viewSO = new SerializedObject(hotbarView);
            viewSO.FindProperty("hotbarPanel").objectReferenceValue = hotbarPanel;

            if (woodSprite != null) viewSO.FindProperty("woodSprite").objectReferenceValue = woodSprite;
            if (rockSprite != null) viewSO.FindProperty("rockSprite").objectReferenceValue = rockSprite;
            if (oreSprite != null) viewSO.FindProperty("oreSprite").objectReferenceValue = oreSprite;
            if (chaosShardSprite != null) viewSO.FindProperty("chaosShardSprite").objectReferenceValue = chaosShardSprite;
            if (workbenchSprite != null) viewSO.FindProperty("workbenchSprite").objectReferenceValue = workbenchSprite;

            SerializedProperty slotViewsProp = viewSO.FindProperty("slotViews");
            slotViewsProp.ClearArray();

            // 2. Build 10 Slots (Slot_1 to Slot_10)
            for (int i = 0; i < 10; i++)
            {
                int displayNum = i == 9 ? 0 : i + 1;
                GameObject slotObj = new GameObject($"Slot_{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HotbarSlotView));
                slotObj.transform.SetParent(hotbarPanel.transform, false);

                RectTransform slotRect = slotObj.GetComponent<RectTransform>();
                slotRect.sizeDelta = new Vector2(75f, 75f);

                Image slotBg = slotObj.GetComponent<Image>();
                slotBg.type = Image.Type.Sliced;
                if (slotNormalSprite != null) slotBg.sprite = slotNormalSprite;
                slotBg.color = new Color(0.25f, 0.32f, 0.42f, 0.95f);

                Button slotBtn = slotObj.GetComponent<Button>();

                // Highlight Frame
                GameObject highlightObj = new GameObject("SelectedFrame", typeof(RectTransform), typeof(Image));
                highlightObj.transform.SetParent(slotObj.transform, false);
                RectTransform hlRect = highlightObj.GetComponent<RectTransform>();
                hlRect.anchorMin = Vector2.zero;
                hlRect.anchorMax = Vector2.one;
                hlRect.sizeDelta = Vector2.zero;

                Image hlImg = highlightObj.GetComponent<Image>();
                hlImg.type = Image.Type.Sliced;
                if (slotHighlightSprite != null) hlImg.sprite = slotHighlightSprite;
                hlImg.color = new Color(1f, 0.78f, 0.15f, 1f);
                highlightObj.SetActive(i == 0);

                // Icon Image
                GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(slotObj.transform, false);
                RectTransform iconRect = iconObj.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(50f, 50f);
                iconObj.SetActive(false);

                // Key Text (Top-Left)
                GameObject keyObj = new GameObject("KeyText", typeof(RectTransform), typeof(TextMeshProUGUI));
                keyObj.transform.SetParent(slotObj.transform, false);
                RectTransform keyRect = keyObj.GetComponent<RectTransform>();
                keyRect.anchorMin = new Vector2(0f, 1f);
                keyRect.anchorMax = new Vector2(0f, 1f);
                keyRect.pivot = new Vector2(0f, 1f);
                keyRect.anchoredPosition = new Vector2(4f, -3f);
                keyRect.sizeDelta = new Vector2(30f, 20f);

                TMP_Text keyTxt = keyObj.GetComponent<TextMeshProUGUI>();
                keyTxt.text = displayNum.ToString();
                keyTxt.fontSize = 12;
                keyTxt.fontStyle = FontStyles.Bold;
                keyTxt.color = new Color(0.9f, 0.95f, 1f, 0.9f);

                // Amount Text (Bottom-Right)
                GameObject amountObj = new GameObject("AmountText", typeof(RectTransform), typeof(TextMeshProUGUI));
                amountObj.transform.SetParent(slotObj.transform, false);
                RectTransform amountRect = amountObj.GetComponent<RectTransform>();
                amountRect.anchorMin = new Vector2(1f, 0f);
                amountRect.anchorMax = new Vector2(1f, 0f);
                amountRect.pivot = new Vector2(1f, 0f);
                amountRect.anchoredPosition = new Vector2(-4f, 3f);
                amountRect.sizeDelta = new Vector2(40f, 20f);

                TMP_Text amountTxt = amountObj.GetComponent<TextMeshProUGUI>();
                amountTxt.text = "";
                amountTxt.fontSize = 13;
                amountTxt.fontStyle = FontStyles.Bold;
                amountTxt.alignment = TextAlignmentOptions.Right;
                amountTxt.color = Color.white;

                // Bind references to HotbarSlotView
                HotbarSlotView slotView = slotObj.GetComponent<HotbarSlotView>();
                SerializedObject slotSO = new SerializedObject(slotView);
                slotSO.FindProperty("slotBackground").objectReferenceValue = slotBg;
                slotSO.FindProperty("itemIcon").objectReferenceValue = iconObj.GetComponent<Image>();
                slotSO.FindProperty("amountText").objectReferenceValue = amountTxt;
                slotSO.FindProperty("keyText").objectReferenceValue = keyTxt;
                slotSO.FindProperty("selectedFrame").objectReferenceValue = highlightObj;
                slotSO.FindProperty("slotButton").objectReferenceValue = slotBtn;
                slotSO.FindProperty("slotIndex").intValue = i;
                slotSO.ApplyModifiedProperties();

                // Add to views list
                slotViewsProp.InsertArrayElementAtIndex(i);
                slotViewsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotView;
            }

            viewSO.ApplyModifiedProperties();

            // 3. Build In-Game Settings Modal Overlay
            BuildSettingsOverlay(canvas.gameObject);

            // 4. Disable old IMGUI Hotbar in DiagnosticNetworkPlayer.prefab
            string playerPrefabPath = "Assets/MidnightChaos/Generated/Prefabs/DiagnosticNetworkPlayer.prefab";
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            if (playerPrefab != null)
            {
                var imgui = playerPrefab.GetComponent<MidnightChaos.Inventory.DiagnosticHotbarIMGUI>();
                if (imgui != null)
                {
                    SerializedObject imguiSO = new SerializedObject(imgui);
                    imguiSO.FindProperty("showHotbar").boolValue = false;
                    imguiSO.ApplyModifiedProperties();
                    PrefabUtility.SavePrefabAsset(playerPrefab);
                    Debug.Log("[BuildHUD] Đã tự động tắt UI IMGUI cũ trên DiagnosticNetworkPlayer.prefab!");
                }
            }

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = canvas.gameObject;

            EditorUtility.DisplayDialog("Hoàn tất 100%!", "Đã tự động dựng xong 100% Giao Diện Hotbar HUD & Bảng Settings mới cực đẹp!\n\n✓ Đã tạo PlayerHUD & 10 ô Slot_1 -> Slot_10.\n✓ Đã nạp Bảng cài đặt Settings (phím ESC / P để bật/tắt khi chơi).\n✓ Đã nạp Icon chuẩn cho Gỗ, Đá, Quặng, Mảnh Chaos & Bàn Chế.\n✓ Đã kết nối InGameSettingsController, HotbarUGUIView & HotbarSlotView.\n✓ Đã tự động tắt UI IMGUI cũ trên Prefab theo hướng dẫn của Hoàng.", "OK");
        }

        private static void BuildSettingsOverlay(GameObject canvasObj)
        {
            Transform oldSettings = canvasObj.transform.Find("[SettingsModalOverlay]");
            if (oldSettings != null)
            {
                Object.DestroyImmediate(oldSettings.gameObject);
            }

            // Dark Backdrop
            GameObject overlay = new GameObject("[SettingsModalOverlay]", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(canvasObj.transform, false);

            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;

            Image overlayImg = overlay.GetComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.75f);

            // Muck Panel
            GameObject panel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(overlay.transform, false);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(480f, 420f);

            Image panelBg = panel.GetComponent<Image>();
            panelBg.color = new Color(0.18f, 0.11f, 0.07f, 0.95f);

            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.6f, 0.42f, 0.22f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);

            // Title
            GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(panel.transform, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -20f);
            titleRect.sizeDelta = new Vector2(400f, 40f);

            TMP_Text titleTxt = titleObj.GetComponent<TextMeshProUGUI>();
            titleTxt.text = "CÀI ĐẶT GAME";
            titleTxt.fontSize = 24;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.color = new Color(0.98f, 0.92f, 0.78f);

            // Buttons: Resume, Return to Menu, Quit
            GameObject resumeBtn = CreateMuckButton(panel.transform, "ResumeButton", "TIẾP TỤC CHƠI", new Vector2(0f, -75f));
            GameObject menuBtn = CreateMuckButton(panel.transform, "MainMenuButton", "VỀ MENU CHÍNH", new Vector2(0f, -145f));
            GameObject quitBtn = CreateMuckButton(panel.transform, "QuitButton", "THOÁT GAME", new Vector2(0f, -215f));

            // Setup Controller
            MidnightChaos.UI.InGameSettingsController ctrl = canvasObj.GetComponent<MidnightChaos.UI.InGameSettingsController>() ?? canvasObj.AddComponent<MidnightChaos.UI.InGameSettingsController>();
            SerializedObject ctrlSO = new SerializedObject(ctrl);
            ctrlSO.FindProperty("settingsOverlay").objectReferenceValue = overlay;
            ctrlSO.FindProperty("resumeButton").objectReferenceValue = resumeBtn.GetComponent<Button>();
            ctrlSO.FindProperty("mainMenuButton").objectReferenceValue = menuBtn.GetComponent<Button>();
            ctrlSO.FindProperty("quitButton").objectReferenceValue = quitBtn.GetComponent<Button>();
            ctrlSO.ApplyModifiedProperties();

            overlay.SetActive(false);
        }

        private static GameObject CreateMuckButton(Transform parent, string name, string labelText, Vector2 pos)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(360f, 50f);

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
            txt.fontSize = 17;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = new Color(0.98f, 0.92f, 0.78f);

            return btnObj;
        }

        private static void EnsureSpriteImport(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
        }
    }
}
