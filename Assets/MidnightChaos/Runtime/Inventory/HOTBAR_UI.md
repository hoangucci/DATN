# Hotbar UI integration

The hotbar has two separate responsibilities:

- `DiagnosticNetworkInventory` owns replicated inventory state, hotkey input,
  and the selected slot.
- `DiagnosticHotbarIMGUI` is only the replaceable diagnostic presentation.

Custom UI must read hotbar state through `IHotbarDataSource`. It should not
read or modify Netcode collections, invoke RPCs, or duplicate inventory rules.

## Creating a custom view

1. Put the custom view on the player prefab or connect it to the local player.
2. Get `DiagnosticNetworkInventory` as an `IHotbarDataSource`.
3. Subscribe to `HotbarChanged` in `OnEnable` and unsubscribe in `OnDisable`.
4. Build `SlotCount` visual slots and refresh each one with `GetSlot(index)`.
5. Highlight `SelectedSlotIndex`.
6. Call `RequestSelectSlot(index)` when the user clicks a slot.
7. Only show the view while `IsLocalPlayerHotbar` is true.
8. Disable or remove `DiagnosticHotbarIMGUI` after the replacement is active.

Minimal binding example:

```csharp
private DiagnosticNetworkInventory inventory;
private IHotbarDataSource hotbar;

private void Awake()
{
    inventory = GetComponent<DiagnosticNetworkInventory>();
    hotbar = inventory;
}

private void OnEnable()
{
    hotbar.HotbarChanged += Refresh;
}

private void OnDisable()
{
    hotbar.HotbarChanged -= Refresh;
}

private void Refresh()
{
    for (int index = 0; index < hotbar.SlotCount; index++)
    {
        VerticalSliceInventorySlot slot = hotbar.GetSlot(index);
        // Update the icon, amount label, and selected state here.
    }
}

public void SelectSlot(int index)
{
    hotbar.RequestSelectSlot(index);
}
```

`RequestSelectSlot` validates ownership and index range. Item addition and
consumption remain server-authoritative gameplay operations and intentionally
are not exposed by the UI interface.
