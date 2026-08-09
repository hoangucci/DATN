# CHANGELOG V0.3.1 — UnityTransport Runtime Repair

> Changelog này được tái dựng bằng cách so sánh `v0.3.1` với `v0.3` và đối chiếu phần “Hotfix v0.3.1” trong tài liệu thiết lập.

## Phần 1 — File

Tổng thay đổi: **4 file**

File ghi đè — **3**:

- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`
- `Assets/MidnightChaos/Runtime/Networking/LanSessionController.cs`
- `BOOTSTRAP_SETUP.txt`

File mới — **1**:

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_3_1.md`

File xóa: **0**

### Thay đổi chính

- Tự sửa `NetworkManager.NetworkConfig.NetworkTransport` để trỏ đúng `UnityTransport` trước khi NGO khởi tạo session.
- Builder đánh dấu `NetworkManager` và scene là dirty — đã thay đổi — trước khi save để Unity lưu tham chiếu transport.
- Sửa lỗi runtime: `[Netcode] No transport has been selected` sau khi import hoặc refresh scene.
- Không thay đổi combat, resource transaction, inventory hoặc input.
- `NetworkConfig.ProtocolVersion` giữ nguyên `1`.

## Phần 2 — Thiết lập Unity và kiểm thử

### Thiết lập

1. Chép `Assets/MidnightChaos` vào `Assets` và ghi đè.
2. Chờ Unity compile không còn lỗi đỏ.
3. Bắt buộc chạy lại `Create or Refresh LAN Test Scene` để tham chiếu transport được lưu lại.
4. Mở scene vừa tạo và build lại Client.

### Test bắt buộc

- Console không còn thông báo `No transport has been selected` khi Start Host hoặc Start Client.
- `NetworkConfig.NetworkTransport` trỏ đúng component `UnityTransport` trên `NetworkRoot`.
- Host, Join, disconnect và reconnect hoạt động sau khi refresh scene.
- PvP combat và gather bằng `E` của `v0.3` không hồi quy.

## Phần 3 — Nói thêm

- Đây là hotfix cấu hình/serialization, không phải bản tính năng.
- Source diff xác nhận chỉ Builder, `LanSessionController` và tài liệu thiết lập thay đổi.
- Chưa thể xác nhận compile/runtime thật trong Unity ở lần tái dựng changelog này.
