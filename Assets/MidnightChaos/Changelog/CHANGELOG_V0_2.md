# CHANGELOG V0.2 — Host-Validated PvP Melee

> Changelog này được tái dựng bằng cách so sánh `v0.2` với `v0.1.1` và đối chiếu tài liệu thiết lập trong ZIP.

## Phần 1 — File

Tổng thay đổi: **8 file**

File ghi đè — **4**:

- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`
- `Assets/MidnightChaos/Runtime/Player/DiagnosticNetworkPlayer.cs`
- `Assets/MidnightChaos/Runtime/UI/DiagnosticLanUI.cs`
- `BOOTSTRAP_SETUP.txt`

File mới — **4**:

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_2.md`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticMeleeCombat.cs`
- `Assets/MidnightChaos/Runtime/Combat/NetworkHealth.cs`
- `Assets/MidnightChaos/Runtime/UI/DiagnosticWorldHealthLabel.cs`

File xóa: **0**

### Thay đổi chính

- Thêm Gate B: PvP melee do Host xác thực.
- Client chỉ gửi yêu cầu đánh; không gửi target ID hoặc damage.
- Host kiểm tra người đánh còn sống, cooldown, khoảng cách và góc trước khi áp dụng damage.
- Tầm đánh mặc định `2.6 m`, nửa góc đánh `65°`, damage `25`, cooldown `0.65 s`.
- Mỗi đòn hợp lệ làm tăng `attackSequence`; mọi peer hiển thị attack indicator màu cam.
- Thêm `NetworkHealth`: Player có `100 HP`, health chỉ do Server ghi và được đồng bộ cho mọi peer.
- Player ở `0 HP` đổi màu đỏ, không thể di chuyển hoặc tiếp tục đánh.
- Thêm world-space health label chẩn đoán.
- Builder cập nhật prefab Player và scene thử nghiệm cho combat.
- `NetworkConfig.ProtocolVersion` vẫn là `1` theo source lịch sử.

## Phần 2 — Thiết lập Unity và kiểm thử

### Thiết lập

1. Chép `Assets/MidnightChaos` vào `Assets` và ghi đè.
2. Chờ Unity compile không còn lỗi đỏ.
3. Chạy `Create or Refresh LAN Test Scene`.
4. Mở scene `LAN_Bootstrap.unity` mới tạo.
5. Build lại Client vì prefab Player và runtime scripts đã đổi.

### Test bắt buộc

- Host và Client cùng thấy indicator khi một đòn được Host chấp nhận.
- Một hit hợp lệ giảm đúng `25 HP` trên cả hai máy.
- Target ngoài `2.6 m` không nhận damage.
- Target ở phía sau người đánh không nhận damage.
- Spam input không vượt cooldown `0.65 s`.
- Player về `0 HP` không còn di chuyển hoặc đánh.
- Gate A: kết nối, di chuyển, disconnect/reconnect vẫn hoạt động.

## Phần 3 — Nói thêm

- Đây là proof — bản chứng minh kỹ thuật — cho quyền quyết định của Host, chưa phải weapon/hitbox/animation cuối.
- Chưa có lag compensation, line of sight, armor, stamina, knockback, invulnerability, respawn hoặc death flow hoàn chỉnh.
- Source diff xác nhận 7 đường dẫn gốc thay đổi; changelog là file mới thứ 8.
- Chưa thể xác nhận compile/runtime thật trong Unity ở lần tái dựng changelog này.
