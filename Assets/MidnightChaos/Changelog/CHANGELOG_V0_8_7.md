# Midnight Chaos v0.8.7 — Scriptable Rest Pose and Motion Set Cleanup

Baseline: `v0.8.6 — Muck Animator First-Person Melee`

## Mục tiêu

- Cho phép chỉnh First-Person Rest Pose trực tiếp trong
  `SwordAttackProfile.asset`.
- Xóa toàn bộ hệ Motion Set tự tạo không còn được runtime đọc từ v0.8.6.
- Giữ nguyên Animator Clip của Muck, Host authority, damage, timing và Protocol.

## Thay đổi chính

- `DiagnosticMeleeAttackProfile` chứa:
  - `First Person Rest Local Position`
  - `First Person Rest Local Euler Angles`
  - `First Person Rest Local Scale`
- `DiagnosticPlayerEquipment` đọc pose từ Sword profile khi tạo viewmodel.
- Chỉnh Rest Pose trong `SwordAttackProfile.asset` khi đang Play sẽ cập nhật
  viewmodel ngay; code chỉ ghi `WeaponPos`/adapter khi giá trị asset đổi, không
  ghi đè Transform `Cube` do Animator điều khiển mỗi frame.
- Bỏ `firstPersonMotionSet` khỏi Attack Profile.
- Bỏ ba trường Rest Pose cũ khỏi `DiagnosticPlayerEquipment`.
- Migration sao chép Rest Pose từ `SwordFirstPersonAttackMotionSet.asset` sang
  `SwordAttackProfile.asset`, sau đó xóa ba Motion Set và hai source file cũ.
- `ProtocolVersion` giữ nguyên `10` vì không thay đổi gameplay hoặc network
  contract.

## File runtime/editor thay đổi

- `Runtime/Combat/DiagnosticMeleeAttackProfile.cs`
- `Runtime/Equipment/DiagnosticPlayerEquipment.cs`
- `Editor/MidnightChaosBootstrapBuilder.cs`

## File bị migration xóa

- `Generated/Settings/DiagnosticFirstPersonAttackMotionSet.asset`
- `Generated/Settings/UnarmedFirstPersonAttackMotionSet.asset`
- `Generated/Settings/SwordFirstPersonAttackMotionSet.asset`
- `Runtime/Combat/DiagnosticFirstPersonAttackMotionSet.cs`
- `Editor/DiagnosticFirstPersonAttackMotionSetEditor.cs`

Unity tự xóa `.meta` đi kèm thông qua `AssetDatabase.DeleteAsset`.

## Migration

Sau khi giải nén và Unity compile sạch lỗi đỏ, chạy đúng một lần:

`Midnight Chaos → Bootstrap → Migrate Rest Pose and Cleanup to v0.8.7`

Không chạy migration v0.8.4, v0.8.6 hoặc `Create or Refresh LAN Test Scene`.

## Test bắt buộc

1. Settings chỉ còn đúng ba ScriptableObject: Combat Settings, Sword Profile,
   Unarmed Profile.
2. Sword Profile giữ Rest Pose cũ và có thể chỉnh Position/Rotation/Scale.
3. Trong Play Mode, sửa Rest Pose trên Sword Profile làm kiếm cập nhật ngay.
4. `Idle`, `Equip`, `Attack1`, `Attack2`, `Attack3` vẫn chạy, không có Missing
   Motion hoặc lỗi binding.
5. Host/Client Protocol 10 vẫn kết nối; damage/harvest và Impact timing không
   đổi so với v0.8.6.

## Giới hạn xác minh

- Chỉ kiểm tra tĩnh trong môi trường đóng gói.
- Chưa compile, import, chạy migration hoặc test runtime bằng Unity 6000.0.69f1.
