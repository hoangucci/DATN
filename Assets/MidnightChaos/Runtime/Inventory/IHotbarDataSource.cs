using System;

namespace MidnightChaos.Inventory
{
    /// <summary>
    /// Read-only hotbar state plus the one user action a presentation layer
    /// needs. Implement a view with uGUI, UI Toolkit, or another UI system
    /// against this interface; do not read Netcode collections directly.
    /// </summary>
    public interface IHotbarDataSource
    {
        int SlotCount { get; }
        int SelectedSlotIndex { get; }
        VerticalSliceItemId SelectedItem { get; }

        /// <summary>
        /// True when this is the spawned hotbar owned by the local player.
        /// Player-prefab views should remain hidden while this is false.
        /// </summary>
        bool IsLocalPlayerHotbar { get; }

        /// <summary>
        /// Raised after replicated slot contents or selection changes.
        /// </summary>
        event Action HotbarChanged;

        VerticalSliceInventorySlot GetSlot(int index);

        /// <summary>
        /// Requests that the owning player select a slot.
        /// </summary>
        void RequestSelectSlot(int index);
    }
}
