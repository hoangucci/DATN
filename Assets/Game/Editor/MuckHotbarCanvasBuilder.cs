using System.Collections.Generic;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class MuckHotbarCanvasBuilder
    {
        [MenuItem("Game Utility/Build Standalone Hotbar Canvas UI (Tạo Canvas Hotbar Độc Lập Dễ Sửa)")]
        public static void BuildHotbarCanvas()
        {
            // Find or Create Canvas
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject mainCanvasObj = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = mainCanvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = mainCanvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            // Remove old [HotbarCanvas] if exists
            Transform oldHotbar = canvas.transform.Find("[HotbarCanvas]");
            if (oldHotbar != null)
            {
                Object.DestroyImmediate(oldHotbar.gameObject);
            }

            // 1. Create Root Panel for Hotbar Canvas
            GameObject hotbarRoot = new GameObject("[HotbarCanvas]", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(MuckHotbarCanvasBridge));
            hotbarRoot.transform.SetParent(canvas.transform, false);

            RectTransform rootRect = hotbarRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 25f);
            rootRect.sizeDelta = new Vector2(960f, 80f);

            Image bgImg = hotbarRoot.GetComponent<Image>();
            bgImg.color = new Color(0.12f, 0.14f, 0.18f, 0.85f); // Khay tối mờ phong cách Muck

            HorizontalLayoutGroup layout = hotbarRoot.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 8f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Load UI Sprites if available in project
            Sprite slotNormalSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/kenney_ui-pack/PNG/Yellow/Double/button_square_flat.png");
            Sprite slotHighlightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/kenney_ui-pack/PNG/Yellow/Double/button_square_border.png");

            MuckHotbarCanvasBridge bridge = hotbarRoot.GetComponent<MuckHotbarCanvasBridge>();
            SerializedObject bridgeSO = new SerializedObject(bridge);
            SerializedProperty slotsProp = bridgeSO.FindProperty("slots");
            slotsProp.ClearArray();

            // 2. Build 10 Hotbar Slots (0 to 9)
            for (int i = 0; i < 10; i++)
            {
                int slotNumber = i == 9 ? 0 : i + 1;
                GameObject slotObj = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(Image));
                slotObj.transform.SetParent(hotbarRoot.transform, false);

                RectTransform slotRect = slotObj.GetComponent<RectTransform>();
                slotRect.sizeDelta = new Vector2(74f, 74f);

                Image slotBg = slotObj.GetComponent<Image>();
                slotBg.type = Image.Type.Sliced;
                if (slotNormalSprite != null) slotBg.sprite = slotNormalSprite;
                slotBg.color = new Color(0.25f, 0.3f, 0.4f, 0.95f);

                // Highlight Border Image
                GameObject highlightObj = new GameObject("HighlightBorder", typeof(RectTransform), typeof(Image));
                highlightObj.transform.SetParent(slotObj.transform, false);
                RectTransform hlRect = highlightObj.GetComponent<RectTransform>();
                hlRect.anchorMin = Vector2.zero;
                hlRect.anchorMax = Vector2.one;
                hlRect.sizeDelta = Vector2.zero;

                Image hlImg = highlightObj.GetComponent<Image>();
                hlImg.type = Image.Type.Sliced;
                if (slotHighlightSprite != null) hlImg.sprite = slotHighlightSprite;
                hlImg.color = new Color(1f, 0.75f, 0.2f, 1f);
                highlightObj.SetActive(i == 0); // Active on first slot by default

                // Item Icon Image
                GameObject iconObj = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(slotObj.transform, false);
                RectTransform iconRect = iconObj.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(48f, 48f);
                iconObj.SetActive(false);

                // Slot Number Text (Top-Left)
                GameObject numObj = new GameObject("SlotNumberText", typeof(RectTransform), typeof(TextMeshProUGUI));
                numObj.transform.SetParent(slotObj.transform, false);
                RectTransform numRect = numObj.GetComponent<RectTransform>();
                numRect.anchorMin = new Vector2(0f, 1f);
                numRect.anchorMax = new Vector2(0f, 1f);
                numRect.pivot = new Vector2(0f, 1f);
                numRect.anchoredPosition = new Vector2(4f, -3f);
                numRect.sizeDelta = new Vector2(30f, 20f);

                TMP_Text numTxt = numObj.GetComponent<TextMeshProUGUI>();
                numTxt.text = slotNumber.ToString();
                numTxt.fontSize = 12;
                numTxt.fontStyle = FontStyles.Bold;
                numTxt.color = new Color(0.85f, 0.9f, 1f, 0.8f);

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
                amountTxt.fontSize = 12;
                amountTxt.fontStyle = FontStyles.Bold;
                amountTxt.alignment = TextAlignmentOptions.Right;
                amountTxt.color = Color.white;

                // Add to bridge serialized array
                slotsProp.InsertArrayElementAtIndex(i);
                SerializedProperty elem = slotsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("slotRoot").objectReferenceValue = slotObj;
                elem.FindPropertyRelative("slotBackground").objectReferenceValue = slotBg;
                elem.FindPropertyRelative("slotHighlight").objectReferenceValue = hlImg;
                elem.FindPropertyRelative("itemIcon").objectReferenceValue = iconObj.GetComponent<Image>();
                elem.FindPropertyRelative("amountText").objectReferenceValue = amountTxt;
                elem.FindPropertyRelative("slotNumberText").objectReferenceValue = numTxt;
            }

            bridgeSO.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

            Selection.activeGameObject = hotbarRoot;
            EditorUtility.DisplayDialog("Hoàn tất!", "Đã tạo thành công Khung Giao Diện [HotbarCanvas] độc lập trên Canvas!\n\nBây giờ bạn có thể mở Hierarchy, bấm phím 2D để xem và chỉnh sửa giao diện 10 ô Hotbar cực kỳ dễ dàng!", "OK");
        }
    }
}
