# CHANGELOG V0.6.1 — Evolution Death and Session Configuration Hotfix

> Changelog này được tái dựng bằng cách so sánh gói vá `v0.6.1` với trạng thái source `v0.6`.

## Phần 1 — File

Tổng thay đổi: **5 file**

File ghi đè — **4**:

- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`
- `Assets/MidnightChaos/Runtime/Enemies/DiagnosticChaosEvolutionService.cs`
- `Assets/MidnightChaos/Runtime/Enemies/DiagnosticEnemyEvolution.cs`
- `Assets/MidnightChaos/Runtime/Networking/LanSessionController.cs`

File mới — **1**:

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_6_1.md`

File xóa: **0**

### Thay đổi chính

- Thay đường commit death của Enemy Evolution: Server kiểm tra trạng thái chết trong `Update` và chỉ đánh dấu đã xử lý sau khi tìm được evolution service.
- Nếu service chưa tồn tại, death chờ và chỉ log lỗi một lần thay vì mất side effect ngay lập tức.
- Thiếu Chaos Shard prefab không còn disable toàn bộ `DiagnosticChaosEvolutionService`; charge transfer vẫn tiếp tục hoạt động.
- Alpha chết khi thiếu shard prefab ghi lỗi rõ và yêu cầu rebuild scene, không giả vờ drop thành công.
- `LanSessionController` cache lại `NetworkManager`/`UnityTransport`, đảm bảo NetworkConfig ở `OnEnable` và ngay trước khi start session.
- Thêm Player prefab dự phòng được serialize; runtime có thể khôi phục `NetworkConfig.PlayerPrefab` nếu tham chiếu bị mất.
- Luôn phục hồi `ConnectionApproval = true` trước khi chạy mạng.
- Bind callback theo kiểu tháo trước/gắn lại và unbind ở `OnDisable`/`OnDestroy`, tránh callback bị đăng ký lặp sau reconnect/re-enable.
- Builder truyền Player prefab cho session controller và đánh dấu manager, controller, spawner/service là dirty trước khi save.
- Không đổi layout mạng; `NetworkConfig.ProtocolVersion` giữ nguyên `4`.

## Phần 2 — Thiết lập Unity và kiểm thử

### Thiết lập

1. Chép gói vá lên project `v0.6` và ghi đè.
2. Chờ Unity compile không còn lỗi đỏ.
3. Bắt buộc chạy lại `Create or Refresh LAN Test Scene` để serialize Player prefab và các reference fallback.
4. Build lại Client do runtime scripts đã đổi, dù ProtocolVersion vẫn là `4`.

### Test bắt buộc

- Host sinh đúng 7 Small sau khi bắt đầu session.
- Giết Small gần đàn: charge transfer và `Small → Mature` hoạt động ổn định.
- Dừng session rồi Host lại: không có log/callback bị nhân đôi và không spawn lặp ngoài thiết kế.
- Disconnect/reconnect Client nhiều lần: connected count và status không spam sai.
- `NetworkConfig.NetworkTransport` và `PlayerPrefab` vẫn có giá trị trước khi Start Host/Client.
- Xóa tạm shard prefab khỏi service: charge transfer vẫn hoạt động; Alpha death báo lỗi drop shard thay vì disable evolution.

## Phần 3 — Nói thêm

- Bản này sửa tính bền vững của transaction death/config, không thay requirement charge, HP hoặc stage.
- Theo lịch sử dự án, hệ thống evolution hoạt động sau hotfix và lỗi spam khi re-host/reconnect đã được xử lý.
- Nếu không chạy lại Builder, reference fallback mới có thể chưa được serialize vào scene cũ.
- Chưa thể xác nhận compile/runtime thật trong Unity ở lần tái dựng changelog này.
