using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidnightChaos.Inventory
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DiagnosticNetworkInventory : NetworkBehaviour
    {
        public const int HotbarSize = 10;

        [Header("Demo Vertical Slice V1")]
        [SerializeField, Min(1)] private int maximumStackSize = 999;

        private readonly NetworkVariable<byte> selectedSlot =
            new NetworkVariable<byte>(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        private NetworkList<VerticalSliceInventorySlot> slots;
        private GUIStyle hotbarSlotStyle;
        private GUIStyle selectedItemStyle;

        public event Action<int, int> WoodChanged;
        public event Action InventoryChanged;

        public int SelectedSlotIndex => Mathf.Clamp(selectedSlot.Value, 0, HotbarSize - 1);
        public VerticalSliceItemId SelectedItem => GetSlot(SelectedSlotIndex).Item;
        public int Wood => Count(VerticalSliceItemId.Wood);

        private void Awake()
        {
            slots = new NetworkList<VerticalSliceInventorySlot>();
        }

        public override void OnNetworkSpawn()
        {
            slots.OnListChanged += HandleListChanged;
            selectedSlot.OnValueChanged += HandleSelectedSlotChanged;
            if (IsServer)
            {
                slots.Clear();
                for (int index = 0; index < HotbarSize; index++)
                {
                    slots.Add(default);
                }
                selectedSlot.Value = 0;
            }
            InventoryChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            slots.OnListChanged -= HandleListChanged;
            selectedSlot.OnValueChanged -= HandleSelectedSlotChanged;
        }

        private void Update()
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                for (int index = 0; index < HotbarSize; index++)
                {
                    if (WasSlotKeyPressed(keyboard, index))
                    {
                        SelectSlotRpc((byte)index);
                    }
                }
            }

            float wheel = Mouse.current != null
                ? Mouse.current.scroll.ReadValue().y
                : 0f;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                int direction = wheel > 0f ? -1 : 1;
                int next = (SelectedSlotIndex + direction + HotbarSize) %
                           HotbarSize;
                SelectSlotRpc((byte)next);
            }
        }

        private static bool WasSlotKeyPressed(Keyboard keyboard, int index)
        {
            return index switch
            {
                0 => keyboard.digit1Key.wasPressedThisFrame,
                1 => keyboard.digit2Key.wasPressedThisFrame,
                2 => keyboard.digit3Key.wasPressedThisFrame,
                3 => keyboard.digit4Key.wasPressedThisFrame,
                4 => keyboard.digit5Key.wasPressedThisFrame,
                5 => keyboard.digit6Key.wasPressedThisFrame,
                6 => keyboard.digit7Key.wasPressedThisFrame,
                7 => keyboard.digit8Key.wasPressedThisFrame,
                8 => keyboard.digit9Key.wasPressedThisFrame,
                9 => keyboard.digit0Key.wasPressedThisFrame,
                _ => false
            };
        }

        public VerticalSliceInventorySlot GetSlot(int index)
        {
            return slots != null && index >= 0 && index < slots.Count
                ? slots[index]
                : default;
        }

        public int Count(VerticalSliceItemId item)
        {
            int total = 0;
            if (slots == null)
            {
                return total;
            }
            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index].Item == item)
                {
                    total += slots[index].Amount;
                }
            }
            return total;
        }

        public bool TryAddItemServer(VerticalSliceItemId item, int amount)
        {
            if (!IsServer || item == VerticalSliceItemId.None || amount <= 0)
            {
                return false;
            }

            int capacity = 0;
            for (int index = 0; index < slots.Count; index++)
            {
                VerticalSliceInventorySlot candidate = slots[index];
                if (candidate.Item == item)
                {
                    capacity += maximumStackSize - candidate.Amount;
                }
                else if (candidate.Item == VerticalSliceItemId.None)
                {
                    capacity += maximumStackSize;
                }
            }
            if (capacity < amount)
            {
                return false;
            }

            int remaining = amount;
            for (int index = 0; index < slots.Count && remaining > 0; index++)
            {
                VerticalSliceInventorySlot slot = slots[index];
                if (slot.Item != item || slot.Amount >= maximumStackSize)
                {
                    continue;
                }
                int added = Mathf.Min(remaining, maximumStackSize - slot.Amount);
                slots[index] = new VerticalSliceInventorySlot(item, slot.Amount + added);
                remaining -= added;
            }
            for (int index = 0; index < slots.Count && remaining > 0; index++)
            {
                if (slots[index].Item != VerticalSliceItemId.None)
                {
                    continue;
                }
                int added = Mathf.Min(remaining, maximumStackSize);
                slots[index] = new VerticalSliceInventorySlot(item, added);
                remaining -= added;
            }
            return remaining == 0;
        }

        public bool TrySpendItemServer(VerticalSliceItemId item, int amount)
        {
            if (!IsServer || amount <= 0 || Count(item) < amount)
            {
                return false;
            }
            int remaining = amount;
            for (int index = slots.Count - 1; index >= 0 && remaining > 0; index--)
            {
                VerticalSliceInventorySlot slot = slots[index];
                if (slot.Item != item)
                {
                    continue;
                }
                int spent = Mathf.Min(remaining, slot.Amount);
                int next = slot.Amount - spent;
                slots[index] = next == 0
                    ? default
                    : new VerticalSliceInventorySlot(item, next);
                remaining -= spent;
            }
            return true;
        }

        public bool TrySpendSelectedServer(
            VerticalSliceItemId expectedItem,
            int amount)
        {
            if (!IsServer || SelectedItem != expectedItem || amount <= 0)
            {
                return false;
            }
            VerticalSliceInventorySlot slot = slots[SelectedSlotIndex];
            if (slot.Amount < amount)
            {
                return false;
            }
            int next = slot.Amount - amount;
            slots[SelectedSlotIndex] = next == 0
                ? default
                : new VerticalSliceInventorySlot(expectedItem, next);
            return true;
        }

        public bool TryAddWoodServer(int amount) =>
            TryAddItemServer(VerticalSliceItemId.Wood, amount);

        public bool TrySpendWoodServer(int amount) =>
            TrySpendItemServer(VerticalSliceItemId.Wood, amount);

        [Rpc(
            SendTo.Server,
            InvokePermission = RpcInvokePermission.Owner)]
        private void SelectSlotRpc(byte index)
        {
            if (index < HotbarSize)
            {
                selectedSlot.Value = index;
            }
        }

        private void HandleListChanged(
            NetworkListEvent<VerticalSliceInventorySlot> change)
        {
            InventoryChanged?.Invoke();
            int previousWood = Wood;
            // Compatibility event consumers only refresh their label; the
            // exact previous count is not part of gameplay authority.
            WoodChanged?.Invoke(previousWood, Wood);
        }

        private void HandleSelectedSlotChanged(byte previous, byte current)
        {
            InventoryChanged?.Invoke();
        }

        private void OnGUI()
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }
            EnsureHotbarStyles();
            const float margin = 8f;
            const float gap = 2f;
            const float slotHeight = 58f;
            float availableWidth = Mathf.Max(
                400f,
                Screen.width - margin * 2f);
            float slotWidth = Mathf.Min(
                92f,
                (availableWidth - gap * (HotbarSize - 1)) / HotbarSize);
            float totalWidth = slotWidth * HotbarSize +
                               gap * (HotbarSize - 1);
            float startX = Mathf.Max(margin, (Screen.width - totalWidth) * 0.5f);
            float startY = Screen.height - slotHeight - 8f;

            VerticalSliceInventorySlot selectedSlotData =
                GetSlot(SelectedSlotIndex);
            string selectedText = selectedSlotData.Item ==
                                  VerticalSliceItemId.None
                ? $"Selected [{DisplaySlotNumber(SelectedSlotIndex)}]: Empty"
                : $"Selected [{DisplaySlotNumber(SelectedSlotIndex)}]: " +
                  $"{selectedSlotData.Item} x{selectedSlotData.Amount}";
            GUI.Label(
                new Rect(startX, startY - 24f, totalWidth, 22f),
                selectedText,
                selectedItemStyle);

            Color previousBackground = GUI.backgroundColor;
            for (int index = 0; index < HotbarSize; index++)
            {
                VerticalSliceInventorySlot slot = GetSlot(index);
                string label = slot.Item == VerticalSliceItemId.None
                    ? $"{DisplaySlotNumber(index)}\n—"
                    : $"{DisplaySlotNumber(index)}\n" +
                      $"{GetShortItemName(slot.Item)}\nx{slot.Amount}";
                GUI.backgroundColor = index == SelectedSlotIndex
                    ? new Color(1f, 0.72f, 0.18f, 1f)
                    : new Color(0.28f, 0.34f, 0.44f, 0.96f);
                Rect slotRect = new Rect(
                    startX + index * (slotWidth + gap),
                    startY,
                    slotWidth,
                    slotHeight);
                if (GUI.Button(slotRect, label, hotbarSlotStyle))
                {
                    SelectSlotRpc((byte)index);
                }
            }
            GUI.backgroundColor = previousBackground;
        }

        private void EnsureHotbarStyles()
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
            return index == HotbarSize - 1 ? 0 : index + 1;
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
