# CHANGELOG V0.1 — LAN Bootstrap

> Changelog này được tái dựng từ source `v0.1` và tài liệu `BOOTSTRAP_SETUP.txt`. Đây không phải changelog được viết tại thời điểm phát hành ban đầu.

## Phần 1 — File

Tổng thay đổi: **12 file**

File ghi đè: **0**

File mới — **12**:

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_1.md`
- `Assets/MidnightChaos/Editor/MidnightChaos.Editor.asmdef`
- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`
- `Assets/MidnightChaos/Runtime/MidnightChaos.Runtime.asmdef`
- `Assets/MidnightChaos/Runtime/Networking/LanEndpointValidator.cs`
- `Assets/MidnightChaos/Runtime/Networking/LanSessionController.cs`
- `Assets/MidnightChaos/Runtime/Networking/ValidatedOwnerNetworkTransform.cs`
- `Assets/MidnightChaos/Runtime/Player/DiagnosticCameraFollow.cs`
- `Assets/MidnightChaos/Runtime/Player/DiagnosticNetworkPlayer.cs`
- `Assets/MidnightChaos/Runtime/UI/DiagnosticLanUI.cs`
- `BOOTSTRAP_SETUP.txt`
- `Packages/Required_Packages.json`

File xóa: **0**

### Thay đổi chính

- Tạo Gate A để kiểm chứng kết nối LAN trực tiếp bằng IPv4 và UDP, cổng mặc định `7777`.
- Thêm ba chế độ khởi chạy: Single Player, Host LAN và Join LAN.
- Host phê duyệt kết nối, giới hạn tối đa 8 người và tạo vị trí spawn tách nhau.
- Mỗi máy chỉ điều khiển Player do chính máy đó sở hữu.
- Thêm di chuyển `WASD`, chạy nhanh bằng `Left Shift`, nhảy bằng `Space` và trọng lực.
- Đồng bộ vị trí/xoay bằng `ValidatedOwnerNetworkTransform` với kiểm tra tốc độ và khóa scale do client gửi lên.
- Thêm camera theo Player local và UI chẩn đoán bằng `OnGUI`.
- Kiểm tra IPv4/port trước khi gửi yêu cầu kết nối.
- Cho phép disconnect rồi tạo hoặc tham gia session mới mà không cần khởi động lại game.
- Thêm Builder tạo `DiagnosticNetworkPlayer.prefab`, scene `LAN_Bootstrap.unity` và tự đưa scene vào Build Settings.
- `NetworkConfig.ProtocolVersion = 1`.

## Phần 2 — Thiết lập Unity và kiểm thử

### Thiết lập

1. Dùng Unity `6000.0.69f1`, project Universal 3D — URP 17.
2. Cài `Netcode for GameObjects 2.7.0` và `Input System 1.14.2`.
3. Đặt Active Input Handling thành `Input System Package (New)`.
4. Chép `Assets/MidnightChaos` vào thư mục `Assets` của project.
5. Chờ Unity compile không còn lỗi đỏ.
6. Chạy:

   `Midnight Chaos > Bootstrap > Create or Refresh LAN Test Scene`

7. Mở `Assets/MidnightChaos/Generated/Scenes/LAN_Bootstrap.unity` và bảo đảm scene được bật trong Build Profiles.
8. Build Client Windows và cho phép ứng dụng qua Windows Firewall ở mạng Private.

### Test bắt buộc

- Host và Client kết nối bằng IPv4 LAN, mỗi kết nối sinh đúng một Player.
- Mỗi máy chỉ điều khiển capsule của chính mình.
- Hai máy nhìn thấy chuyển động của nhau.
- `WASD`, `Left Shift` và `Space` hoạt động trên Player owner.
- IPv4 hoặc port không hợp lệ không được bắt đầu yêu cầu mạng.
- Người thứ 9 bị từ chối khi `maxPlayers = 8`.
- Client disconnect rồi reconnect được.
- Dừng Host rồi tạo Host mới được mà không restart game.

## Phần 3 — Nói thêm

- Đây là bootstrap chẩn đoán, chưa phải controller góc nhìn thứ nhất hoặc UI sản phẩm cuối.
- Kiểm tra transform chỉ là sanity check — kiểm tra hợp lý cơ bản — không phải hệ thống chống gian lận hoàn chỉnh.
- Chưa có combat, inventory, crafting, AI, procedural map, Chaos Evolution, day/night hoặc boss flow.
- Source và cấu trúc gói đã được đối chiếu lại; chưa thể xác nhận compile/runtime thật trong Unity ở lần tái dựng changelog này.
