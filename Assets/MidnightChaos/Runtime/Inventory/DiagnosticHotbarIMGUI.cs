using UnityEngine;

namespace MidnightChaos.Inventory
{
    /// <summary>
    /// Replaceable diagnostic presentation for the player hotbar. Disable or
    /// remove this component when a project-specific uGUI/UI Toolkit view is
    /// installed; inventory and networking continue to work independently.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DiagnosticNetworkInventory))]
    public sealed class DiagnosticHotbarIMGUI : MonoBehaviour
    {
        [Header("Diagnostic Hotbar UI")]
        [Tooltip("Shows the built-in diagnostic hotbar for the local player.")]
        [SerializeField] private bool showHotbar = true;

        [Tooltip("Shows a text label describing the currently selected slot.")]
        [SerializeField] private bool showSelectedItemLabel = true;

        [Header("Layout")]
        [Tooltip("Minimum horizontal distance from the edge of the Game view.")]
        [SerializeField, Min(0f)] private float horizontalMargin = 8f;

        [Tooltip("Distance from the bottom edge of the Game view.")]
        [SerializeField, Min(0f)] private float bottomMargin = 8f;

        [Tooltip("Space between adjacent hotbar slots.")]
        [SerializeField, Min(0f)] private float slotGap = 2f;

        [Tooltip("Maximum width of one slot. Slots shrink on narrow screens.")]
        [SerializeField, Min(24f)] private float maximumSlotWidth = 92f;

        [Tooltip("Height of one hotbar slot.")]
        [SerializeField, Min(24f)] private float slotHeight = 58f;

        [Header("Custom Slot Textures")]
        [Tooltip("Kéo thả file ảnh PNG/Texture2D ô bình thường vào đây")]
        [SerializeField] private Texture2D slotNormalTexture;

        [Tooltip("Kéo thả file ảnh PNG/Texture2D ô được chọn (Highlight) vào đây")]
        [SerializeField] private Texture2D slotSelectedTexture;

        private DiagnosticNetworkInventory inventory;
        private GUIStyle hotbarSlotStyle;
        private GUIStyle selectedItemStyle;

        private void Awake()
        {
            inventory = GetComponent<DiagnosticNetworkInventory>();
        }

        private void OnGUI()
        {
            if (!showHotbar || inventory == null ||
                !inventory.IsLocalPlayerHotbar)
            {
                return;
            }

            EnsureStyles();
            int slotCount = inventory.SlotCount;
            float availableWidth = Mathf.Max(
                1f,
                Screen.width - horizontalMargin * 2f);
            float calculatedSlotWidth =
                (availableWidth - slotGap * (slotCount - 1)) / slotCount;
            float slotWidth = Mathf.Min(
                maximumSlotWidth,
                Mathf.Max(1f, calculatedSlotWidth));
            float totalWidth = slotWidth * slotCount +
                               slotGap * (slotCount - 1);
            float startX = Mathf.Max(
                horizontalMargin,
                (Screen.width - totalWidth) * 0.5f);
            float startY = Screen.height - slotHeight - bottomMargin;

            if (showSelectedItemLabel)
            {
                DrawSelectedItemLabel(startX, startY, totalWidth);
            }

            Color previousBackground = GUI.backgroundColor;
            for (int index = 0; index < slotCount; index++)
            {
                DrawSlot(index, startX, startY, slotWidth);
            }
            GUI.backgroundColor = previousBackground;
        }

        private void DrawSelectedItemLabel(
            float startX,
            float startY,
            float totalWidth)
        {
            int selectedIndex = inventory.SelectedSlotIndex;
            VerticalSliceInventorySlot slot = inventory.GetSlot(selectedIndex);
            string text = slot.Item == VerticalSliceItemId.None
                ? $"Selected [{DisplaySlotNumber(selectedIndex)}]: Empty"
                : $"Selected [{DisplaySlotNumber(selectedIndex)}]: " +
                  $"{slot.Item} x{slot.Amount}";
            GUI.Label(
                new Rect(startX, startY - 24f, totalWidth, 22f),
                text,
                selectedItemStyle);
        }

        private void DrawSlot(
            int index,
            float startX,
            float startY,
            float slotWidth)
        {
            VerticalSliceInventorySlot slot = inventory.GetSlot(index);
            string label = slot.Item == VerticalSliceItemId.None
                ? $"{DisplaySlotNumber(index)}\n—"
                : $"{DisplaySlotNumber(index)}\n" +
                  $"{GetShortItemName(slot.Item)}\nx{slot.Amount}";
            bool isSelected = index == inventory.SelectedSlotIndex;
            GUI.backgroundColor = isSelected
                ? new Color(1f, 0.72f, 0.18f, 1f)
                : new Color(0.28f, 0.34f, 0.44f, 0.96f);
            Rect slotRect = new Rect(
                startX + index * (slotWidth + slotGap),
                startY,
                slotWidth,
                slotHeight);

            Texture2D customTex = isSelected ? slotSelectedTexture : slotNormalTexture;
            if (customTex != null)
            {
                GUI.DrawTexture(slotRect, customTex);
            }

            if (GUI.Button(slotRect, label, customTex != null ? GUIStyle.none : hotbarSlotStyle))
            {
                inventory.RequestSelectSlot(index);
            }
        }

        private void EnsureStyles()
        {
            hotbarSlotStyle ??= new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                padding = new RectOffset(2, 2, 2, 2)
            };
            selectedItemStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            selectedItemStyle.normal.textColor = Color.white;
        }

        private static int DisplaySlotNumber(int index)
        {
            return index == DiagnosticNetworkInventory.HotbarSize - 1
                ? 0
                : index + 1;
        }

        private static string GetShortItemName(VerticalSliceItemId item)
        {
            return item switch
            {
                VerticalSliceItemId.Workbench => "BENCH",
                VerticalSliceItemId.ChaosShard => "SHARD",
                VerticalSliceItemId.Rock => "ROCK",
                VerticalSliceItemId.Wood => "WOOD",
                VerticalSliceItemId.Ore => "ORE",
                _ => "EMPTY"
            };
        }
    }
}
