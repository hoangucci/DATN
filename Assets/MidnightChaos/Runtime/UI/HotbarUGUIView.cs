using System.Collections.Generic;
using MidnightChaos.Inventory;
using Unity.Netcode;
using UnityEngine;

namespace MidnightChaos.UI
{
    public class HotbarUGUIView : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject hotbarPanel;
        [SerializeField] private List<HotbarSlotView> slotViews = new List<HotbarSlotView>();

        [Header("Item Sprites Presentation")]
        [SerializeField] private Sprite woodSprite;
        [SerializeField] private Sprite rockSprite;
        [SerializeField] private Sprite oreSprite;
        [SerializeField] private Sprite chaosShardSprite;
        [SerializeField] private Sprite workbenchSprite;

        private IHotbarDataSource hotbar;

        private void Awake()
        {
            LoadDefaultSpritesIfNull();
        }

        private void LoadDefaultSpritesIfNull()
        {
#if UNITY_EDITOR
            if (woodSprite == null) woodSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/Icons/icon_wood.png");
            if (rockSprite == null) rockSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/Icons/icon_rock.png");
            if (oreSprite == null) oreSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/Icons/icon_ore.png");
            if (chaosShardSprite == null) chaosShardSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/Icons/icon_chaos_shard.png");
            if (workbenchSprite == null) workbenchSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/UI/Icons/icon_workbench.png");
#endif
        }

        private void Start()
        {
            LoadDefaultSpritesIfNull();
            InitializeSlotIndices();
        }

        private void InitializeSlotIndices()
        {
            for (int i = 0; i < slotViews.Count; i++)
            {
                if (slotViews[i] != null)
                {
                    slotViews[i].Initialize(i, this);
                }
            }
        }

        private void Update()
        {
            if (hotbar == null)
            {
                TryBindLocalPlayer();
            }
        }

        private void TryBindLocalPlayer()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                return;
            }

            if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                var inventory = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<DiagnosticNetworkInventory>();
                if (inventory != null)
                {
                    Bind(inventory);
                }
            }
        }

        private void Bind(IHotbarDataSource source)
        {
            Unbind();
            hotbar = source;
            if (hotbar == null) return;

            hotbar.HotbarChanged += Refresh;
            Refresh();
        }

        private void Unbind()
        {
            if (hotbar != null)
            {
                hotbar.HotbarChanged -= Refresh;
                hotbar = null;
            }
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Refresh()
        {
            bool visible = hotbar != null && hotbar.IsLocalPlayerHotbar;
            if (hotbarPanel != null)
            {
                hotbarPanel.SetActive(visible);
            }

            if (!visible) return;

            int count = Mathf.Min(hotbar.SlotCount, slotViews.Count);
            for (int index = 0; index < count; index++)
            {
                VerticalSliceInventorySlot slot = hotbar.GetSlot(index);
                Sprite icon = GetSpriteForItem(slot.Item);

                slotViews[index].SetItem(slot.Item, icon);
                slotViews[index].SetAmount(slot.Amount);
                slotViews[index].SetSelected(index == hotbar.SelectedSlotIndex);
            }
        }

        public void OnSlotClicked(int index)
        {
            hotbar?.RequestSelectSlot(index);
        }

        private Sprite GetSpriteForItem(VerticalSliceItemId itemId)
        {
            return itemId switch
            {
                VerticalSliceItemId.Wood => woodSprite,
                VerticalSliceItemId.Rock => rockSprite,
                VerticalSliceItemId.Ore => oreSprite,
                VerticalSliceItemId.ChaosShard => chaosShardSprite,
                VerticalSliceItemId.Workbench => workbenchSprite,
                _ => null
            };
        }
    }
}
