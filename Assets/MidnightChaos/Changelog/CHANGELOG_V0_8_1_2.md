# CHANGELOG V0.8.1.2 — Attack Restart Reliability and Input Buffer

> Changelog này được tái dựng từ source đầy đủ `v0.8.1.2`, diff với trạng thái `v0.8.1.1` và phần bàn giao còn lưu trong lịch sử trò chuyện.

## Phần 1 — File

Tổng thay đổi: **4 file**

File ghi đè — **3**:

- `Assets/MidnightChaos/Runtime/Combat/DiagnosticMeleeCombat.cs`
- `Assets/MidnightChaos/Runtime/Player/DiagnosticPlayerAnimation.cs`
- `BOOTSTRAP_SETUP.txt`

File mới — **1**:

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_8_1_2.md`

File xóa: **0**

### Thay đổi chính

- Sửa lỗi restart UpperBody Attack đọc `normalizedTime` của lần đánh cũ và blend out ngay.
- Sau `Animator.Play`, code bỏ qua frame restart và chờ state `Attack` thực sự xuất hiện lại ở thời gian nhỏ hơn exit threshold rồi mới cho phép kiểm tra kết thúc.
- Thêm input buffer phía Host, mặc định `0.15 s` ở cuối cooldown.
- Nếu request đến khi cooldown còn không quá buffer, Host lưu tối đa một intent đánh.
- Spam trong cooldown không tạo queue nhiều đòn và không vượt cooldown authoritative `0.65 s`.
- Buffer bị xóa khi Player chết, despawn hoặc một attack khác được chấp nhận.
- Tách `ExecuteAcceptedAttackServer` để request trực tiếp và buffered request dùng cùng một đường commit.
- Không đổi RPC hoặc `NetworkVariable`; `NetworkConfig.ProtocolVersion` giữ nguyên `6`.

## Phần 2 — Thiết lập Unity và kiểm thử

### Thiết lập

1. Thoát Play Mode.
2. Chép `Assets/MidnightChaos` vào project và ghi đè.
3. Chờ Unity compile không còn lỗi đỏ.
4. **Không chạy lại Bootstrap Builder** cho patch này.
5. Trong `DiagnosticMeleeCombat`, kiểm tra:
   - `Cooldown Seconds = 0.65`
   - `Input Buffer Seconds = 0.15`
6. Trong `DiagnosticPlayerAnimation`, kiểm tra:
   - `Attack Blend In Seconds = 0.08`
   - `Attack Exit Normalized Time = 0.95`
   - `Attack Blend Out Seconds = 0.10`
7. Build lại Client vì runtime scripts đã đổi.

### Test bắt buộc

- Chờ ít nhất một giây rồi nhấn `F` đúng một lần: animation phải phát đầy đủ, không tắt ngay.
- Nhấn attack khoảng `0.10 s` trước khi cooldown hết: đúng một đòn buffer phát khi cooldown kết thúc.
- Spam ở đầu cooldown: không thêm đòn và không tăng attack rate.
- Spam ở cuối cooldown: chỉ giữ tối đa một buffered attack.
- Chết trong lúc có buffer: không phát đòn sau khi chết.
- Lặp lại trên Host và joined Client.
- Sword visual, UpperBody blend và các Gate cũ không hồi quy.

## Phần 3 — Nói thêm

- Nợ kiến trúc đã biết: `cooldownSeconds`, `inputBufferSeconds`, damage/reach/angle vẫn là field chỉnh trực tiếp trong `DiagnosticMeleeCombat`; yêu cầu đưa tuning vào `ScriptableObject` được thực hiện ở `v0.8.2`.
- Buffer cải thiện khả năng nhận input nhưng không tăng tốc độ đánh tối đa.
- Theo log bàn giao gốc, toàn bộ 26 file C# của gói đã qua kiểm tra cú pháp tĩnh; runtime Unity thực tế khi đó vẫn chưa được xác nhận bởi phía tạo gói.
