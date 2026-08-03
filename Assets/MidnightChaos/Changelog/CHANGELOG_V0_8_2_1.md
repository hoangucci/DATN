# CHANGELOG V0.8.2.1 — First-Person Swing Path and Live Tuning Fix

> Changelog này được tái dựng từ source đầy đủ `v0.8.2.1`, diff với `v0.8.2`, ảnh lỗi người dùng cung cấp và phần bàn giao còn lưu trong lịch sử trò chuyện.

## Phần 1 — File

Tổng thay đổi: **7 file**

File ghi đè — **6**:

- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticFirstPersonAttackMotionSet.cs`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticMeleeAttackProfile.cs`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticMeleeCombat.cs`
- `Assets/MidnightChaos/Runtime/Player/DiagnosticFirstPersonAttackAnimator.cs`
- `BOOTSTRAP_SETUP.txt`

File mới — **1**:

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_8_2_1.md`

File xóa: **0**

### Thay đổi chính

- Sửa nghĩa dữ liệu motion: start/end position là tọa độ local tuyệt đối của `AttackPivot`, không còn là offset cộng quanh Rest Position.
- Đặt lại bốn endpoint mặc định để đường kiếm thực sự đi giữa bốn vùng và cắt qua tâm camera diagnostic.
- Runtime giữ `motionIndex` thay vì sao chép toàn bộ motion khi attack bắt đầu.
- Mỗi frame, animator đọc lại motion theo index từ đúng `ScriptableObject`; chỉnh endpoint, rotation, timing hoặc curve trong Play Mode phản ánh ngay.
- Rest position/rotation/scale cũng được đọc lại khi idle nên có thể tune trực tiếp khi game đang chạy trong Editor.
- Tách motion set riêng:
  - `UnarmedFirstPersonAttackMotionSet.asset`
  - `SwordFirstPersonAttackMotionSet.asset`
- Migration giữ `DiagnosticFirstPersonAttackMotionSet.asset` cũ làm fallback, sao chép rest/timing/curve sang hai asset mới nhưng thay endpoint sai bằng tọa độ tuyệt đối.
- `UnarmedAttackProfile` và `SwordAttackProfile` được gán lại vào motion set tương ứng.
- Thêm menu migration riêng, sửa trực tiếp prefab hiện tại và không dựng lại `PlayerVisual`.
- Không đổi RPC hoặc `NetworkVariable`; `NetworkConfig.ProtocolVersion` giữ nguyên `7`.

## Phần 2 — Thiết lập Unity và kiểm thử

### Thiết lập

1. Thoát Play Mode và tạo Git commit/bản sao lưu.
2. Chép `Assets/MidnightChaos` vào project và ghi đè.
3. Chờ Unity compile; không tiếp tục nếu còn lỗi đỏ.
4. Chạy đúng menu:

   `Midnight Chaos > Bootstrap > Fix First-Person Attack v0.8.2.1`

5. **Không chạy** `Create or Refresh LAN Test Scene`.
6. Kiểm tra `UnarmedAttackProfile.asset` trỏ tới `UnarmedFirstPersonAttackMotionSet.asset`.
7. Kiểm tra `SwordAttackProfile.asset` trỏ tới `SwordFirstPersonAttackMotionSet.asset`.
8. Chỉnh animation Sword tại:

   `Assets/MidnightChaos/Generated/Settings/SwordFirstPersonAttackMotionSet.asset`

9. Build lại mọi Client do runtime scripts đổi, dù ProtocolVersion vẫn là `7`.

### Test bắt buộc

- Bốn motion đi qua bốn đường chéo đúng hướng, không chỉ quẹt ở góc dưới phải.
- Không có hai motion giống nhau liên tiếp.
- Trong Play Mode ở Host Editor, sửa Rest Position: kiếm idle dịch chuyển ngay.
- Sửa endpoint/rotation/curve của motion đang chạy: presentation cập nhật trực tiếp theo dữ liệu mới.
- Sword và unarmed profile dùng hai motion set khác nhau.
- Giữ `F`/chuột trái, input buffer, attack speed `x2`, UpperBody animation và damage cadence không hồi quy.
- Craft Sword khi đang giữ chuyển sang Sword profile ở attack tiếp theo.
- Host và Client vẫn kết nối ở Protocol 7 và nhận cùng accepted motion index.

## Phần 3 — Nói thêm

- Default mới được tune cho camera diagnostic; FOV, aspect ratio, pivot và kích thước mesh khác vẫn cần chỉnh `SwordFirstPersonAttackMotionSet`.
- Asset cũ `DiagnosticFirstPersonAttackMotionSet.asset` chỉ là fallback sau migration; chỉnh nhầm asset cũ sẽ không ảnh hưởng Sword nếu profile đã trỏ asset mới.
- Damage/harvest vẫn commit lúc Host chấp nhận attack, chưa đồng bộ hit timing với đường kiếm.
- Theo log bàn giao gốc, 30 script C# đã qua kiểm tra cú pháp tĩnh, số RPC/`NetworkVariable` không đổi và ZIP đã kiểm tra toàn vẹn; compile/runtime Unity thật vẫn cần test trong project.
