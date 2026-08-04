# Midnight Chaos v0.8.6 — Muck Animator First-Person Melee

Baseline: `v0.8.4 — Rotation-Driven First-Person Melee`

`v0.8.5` không được dùng làm baseline; đó là quỹ đạo suy luận từ video và đã
được tạm bỏ trước phiên bản này.

## Mục tiêu

Ngừng tự nội suy Position/Rotation bằng C# cho vũ khí góc nhìn thứ nhất. Dùng
trực tiếp `Cube.controller` cùng `Attack_1`, `Attack2`, `Attack3` và các clip phụ
được cung cấp từ project Muck dựng lại.

## Thay đổi chính

- `DiagnosticFirstPersonAttackAnimator` chỉ gọi `Animator.SetFloat` và
  `Animator.Play`; không còn ghi Transform mỗi frame.
- Tạo đúng hierarchy mà clip yêu cầu:
  `FirstPersonViewmodelRoot/WeaponPos/Cube/GameObject/Trail` và
  `FirstPersonViewmodelRoot/Hitbox/Cube`.
- Dùng adapter rotation để rotation nền của clip Muck không phá Rest Pose đã
  duyệt của Midnight Chaos:
  - Position `(0.45, -0.35, 0.65)`
  - Euler `(0, 100, 9.5)`
  - Scale `(0.6, 0.6, 0.6)`
- Host chọn ngẫu nhiên `Attack1`, `Attack2`, `Attack3`; cho phép lặp lại giống
  lệnh `Random.Range(1, 4)` quan sát được trong mã Muck dựng lại.
- Host giải quyết Sword hit/harvest tại `0.2666667 / AttackSpeed`.
- Animation Event `UseHitbox` được nhận bởi relay visual nhưng không có quyền
  gây damage. Host authority vẫn giữ nguyên.
- Unarmed giữ Impact `0.10825 / AttackSpeed` và chỉ một variant; không bị đổi
  gameplay chỉ vì tích hợp visual Sword.
- `ProtocolVersion: 9 → 10` do hợp đồng thời điểm Sword Impact thay đổi.
- Motion Set v0.8.x vẫn nằm trong project để tương thích dữ liệu cũ nhưng không
  còn điều khiển first-person runtime.

## Asset được thêm

`Assets/MidnightChaos/Animation/MuckFirstPerson/`

- `Cube.controller`
- `Attack_1.anim`, `Attack2.anim`, `Attack3.anim`
- `Idle_3.anim`, `Equip.anim`, `Eat_0.anim`
- `Charge_0.anim`, `ChargeHold_0.anim`, `Shoot.anim`
- Toàn bộ `.meta` cần thiết để giữ GUID mà controller tham chiếu.

## File code thay đổi

- `Editor/DiagnosticFirstPersonAttackMotionSetEditor.cs`
- `Editor/MidnightChaosBootstrapBuilder.cs`
- `Runtime/Combat/DiagnosticMeleeAttackProfile.cs`
- `Runtime/Combat/DiagnosticMeleeCombat.cs`
- `Runtime/Equipment/DiagnosticPlayerEquipment.cs`
- `Runtime/Networking/LanSessionController.cs`
- `Runtime/Player/DiagnosticFirstPersonAttackAnimator.cs`

## File code mới

- `Runtime/Player/DiagnosticFirstPersonAnimationEventRelay.cs`

## Migration

Sau khi Unity import asset và compile sạch lỗi đỏ, chạy đúng:

`Midnight Chaos → Bootstrap → Upgrade Muck First-Person Melee to v0.8.6`

Không chạy migration v0.8.4. Không cần chạy `Create or Refresh LAN Test Scene`
đối với prefab/scene đã có.

Gói patch không ghi đè prefab, Attack Profile hoặc scene đã sinh sẵn để tránh
mất model, damage và tuning riêng trong project thật. Migration cập nhật đúng
controller, timing và profile reference tại chỗ; `LanSessionController` ép
Protocol 10 trước khi mở session.

## Test bắt buộc trong Unity

1. Craft Sword, xác nhận Equip chạy và Rest Pose không đổi đáng kể.
2. Đánh nhiều lần, xác nhận thấy đủ ba variant và không có `Missing Motion`.
3. Console không có lỗi thiếu `UseHitbox`, `WeaponPos/Cube`, `Hitbox/Cube` hoặc
   `Trail` binding.
4. Host đánh enemy/tree; damage/harvest xảy ra gần đúng frame event của clip.
5. Build lại cả Host và Client rồi test LAN; build protocol cũ phải bị từ chối.

## Giới hạn chưa được tuyên bố là đã giải quyết

- Chưa compile hoặc chạy Unity 6000.0.69f1 trong môi trường đóng gói này.
- Mesh/pivot của kiếm Midnight Chaos khác asset Muck; adapter giữ Rest Pose
  nhưng cảm giác runtime vẫn cần video đối chiếu trước khi nói “giống 100%”.
- Trail dùng material của kiếm và thông số chẩn đoán; không có material/trail
  prefab gốc của Muck để sao chép chính xác.
- Hit detection vẫn là cone check của Host, không phải collider lưỡi kiếm.
