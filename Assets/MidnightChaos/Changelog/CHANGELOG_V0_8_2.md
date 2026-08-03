# CHANGELOG V0.8.2 — Data-Driven Held Attack and Code-Driven Viewmodel Motion

> Changelog này được tái dựng từ source đầy đủ `v0.8.2`, diff với `v0.8.1.2`, tài liệu migration và kế hoạch/bàn giao còn lưu trong lịch sử trò chuyện.

## Phần 1 — File

Tổng thay đổi: **11 file**

File ghi đè — **6**:

- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticMeleeCombat.cs`
- `Assets/MidnightChaos/Runtime/Equipment/DiagnosticPlayerEquipment.cs`
- `Assets/MidnightChaos/Runtime/Networking/LanSessionController.cs`
- `Assets/MidnightChaos/Runtime/Player/DiagnosticPlayerAnimation.cs`
- `BOOTSTRAP_SETUP.txt`

File mới — **5**:

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_8_2.md`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticFirstPersonAttackMotionSet.cs`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticMeleeAttackProfile.cs`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticMeleeCombatSettings.cs`
- `Assets/MidnightChaos/Runtime/Player/DiagnosticFirstPersonAttackAnimator.cs`

File xóa: **0**

### Thay đổi chính

- Chuyển combat tuning ra khỏi `DiagnosticMeleeCombat` sang `ScriptableObject`.
- `DiagnosticMeleeCombatSettings` chứa input buffer, minimum interval, giới hạn attack speed, indicator và tuning animation third-person.
- `DiagnosticMeleeAttackProfile` chứa damage, reach, angle, base interval và motion set theo loại vũ khí.
- Migration tạo `UnarmedAttackProfile.asset`, `SwordAttackProfile.asset`, `DiagnosticMeleeCombatSettings.asset` và `DiagnosticFirstPersonAttackMotionSet.asset`.
- Legacy values từ `v0.8.1.2` được giữ trong field ẩn để migration lần đầu không âm thầm thay tuning cũ.
- Attack interval chỉ có một công thức:

  `max(MinimumAttackInterval, BaseAttackInterval / AttackSpeedMultiplier)`

- Attack Speed Multiplier là state runtime riêng từng Player, chỉ Host thay đổi; code không sửa asset dùng chung.
- Thêm API Server: `TrySetAttackSpeedMultiplierServer(multiplier)`.
- Giữ `F` hoặc chuột trái để tự động đánh. Owner chỉ gửi thay đổi held/released; Host tự lặp theo interval, không gửi RPC mỗi frame.
- Hỗ trợ giữ đồng thời hai input; thả một nút không dừng cho đến khi cả hai đã được thả.
- Mỗi attack được Host chấp nhận replicate snapshot gồm profile, interval, speed multiplier và motion index để peer không tự đoán presentation.
- Host chọn ngẫu nhiên một trong bốn motion chéo và không lặp ngay motion trước:
  1. Trên trái → dưới phải.
  2. Dưới phải → trên trái.
  3. Dưới trái → trên phải.
  4. Trên phải → dưới trái.
- Thêm hierarchy runtime `FirstPersonViewmodelRoot/AttackPivot/FirstPersonSwordVisual`.
- `DiagnosticFirstPersonAttackAnimator` điều khiển `AttackPivot` bằng pose tuyệt đối, tránh transform lệch dần sau nhiều đòn.
- First-person motion và UpperBody animation dùng cùng accepted interval/speed nên tăng attack speed đồng bộ với gameplay.
- Thêm migration riêng sửa prefab hiện tại, bảo toàn `PlayerVisual`, model, armature và world sword.
- `NetworkConfig.ProtocolVersion`: `6 → 7` vì RPC/layout dữ liệu mạng thay đổi.

## Phần 2 — Thiết lập Unity và kiểm thử

### Thiết lập

1. Thoát Play Mode và tạo Git commit/bản sao lưu.
2. Chép `Assets/MidnightChaos` vào project và ghi đè.
3. Chờ Unity compile; không tiếp tục nếu còn lỗi đỏ.
4. Chạy đúng menu:

   `Midnight Chaos > Bootstrap > Migrate Combat to v0.8.2`

5. **Không chạy** `Create or Refresh LAN Test Scene`; lệnh đó có thể dựng lại scene/prefab thay vì chỉ migrate prefab đang dùng.
6. Mở `DiagnosticNetworkPlayer.prefab`, xác nhận root có:
   - `DiagnosticMeleeCombat` với ba reference Settings/Unarmed/Sword.
   - `DiagnosticFirstPersonAttackAnimator`.
   - `DiagnosticPlayerEquipment` vẫn trỏ đúng World Sword Visual.
7. Kiểm tra bốn asset trong `Assets/MidnightChaos/Generated/Settings/`.
8. Build lại toàn bộ Client; bản Protocol 6 không được chơi lẫn.

### Test bắt buộc

- Nhấn `F` một lần: đúng một attack và một animation.
- Giữ `F` 3 giây: attack lặp đều đúng interval profile.
- Giữ chuột trái khi cursor locked: kết quả giống `F`.
- Thả giữa cooldown: không sinh thêm attack ngoài tối đa một buffer hợp lệ.
- Giữ cả `F` và chuột, thả từng nút: chỉ dừng khi cả hai đã thả.
- Chết khi đang giữ: Host xóa held/buffered state.
- Craft Sword khi đang giữ: attack kế tiếp dùng Sword profile.
- Quan sát đủ bốn motion và không có hai motion giống nhau liên tiếp.
- Host và joined Client đều chạy đúng presentation từ accepted snapshot.
- Đặt attack speed `x2` từ Server: damage cadence, first-person motion và UpperBody animation cùng nhanh gấp đôi.
- Mỗi attack chỉ commit damage hoặc harvest đúng một lần.

## Phần 3 — Nói thêm

- Lỗi đã biết của `v0.8.2`, sửa ở `v0.8.2.1`: bốn `Start/End Position` được code hiểu là offset cộng vào Rest Position ở góc dưới phải. Kết quả cả bốn đường vung thực tế co lại ở vùng dưới phải thay vì cắt qua màn hình.
- `v0.8.2` sao chép motion đang active khi attack bắt đầu; chỉnh asset giữa chính cú vung không phản ánh ngay. Tay không và Sword cũng dùng chung một motion set. Cả hai điểm được sửa ở `v0.8.2.1`.
- Damage/harvest vẫn commit ngay lúc Host chấp nhận attack, chưa căn theo frame kiếm chạm mục tiêu.
- Chưa có model bàn tay first-person cho unarmed attack.
- Theo log bàn giao gốc, 30 script C# đã qua kiểm tra cú pháp tĩnh và ZIP đã kiểm tra toàn vẹn; compile/runtime Unity thật vẫn chưa được xác nhận bởi phía tạo gói.
