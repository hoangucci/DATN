using System.Collections.Generic;
using MidnightChaos.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public class MuckHotbarCanvasBridge : MonoBehaviour
    {
        [Header("UI Canvas References")]
        [SerializeField] private GameObject hotbarPanel;
        [SerializeField] private List<HotbarSlotUI> slots = new List<HotbarSlotUI>();

        [Header("Item Icons Database")]
        [SerializeField] private Sprite woodSprite;
        [SerializeField] private Sprite rockSprite;
        [SerializeField] private Sprite oreSprite;
        [SerializeField] private Sprite chaosShardSprite;
        [SerializeField] private Sprite workbenchSprite;

        private DiagnosticNetworkInventory inventory;

        [System.Serializable]
        public struct HotbarSlotUI
        {
            public GameObject slotRoot;
            public Image slotBackground;
            public Image slotHighlight;
            public Image itemIcon;
            public TMP_Text amountText;
            public TMP_Text slotNumberText;
        }

        private void Awake()
        {
            inventory = GetComponentInParent<DiagnosticNetworkInventory>() ?? FindFirstObjectByType<DiagnosticNetworkInventory>();
        }

        private void Start()
        {
            // Ban đầu tự động ẩn Hotbar đi khi chưa vào gameplay
            SetHotbarVisible(false);
        }

        private void Update()
        {
            if (inventory == null)
            {
                inventory = GetComponentInParent<DiagnosticNetworkInventory>() ?? FindFirstObjectByType<DiagnosticNetworkInventory>();
                if (inventory == null || !inventory.IsLocalPlayerHotbar)
                {
                    SetHotbarVisible(false);
                    return;
                }
            }

            if (!inventory.IsLocalPlayerHotbar)
            {
                SetHotbarVisible(false);
                return;
            }

            // Đã vào gameplay và có Player -> Hiện Hotbar Canvas
            SetHotbarVisible(true);

            int slotCount = Mathf.Min(inventory.SlotCount, slots.Count);
            int selectedIndex = inventory.SelectedSlotIndex;

            for (int i = 0; i < slotCount; i++)
            {
                var slotData = inventory.GetSlot(i);
                var ui = slots[i];

                // Update highlight state
                if (ui.slotHighlight != null)
                {
                    ui.slotHighlight.gameObject.SetActive(i == selectedIndex);
                }

                // Update item icon & amount
                if (slotData.Item == VerticalSliceItemId.None || slotData.Amount <= 0)
                {
                    if (ui.itemIcon != null) ui.itemIcon.gameObject.SetActive(false);
                    if (ui.amountText != null) ui.amountText.text = "";
                }
                else
                {
                    if (ui.itemIcon != null)
                    {
                        ui.itemIcon.gameObject.SetActive(true);
                        ui.itemIcon.sprite = GetSpriteForItem(slotData.Item);
                    }

                    if (ui.amountText != null)
                    {
                        ui.amountText.text = slotData.Amount > 1 ? $"x{slotData.Amount}" : "";
                    }
                }
            }
        }

        private Sprite GetSpriteForItem(VerticalSliceItemId item)
        {
            return item switch
            {
                VerticalSliceItemId.Wood => woodSprite,
                VerticalSliceItemId.Rock => rockSprite,
                VerticalSliceItemId.Ore => oreSprite,
                VerticalSliceItemId.ChaosShard => chaosShardSprite,
                VerticalSliceItemId.Workbench => workbenchSprite,
                _ => null
            };
        }

        private void SetHotbarVisible(bool visible)
        {
            if (hotbarPanel != null && hotbarPanel.activeSelf != visible)
            {
                hotbarPanel.SetActive(visible);
            }
            else if (hotbarPanel == null && gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }
    }
}
