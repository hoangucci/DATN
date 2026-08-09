using MidnightChaos.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MidnightChaos.UI
{
    public class HotbarSlotView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image slotBackground;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private TMP_Text keyText;
        [SerializeField] private GameObject selectedFrame;
        [SerializeField] private Button slotButton;

        [Header("Slot Index")]
        [SerializeField] private int slotIndex;

        private HotbarUGUIView parentView;

        public void Initialize(int index, HotbarUGUIView parent)
        {
            slotIndex = index;
            parentView = parent;

            if (keyText != null)
            {
                // Slot 0-8 is key 1-9, Slot 9 is key 0
                string keyName = index == 9 ? "0" : (index + 1).ToString();
                keyText.text = keyName;
            }

            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
                slotButton.onClick.AddListener(() =>
                {
                    if (parentView != null)
                    {
                        parentView.OnSlotClicked(slotIndex);
                    }
                });
            }
        }

        public void SetItem(VerticalSliceItemId itemId, Sprite iconSprite)
        {
            if (itemId == VerticalSliceItemId.None || iconSprite == null)
            {
                if (itemIcon != null) itemIcon.gameObject.SetActive(false);
            }
            else
            {
                if (itemIcon != null)
                {
                    itemIcon.gameObject.SetActive(true);
                    itemIcon.sprite = iconSprite;
                }
            }
        }

        public void SetAmount(int amount)
        {
            if (amountText != null)
            {
                amountText.text = amount > 1 ? $"x{amount}" : "";
            }
        }

        public void SetSelected(bool isSelected)
        {
            if (selectedFrame != null)
            {
                selectedFrame.SetActive(isSelected);
            }
        }
    }
}
