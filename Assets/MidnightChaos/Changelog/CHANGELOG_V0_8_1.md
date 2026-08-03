# CHANGELOG V0.8.1 — Networked Player Animation Foundation

> Changelog này được tái dựng từ gói vá `v0.8.1`. Không có ZIP `v0.8` trong bộ file được cung cấp, vì vậy danh sách thay đổi bên dưới là diff tổng hợp từ mốc gần nhất có bằng chứng là `v0.7`; không thể tách phần nào từng thuộc một bản `v0.8` không được cung cấp.

## Phần 1 — File

Tổng thay đổi xác định được: **6 file**

File ghi đè — **4**:

- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticMeleeCombat.cs`
- `Assets/MidnightChaos/Runtime/Player/DiagnosticCameraFollow.cs`
- `Assets/MidnightChaos/Runtime/Player/DiagnosticNetworkPlayer.cs`

File mới — **2**:

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_8_1.md`
- `Assets/MidnightChaos/Runtime/Player/DiagnosticPlayerAnimation.cs`

File xóa: **0** được quan sát giữa hai mốc source có sẵn.

### Thay đổi chính

- Thêm Gate H1: nền Player model và animation được điều khiển bằng code.
- Player prefab root đổi từ primitive capsule thành `PlayerNetworkRoot` tách biệt gameplay root với `PlayerVisual`.
- Thêm `CameraAnchor` để camera lấy vị trí từ anchor nhưng yaw vẫn xoay network root.
- Builder tạo hoặc bảo toàn subtree `PlayerVisual` hiện có khi refresh prefab.
- `PlayerVisual` có `Animator`, `applyRootMotion = false`, `AlwaysAnimate` và chỗ chứa Armature/mesh.
- Thêm locomotion state: `Idle`, `Run`, `Sprint`, `Jump`, `Fall`, `Land`.
- Owner tự xác định locomotion từ tốc độ, grounded và vertical velocity; state byte được gửi lên Server và replicate cho peer khác.
- Animator Base Layer được phát/crossfade bằng code, không phụ thuộc transition graph để chọn state.
- UpperBody `Attack` phát khi `DiagnosticMeleeCombat.AttackAccepted` được replicate.
- Model local mặc định bị ẩn; nhấn `F8` để hiện/ẩn khi debug. Remote Player vẫn hiển thị model.
- `DiagnosticNetworkPlayer` công khai `PlanarSpeed`, `IsSprinting`, `IsGrounded`, `VerticalVelocity`, `IsAlive` cho animation.
- `DiagnosticCameraFollow` hỗ trợ rotation target và position anchor riêng.
- `NetworkConfig.ProtocolVersion`: `5 → 6` do thêm `NetworkBehaviour` và locomotion state đồng bộ.

## Phần 2 — Thiết lập Unity và kiểm thử

### Thiết lập

1. Tạo Git commit/bản sao prefab trước khi cập nhật.
2. Chép gói vá lên project và ghi đè.
3. Chờ Unity compile không còn lỗi đỏ.
4. Chạy lại `Create or Refresh LAN Test Scene` để thêm `PlayerVisual`, `CameraAnchor` và `DiagnosticPlayerAnimation`.
5. Trong `DiagnosticNetworkPlayer.prefab`, gắn model/Animator Controller vào `PlayerVisual`.
6. Animator phải có đúng:
   - Base Layer: `Idle`, `Run_F`, `Sprint_F`, `Jump`, `InAir_Fall`, `Land`.
   - Layer `UpperBody`: state `Attack`.
7. Tắt root motion trên Animator/clip nếu asset nguồn bật nó.
8. Build lại mọi Client; build Protocol 5 không được chơi lẫn.

### Test bắt buộc

- Idle/Run/Sprint/Jump/Fall/Land đổi đúng trên owner và remote peer.
- Camera đứng tại `CameraAnchor` và FPS movement của `v0.7` không đổi.
- Nhấn attack: UpperBody `Attack` chạy trên mọi peer nhận `attackSequence`.
- Model local ẩn mặc định; `F8` bật debug full body và nhấn lại để ẩn.
- Remote Player vẫn nhìn thấy model dù owner đang ẩn model local.
- Animator thiếu layer/state phải báo lỗi cấu hình rõ, không làm hỏng networking.
- Combat, craft, AI, evolution và reconnect không hồi quy.

## Phần 3 — Nói thêm

- Do thiếu artifact `v0.8`, changelog này không khẳng định mọi thay đổi trên đều bắt đầu chính xác ở `v0.8.1`; nó chỉ khẳng định chúng tồn tại trong patch và khác `v0.7`.
- `v0.8.1` chưa xử lý đầy đủ world sword/first-person sword và attack layer blend; phần đó được bổ sung ở `v0.8.1.1`.
- Locomotion byte do owner gửi lên mới chỉ kiểm tra phạm vi enum, chưa xác thực chặt trạng thái vật lý.
- Chưa thể xác nhận compile/runtime thật trong Unity ở lần tái dựng changelog này.
