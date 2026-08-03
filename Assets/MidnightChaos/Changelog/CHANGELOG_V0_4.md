# CHANGELOG V0.4 — Host-Validated Sword Crafting

> Changelog này được tái dựng bằng cách phủ gói vá `v0.4` lên source tích lũy `v0.3.2`, sau đó so sánh code trước/sau.

## Phần 1 — File

Tổng thay đổi: **9 file**

File ghi đè — **4**:

- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticMeleeCombat.cs`
- `Assets/MidnightChaos/Runtime/Inventory/DiagnosticNetworkInventory.cs`
- `Assets/MidnightChaos/Runtime/UI/DiagnosticLanUI.cs`

File mới — **5**:

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_4.md`
- `Assets/MidnightChaos/Runtime/Crafting/DiagnosticCraftingInteractor.cs`
- `Assets/MidnightChaos/Runtime/Crafting/DiagnosticCraftingStation.cs`
- `Assets/MidnightChaos/Runtime/Equipment/DiagnosticPlayerEquipment.cs`
- `Assets/MidnightChaos/Runtime/UI/DiagnosticWorldCraftingLabel.cs`

File xóa: **0**

### Thay đổi chính

- Thêm Gate D: chế tạo Sword tại Workbench do Host xác thực.
- Nhấn `E` gần và nhìn về Workbench để yêu cầu craft; client không quyết định cost hoặc tự cấp item.
- Host kiểm tra Player còn sống, chưa có Sword, cooldown request, khoảng cách `2.8 m`, nửa góc `75°` và đủ Wood.
- Chi phí mặc định: `3 Wood`; cooldown request `0.25 s`.
- Inventory thêm `TrySpendWoodServer`; nếu cấp Sword thất bại sau khi trừ Wood, code có nhánh refund phòng thủ.
- `hasSword` là `NetworkVariable<bool>` chỉ Server ghi và mọi peer đọc.
- Thêm SwordVisual chẩn đoán và đồng bộ trạng thái hiện/ẩn theo trang bị.
- Damage tay không giữ `25`; có Sword tăng thành `40`.
- Builder tạo material Sword/Workbench, Workbench trong scene và gắn các component crafting/equipment vào Player prefab.
- UI hiển thị Wood, trạng thái Sword, damage hiện tại và cost craft.
- `NetworkConfig.ProtocolVersion`: `1 → 2` để chặn build `v0.3.x` có layout `NetworkBehaviour` cũ.

## Phần 2 — Thiết lập Unity và kiểm thử

### Thiết lập

1. Chép các file trong ZIP vào project đang ở `v0.3.2` và ghi đè.
2. Chờ Unity compile không còn lỗi đỏ.
3. Chạy `Midnight Chaos > Bootstrap > Create or Refresh LAN Test Scene`.
4. Mở scene mới tạo và build lại mọi Client; `v0.3.x` không được chơi lẫn với `v0.4`.

### Test bắt buộc

- Gather đủ `3 Wood`, đứng gần và nhìn vào Workbench, nhấn `E`: Wood về `0`, `HasSword = true` trên mọi peer.
- SwordVisual chỉ hiện sau khi Host cấp Sword.
- Tay không gây `25 damage`; Sword gây `40 damage`.
- Không đủ Wood, ở ngoài `2.8 m`, quay lưng, đã có Sword hoặc đã chết thì craft thất bại và không mất Wood.
- Spam `E` không tạo nhiều transaction craft.
- Một Player chỉ được cấp Sword một lần.
- Attack/harvest hợp nhất của `v0.3.2` và reconnect LAN vẫn hoạt động.

## Phần 3 — Nói thêm

- Đây là một recipe cố định, chưa phải hệ thống recipe/item/equipment tổng quát.
- Sword và Workbench chỉ là primitive chẩn đoán.
- Gói `v0.4` ban đầu là patch package — gói vá — nên changelog phân loại file dựa trên source tích lũy `v0.3.2`, không dựa trên việc file có nằm trong ZIP hay không.
- Chưa thể xác nhận compile/runtime thật trong Unity ở lần tái dựng changelog này.
