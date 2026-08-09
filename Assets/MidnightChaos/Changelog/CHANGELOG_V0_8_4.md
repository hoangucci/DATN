# Midnight Chaos v0.8.4 — Rotation-Driven First-Person Melee

Baseline: `v0.8.3 — Melee Motion Feel & Hit Timing`

## Mục tiêu

Thay chuyển động kiếm thiên về tịnh tiến bằng một cú vung xoay quanh vùng cầm,
đọc được rõ ở 30 FPS và sử dụng đúng Rest Pose từ video tham chiếu.

## Thay đổi chính

- Đặt Sword Rest Pose mặc định:
  - Position: `(0.45, -0.35, 0.65)`
  - Euler: `(0, 100, 9.5)`
  - Scale: `(0.6, 0.6, 0.6)`
- Thay endpoint tuyệt đối của `v0.8.3` bằng position offset tương đối so với
  Rest Pose.
- Tạo hierarchy runtime:
  `AttackPivot → RestPosePivot → FirstPersonSwordVisual`.
- `AttackPivot` áp dụng swing trong trục camera; `RestPosePivot` giữ hướng gốc
  của mesh. Rotation tổng trở thành `Swing × Rest`.
- Sửa nguyên nhân Rest Y = `100°` khiến Z rotation cũ gần như không đọc được.
- Chỉ giữ một motion mặc định: `Reference Right-Hand Slash`.
- Tăng tổng motion `0.300 → 0.560 giây` ở Attack Speed x1.
- Chuyển Impact `0.10825 → 0.270 giây` ở Attack Speed x1.
- Chỉ áp dụng timing mới cho Sword; Unarmed giữ tổng `0.300 giây` và Impact
  `0.10825 giây` để tránh tạo input delay không có animation tương ứng.
- Thêm depth offset ở Strike/Impact để kiếm tiến gần camera thay vì chỉ chạy
  ngang trên mặt phẳng màn hình.
- Runtime tiếp tục đọc Rest Pose mỗi idle frame và đọc swing data trong từng
  animation frame.
- Thêm Custom Inspector cảnh báo khi Motion Set không được Attack Profile nào
  tham chiếu.
- Thêm menu `Midnight Chaos/Combat/Select Active Sword Motion Set`.
- Migration chọn sẵn đúng `SwordFirstPersonAttackMotionSet.asset` sau khi chạy.
- `ProtocolVersion: 8 → 9` vì Host authoritative Impact delay đã thay đổi.

## Timing mặc định

| Pha | Attack Speed x1 |
|---|---:|
| Wind-up | `0.120 s` |
| Strike đến Impact | `0.150 s` |
| Follow-through | `0.090 s` |
| Recovery | `0.200 s` |
| Tổng | `0.560 s` |
| Impact | `0.270 s` |

Ở Attack Speed x2: tổng `0.280 s`, Impact `0.135 s`.

## File ghi đè so với v0.8.3

- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticFirstPersonAttackMotionSet.cs`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticMeleeCombat.cs`
- `Assets/MidnightChaos/Runtime/Equipment/DiagnosticPlayerEquipment.cs`
- `Assets/MidnightChaos/Runtime/Networking/LanSessionController.cs`
- `Assets/MidnightChaos/Runtime/Player/DiagnosticFirstPersonAttackAnimator.cs`
- `BOOTSTRAP_SETUP.txt`

## File mới

- `Assets/MidnightChaos/Editor/DiagnosticFirstPersonAttackMotionSetEditor.cs`
- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_8_4.md`

## Migration bắt buộc

Sau khi Unity compile sạch lỗi đỏ, chạy đúng:

`Midnight Chaos → Bootstrap → Upgrade Rotation-Driven Melee to v0.8.4`

Không chạy `Create or Refresh LAN Test Scene`.

## Ảnh hưởng mạng

- Không thêm RPC.
- Không thêm `NetworkVariable`.
- Không đổi cấu trúc `AttackPresentationSnapshot`.
- Tăng protocol để ngăn build cũ kết nối với hit timing mới.

## Giới hạn

- Hit detection vẫn là `cone check — kiểm tra hình nón`, chưa phải sweep collider
  theo mesh kiếm.
- Chuyển động chỉ xoay đúng quanh tay cầm nếu origin của visual nằm tại grip.
- Third-person animation không thay đổi.
- Chỉ có một motion mặc định để cô lập và đánh giá chất lượng chuyển động.

## Xác minh

Có thể kiểm tra tĩnh source, diff, timing, số RPC/NetworkVariable và archive.
Chưa thể xác nhận compile hoặc runtime trong Unity ở môi trường tạo gói.
