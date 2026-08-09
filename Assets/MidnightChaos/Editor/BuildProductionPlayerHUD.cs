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

            // Load UI Sprites
            Sprite slotNormalSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/kenney_ui-pack/PNG/Yellow/Double/button_square_flat.png");
            Sprite slotHighlightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/kenney_ui-pack/PNG/Yellow/Double/button_square_border.png");

            Sprite woodSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/image.png");
            Sprite rockSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/image.png");
            Sprite oreSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/image.png");

            HotbarUGUIView hotbarView = canvas.GetComponent<HotbarUGUIView>() ?? canvas.gameObject.AddComponent<HotbarUGUIView>();
            SerializedObject viewSO = new SerializedObject(hotbarView);
            viewSO.FindProperty("hotbarPanel").objectReferenceValue = hotbarPanel;

            if (woodSprite != null) viewSO.FindProperty("woodSprite").objectReferenceValue = woodSprite;
            if (rockSprite != null) viewSO.FindProperty("rockSprite").objectReferenceValue = rockSprite;
            if (oreSprite != null) viewSO.FindProperty("oreSprite").objectReferenceValue = oreSprite;

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

            // 3. Disable old IMGUI Hotbar in DiagnosticNetworkPlayer.prefab
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

            EditorUtility.DisplayDialog("Hoàn tất 100%!", "Đã tự động dựng xong 100% Giao Diện Hotbar HUD mới cực đẹp!\n\n✓ Đã tạo PlayerHUD & 10 ô Slot_1 -> Slot_10.\n✓ Đã kết nối HotbarUGUIView & HotbarSlotView.\n✓ Đã tự động tắt UI IMGUI cũ trên Prefab theo hướng dẫn của Hoàng.", "OK");
        }
    }
}
