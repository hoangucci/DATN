using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidnightChaos.Inventory
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DiagnosticNetworkInventory : NetworkBehaviour,
        IHotbarDataSource
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
        public event Action<int, int> WoodChanged;
        public event Action InventoryChanged;

        /// <summary>
        /// Raised when slot contents or the selected slot changes. Custom
        /// hotbar views should subscribe to this event and refresh themselves.
        /// </summary>
        public event Action HotbarChanged
        {
            add => InventoryChanged += value;
            remove => InventoryChanged -= value;
        }

        public int SlotCount => HotbarSize;
        public int SelectedSlotIndex => Mathf.Clamp(selectedSlot.Value, 0, HotbarSize - 1);
        public VerticalSliceItemId SelectedItem => GetSlot(SelectedSlotIndex).Item;
        public bool IsLocalPlayerHotbar => IsOwner && IsSpawned;
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
                        RequestSelectSlot(index);
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
                RequestSelectSlot(next);
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

        /// <summary>
        /// Requests a selected-slot change from the owning player. UI code
        /// should call this method instead of interacting with Netcode RPCs.
        /// Invalid indices and calls made by non-owners are ignored.
        /// </summary>
        public void RequestSelectSlot(int index)
        {
            if (!IsOwner || !IsSpawned || index < 0 || index >= HotbarSize)
            {
                return;
            }

            SelectSlotRpc((byte)index);
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
    }
}
