# CHANGELOG V0.3 — Host-Authoritative Resource Gathering

> Changelog này được tái dựng bằng cách so sánh `v0.3` với `v0.2` và đối chiếu tài liệu thiết lập trong ZIP.

## Phần 1 — File

Tổng thay đổi: **9 file**

File ghi đè — **3**:

- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`
- `Assets/MidnightChaos/Runtime/UI/DiagnosticLanUI.cs`
- `BOOTSTRAP_SETUP.txt`

File mới — **6**:

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_3.md`
- `Assets/MidnightChaos/Runtime/Inventory/DiagnosticNetworkInventory.cs`
- `Assets/MidnightChaos/Runtime/Resources/DiagnosticResourceGatherer.cs`
- `Assets/MidnightChaos/Runtime/Resources/DiagnosticResourceNode.cs`
- `Assets/MidnightChaos/Runtime/Resources/DiagnosticResourceSpawner.cs`
- `Assets/MidnightChaos/Runtime/UI/DiagnosticWorldResourceLabel.cs`

File xóa: **0**

### Thay đổi chính

- Thêm Gate C: giao dịch thu thập tài nguyên do Host quyết định.
- Người chơi nhấn `E`; client không gửi target, item ID hoặc số lượng.
- Host tự tìm Tree hợp lệ, kiểm tra range, angle và cooldown rồi mới commit kết quả.
- Tầm gather mặc định `2.8 m`, cooldown `0.45 s`.
- Mỗi Tree có 3 hit; mỗi hit hợp lệ giảm 1 hit và cộng đúng 1 Wood cho người gather.
- `DiagnosticNetworkInventory` đồng bộ Wood riêng cho từng Player, chỉ Server có quyền ghi.
- Hai người tranh hit cuối không thể cùng nhận Wood; Tree đã cạn không cấp thêm tài nguyên.
- Thêm resource prefab, resource spawner và label hiển thị số hit còn lại.
- UI chẩn đoán hiển thị Wood của Player local.
- Combat của Gate B và LAN của Gate A được giữ lại.
- `NetworkConfig.ProtocolVersion` giữ nguyên `1`.

## Phần 2 — Thiết lập Unity và kiểm thử

### Thiết lập

1. Chép `Assets/MidnightChaos` vào `Assets` và ghi đè.
2. Chờ Unity compile không còn lỗi đỏ.
3. Chạy `Create or Refresh LAN Test Scene`.
4. Mở `LAN_Bootstrap.unity` và build lại Client vì prefab/script đã đổi.

### Test bắt buộc

- Đứng gần và nhìn vào Tree, nhấn `E` một lần: Tree từ `3/3` thành `2/3`, Wood từ `0` thành `1` trên mọi peer.
- Đúng ba lần gather hợp lệ làm Tree cạn.
- Mỗi lần chỉ cộng Wood cho Player đã gather.
- Ngoài `2.8 m` hoặc quay lưng thì không gather.
- Spam `E` không vượt cooldown `0.45 s`.
- Hai client tranh hit cuối chỉ có một transaction thành công.
- Tree đã cạn không cấp thêm Wood.
- Player đã chết không gather được.
- Gate A và Gate B vẫn hoạt động.

## Phần 3 — Nói thêm

- Inventory ở bản này chỉ có một số nguyên Wood; chưa có stack, pickup, item database, save hoặc crafting.
- Tree không respawn và chưa yêu cầu tool.
- `E` vẫn là input gather riêng; việc hợp nhất attack/harvest được thực hiện ở `v0.3.2`.
- Chưa thể xác nhận compile/runtime thật trong Unity ở lần tái dựng changelog này.
