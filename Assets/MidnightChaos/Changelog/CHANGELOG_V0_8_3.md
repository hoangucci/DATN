# Midnight Chaos v0.8.3 — Melee Motion Feel & Hit Timing

Baseline: `v0.8.2.1`

## Thay đổi chính

- First-person attack đổi từ `Rest → Start → End → Rest` sang bốn pha độc lập:
  `Wind-up → Bézier Strike → Overshoot → Recovery`.
- Motion mặc định dài `0.300 giây` ở Attack Speed x1 và không còn bị kéo giãn
  theo toàn bộ cooldown.
- Mỗi motion có `Strike Control`, `Overshoot Position` và `Overshoot Rotation`
  chỉnh được trực tiếp bằng `ScriptableObject`.
- Mỗi pha có duration và `AnimationCurve` riêng.
- Host trì hoãn target search, damage và harvest tới `Strike Impact` thay vì
  commit ngay khi nhận input.
- Target không bị khóa sớm; Host tìm lại Health/Resource trong cone tại Impact.
- Thêm camera shake cục bộ chỉ khi Host xác nhận damage/harvest thành công.
- Enemy, Player, cây, đá và mọi `DiagnosticResourceNode` hợp lệ đều tạo hit
  feedback; đánh hụt không rung.
- Pending hit bị xóa khi Player chết hoặc despawn.
- Giữ cơ chế chọn ngẫu nhiên bốn đường chéo và không lặp motion liên tiếp.

## File thay đổi

- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticFirstPersonAttackMotionSet.cs`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticMeleeCombat.cs`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticMeleeCombatSettings.cs`
- `Assets/MidnightChaos/Runtime/Networking/LanSessionController.cs`
- `Assets/MidnightChaos/Runtime/Player/DiagnosticCameraFollow.cs`
- `Assets/MidnightChaos/Runtime/Player/DiagnosticFirstPersonAttackAnimator.cs`
- `BOOTSTRAP_SETUP.txt`

## File mới

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_8_3.md`

## Migration Unity

1. Ghi đè `Assets/MidnightChaos` từ package.
2. Chờ Unity compile không còn lỗi đỏ.
3. Chạy `Midnight Chaos/Bootstrap/Upgrade Melee Feel to v0.8.3`.
4. Không chạy `Create or Refresh LAN Test Scene` cho lần nâng cấp này.
5. Build lại Host và toàn bộ Client.

Migration giữ nguyên Rest, Start, End và rotation đã chỉnh trong hai Motion Set.
Nó chỉ tạo cấu trúc phase/control/overshoot mới một lần và không dựng lại
`PlayerVisual`.

## Network

- `ProtocolVersion`: `7 → 8`.
- Lý do: thêm `ConfirmHitRpc` gửi từ Host tới owner sau một hậu quả melee thành
  công.
- Không dùng lẫn build `v0.8.2.1` và `v0.8.3`.
- Không thêm `NetworkVariable` và không thay layout snapshot attack hiện có.

## Test bắt buộc

- Host và Client đều đánh được bằng giữ `F` hoặc chuột trái.
- Enemy/Player chỉ mất HP tại Impact và mỗi attack chỉ damage một lần.
- Cây/đá/resource chỉ harvest tại Impact và mỗi attack chỉ harvest một lần.
- Hit hợp lệ rung camera đúng owner đúng một lần.
- Miss không rung camera.
- Bước ra khỏi cone trước Impact thì tránh được đòn; bước vào trước Impact có
  thể bị trúng.
- Attack Speed x2 co cooldown, motion và hit delay đồng bộ.
- Chết trong Wind-up hủy pending hit.
- Client protocol 7 bị Host protocol 8 từ chối.

## Giới hạn đã biết

- Timing khớp pha kiếm nhưng vùng trúng vẫn là cone check, chưa phải swept blade
  collider — collider lưỡi kiếm quét theo quỹ đạo.
- Độ trễ mạng cao có thể làm hit feedback trên Client lệch vài frame so với
  hình ảnh; Host vẫn quyết định gameplay.
- Upper-body third-person animation chưa được tái cấu trúc theo bốn pha mới.
- Cần compile và test runtime trong Unity `6000.0.69f1`; kiểm tra tĩnh bên ngoài
  Unity không thay thế được bước này.
