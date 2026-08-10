# Hướng dẫn tích hợp và thay thế Hotbar UI

Tài liệu này dành cho người chỉ làm giao diện và không cần hiểu toàn bộ Netcode,
inventory hoặc crafting của project.

## 1. Bắt đầu đọc từ đâu?

Đọc theo đúng thứ tự sau:

1. `IHotbarDataSource.cs`: API mà UI được phép sử dụng.
2. `DiagnosticHotbarIMGUI.cs`: ví dụ UI chẩn đoán đang hiển thị trong game.
3. `VerticalSliceItem.cs`: định nghĩa `VerticalSliceItemId` và dữ liệu một slot.
4. Chỉ đọc `DiagnosticNetworkInventory.cs` khi cần debug dữ liệu/network.

Không bắt đầu bằng cách sửa `DiagnosticNetworkInventory`. UI bình thường không
cần biết `NetworkList`, RPC hoặc luật thêm/trừ item.

## 2. Kiến trúc hiện tại

| Thành phần | Trách nhiệm | Có phải UI không? |
| --- | --- | --- |
| `DiagnosticNetworkInventory` | Dữ liệu inventory replicated, slot đang chọn, phím `1-0`, con lăn chuột và server authority | Không |
| `IHotbarDataSource` | Hợp đồng công khai để UI đọc dữ liệu và yêu cầu chọn slot | Không |
| `DiagnosticHotbarIMGUI` | Hotbar chẩn đoán hiện tại | Có, có thể thay thế |
| UI mới của bạn | Icon, text, animation, tooltip, drag/drop và layout | Có |

Luồng đúng:

```text
DiagnosticNetworkInventory
        ↓ implements
IHotbarDataSource
        ↓ read/subscribe
Hotbar UI của bạn
```

UI không sở hữu item. UI chỉ phản ánh dữ liệu đã được network đồng bộ.

## 3. Chỗ nào được chỉnh?

### Được chỉnh trực tiếp

- Màu, font, icon, sprite, animation và bố cục UI mới.
- Cách hiển thị tên của từng `VerticalSliceItemId`.
- Thứ tự visual, tooltip và hiệu ứng slot được chọn.
- Nút UI gọi `RequestSelectSlot(index)`.
- Bật/tắt `DiagnosticHotbarIMGUI`.
- Các thông số layout của UI chẩn đoán trong Inspector:
  - `Show Hotbar`;
  - `Show Selected Item Label`;
  - `Horizontal Margin`;
  - `Bottom Margin`;
  - `Slot Gap`;
  - `Maximum Slot Width`;
  - `Slot Height`.

Các thông số trên nằm ở component `DiagnosticHotbarIMGUI` của
`DiagnosticNetworkPlayer.prefab`.

### Được chỉnh nhưng đây là thay đổi gameplay, không phải thay skin UI

- `Maximum Stack Size` nằm trên component `DiagnosticNetworkInventory`.
- Mapping phím `1-0` và con lăn hiện nằm trong
  `DiagnosticNetworkInventory.Update()` và `WasSlotKeyPressed()`.
- Thêm một loại item mới bắt đầu từ `VerticalSliceItemId` trong
  `VerticalSliceItem.cs`, sau đó phải cập nhật pickup, crafting, equipment,
  placement và UI liên quan.

Những thay đổi trên cần được kiểm thử Host + Client.

### Không được chỉnh từ code UI

- Không sửa hoặc truy cập trực tiếp `NetworkList` chứa các slot.
- Không tự đặt `SelectedSlotIndex`.
- Không gọi RPC bằng reflection hoặc tạo RPC chọn slot khác.
- Không gọi `TryAddItemServer`, `TrySpendItemServer` hoặc
  `TrySpendSelectedServer` từ nút UI thông thường.
- Không tự cộng/trừ số lượng item ở client để làm UI trông đúng.
- Không nhân bản logic pickup, crafting hoặc placement vào UI.
- Không đổi `HotbarSize = 10` chỉ để thay bố cục. Đây là kích thước dữ liệu,
  không phải số ô visual tùy ý.

Nếu UI cần thay đổi item, hành động đó phải đi qua gameplay system có server
authority tương ứng.

## 4. Chỉnh nhanh UI chẩn đoán hiện tại

1. Mở prefab:
   `Assets/MidnightChaos/Generated/Prefabs/DiagnosticNetworkPlayer.prefab`.
2. Chọn GameObject gốc `DiagnosticNetworkPlayer`.
3. Tìm component `Diagnostic Hotbar IMGUI`.
4. Chỉnh các trường trong phần `Diagnostic Hotbar UI` và `Layout`.
5. Chạy Host, sau đó chạy Host + Client để kiểm tra.

Lưu ý: đây là prefab trong thư mục `Generated`. Chạy lại LAN/bootstrap builder
có thể tạo lại prefab và đưa các giá trị về mặc định. Không nên đặt production
UI phức tạp trực tiếp vào prefab generated này.

## 5. Cách khuyến nghị: tạo HUD trong scene

HUD trong scene không bị nhân bản theo từng network player và ít bị builder ghi
đè hơn.

### Bước 1 - Tạo hierarchy

Trong scene gameplay, tạo tối thiểu:

```text
PlayerHUD (Canvas - Screen Space Overlay)
└── HotbarPanel
    ├── Slot_1 (Button)
    ├── Slot_2 (Button)
    ├── ...
    └── Slot_0 (Button)
```

Mỗi slot nên có:

```text
Slot_X
├── Icon
├── AmountText
├── KeyText
└── SelectedFrame
```

Tạo đủ 10 slot vì `SlotCount` hiện trả về 10. Không hardcode dữ liệu item vào
prefab slot; slot visual chỉ giữ reference đến `Image`, text và selected frame.

### Bước 2 - Tạo component binder/presenter

Tạo script mới trong một thư mục UI riêng, ví dụ:

```text
Assets/MidnightChaos/Runtime/UI/HotbarUGUIView.cs
```

Script này có ba nhiệm vụ duy nhất:

1. Tìm `DiagnosticNetworkInventory` trên local player.
2. Subscribe `IHotbarDataSource.HotbarChanged`.
3. Đưa dữ liệu từ `GetSlot(index)` lên các visual slot.

Không đưa crafting, pickup hoặc server item mutation vào script này.

### Bước 3 - Bind local player

HUD trong scene phải bind vào:

```csharp
NetworkManager.Singleton.LocalClient.PlayerObject
```

Sau khi có `PlayerObject`:

```csharp
DiagnosticNetworkInventory inventory =
    playerObject.GetComponent<DiagnosticNetworkInventory>();
IHotbarDataSource hotbar = inventory;
```

Không dùng `FindFirstObjectByType<DiagnosticNetworkInventory>()`, vì trong LAN
có nhiều player và kết quả có thể là inventory của remote player.

### Bước 4 - Subscribe và unsubscribe

```csharp
private IHotbarDataSource hotbar;

private void Bind(IHotbarDataSource source)
{
    Unbind();
    hotbar = source;
    if (hotbar == null)
    {
        return;
    }

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
```

Phải unsubscribe khi HUD bị disable, scene unload, disconnect hoặc bind sang
player mới.

### Bước 5 - Refresh visual

```csharp
private void Refresh()
{
    bool visible = hotbar != null && hotbar.IsLocalPlayerHotbar;
    hotbarPanel.SetActive(visible);
    if (!visible)
    {
        return;
    }

    for (int index = 0; index < hotbar.SlotCount; index++)
    {
        VerticalSliceInventorySlot slot = hotbar.GetSlot(index);

        slotViews[index].SetItem(slot.Item);
        slotViews[index].SetAmount(slot.Amount);
        slotViews[index].SetSelected(
            index == hotbar.SelectedSlotIndex);
    }
}
```

`slot.Item == VerticalSliceItemId.None` nghĩa là slot rỗng. Với slot rỗng,
ẩn icon và amount thay vì tự điền item mặc định.

### Bước 6 - Xử lý click

Mỗi button chỉ gọi:

```csharp
public void OnSlotClicked(int index)
{
    hotbar?.RequestSelectSlot(index);
}
```

Index runtime là `0-9`. Cách hiển thị phím là `1-9, 0`:

```csharp
string keyLabel = index == 9 ? "0" : (index + 1).ToString();
```

Không gọi gameplay action ngay trong callback chọn slot.

### Bước 7 - Icon và tên item

UI tự quản lý mapping presentation:

```text
VerticalSliceItemId → Display Name + Icon + Color
```

Nên tạo một ScriptableObject UI riêng, ví dụ
`HotbarPresentationSettings`, nếu cần cho nhiều item. Asset UI này chỉ chứa
sprite/text/color và không được chứa stack count hoặc gameplay authority.

Không thêm icon/sprite vào `DiagnosticNetworkInventory`.

### Bước 8 - Tắt UI cũ

Sau khi HUD mới hoạt động:

1. Mở `DiagnosticNetworkPlayer.prefab`.
2. Tắt checkbox của `DiagnosticHotbarIMGUI` hoặc bỏ chọn `Show Hotbar`.
3. Không xóa `DiagnosticNetworkInventory`.

Nếu production UI nằm trong scene, chỉ cần tắt UI chẩn đoán. Inventory,
hotkeys, crafting, placement và network vẫn tiếp tục hoạt động.

## 6. Nếu buộc phải đặt UI trên player prefab

Chỉ dùng cách này khi UI cần đi cùng player prefab.

1. Component UI phải tự ẩn khi `IsLocalPlayerHotbar == false`.
2. Không disable toàn bộ player GameObject; chỉ ẩn Canvas/panel của UI.
3. Không tạo `NetworkObject` cho Canvas hoặc slot.
4. Không thêm `NetworkVariable` chỉ để đồng bộ icon/text.
5. Cần cập nhật builder tương ứng để component không mất khi prefab được tạo
   lại.

Vị trí builder hiện tại:

- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`;
- `Assets/MidnightChaos/Editor/MidnightChaosProceduralDemoBuilder.cs`.

## 7. API UI được phép sử dụng

| API | Mục đích |
| --- | --- |
| `SlotCount` | Số slot dữ liệu hiện tại |
| `SelectedSlotIndex` | Index slot đang chọn |
| `SelectedItem` | Item đang chọn |
| `IsLocalPlayerHotbar` | Có phải hotbar local đã spawn hay không |
| `GetSlot(index)` | Đọc item và amount của một slot |
| `HotbarChanged` | Báo UI cần refresh |
| `RequestSelectSlot(index)` | Yêu cầu chọn slot từ local owner |

Đây là toàn bộ API cần thiết cho một hotbar hiển thị/chọn slot.

## 8. Checklist kiểm thử bắt buộc

### Một người chơi/Host

- Hotbar chỉ xuất hiện sau khi local player spawn.
- Slot rỗng không có icon và amount.
- Pickup bằng `E` cập nhật đúng slot và amount.
- Phím `1-9`, `0` chọn đúng slot.
- Con lăn chuột đổi slot đúng vòng lặp.
- Click slot gọi chọn slot, không tự sử dụng item.
- Selected frame luôn khớp với item đang equip.
- Craft Workbench cập nhật amount.
- Đặt Workbench trừ đúng item và refresh UI.

### Host + Client

- Host chỉ thấy hotbar của Host.
- Client chỉ thấy hotbar của Client.
- Pickup của Client không làm thay đổi hotbar Host.
- Remote player spawn/despawn không tạo thêm HUD.
- Disconnect/reconnect không để event subscription cũ tồn tại.

### Regression

- Attack animation vẫn hoạt động khi đổi slot.
- Equip Rock/Workbench vẫn dùng đúng `SelectedItem`.
- Tắt toàn bộ production UI không làm inventory/network ngừng hoạt động.

## 9. Khi nào cần hỏi người phụ trách gameplay/network?

Dừng chỉnh UI và hỏi trước nếu yêu cầu bao gồm:

- đổi số lượng slot;
- thêm/xóa loại item;
- thay đổi maximum stack;
- drag/drop làm đổi vị trí item;
- split/merge stack;
- bỏ item ra thế giới;
- dùng item từ UI;
- thay đổi key binding gameplay;
- client tự thêm/trừ item.

Đây không còn là thay đổi presentation và có thể ảnh hưởng protocol, server
authority, crafting, equipment hoặc placement.
