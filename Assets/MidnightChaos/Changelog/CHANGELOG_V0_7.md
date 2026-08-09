# CHANGELOG V0.7 — First-Person Camera Foundation

> Changelog này được tái dựng bằng cách phủ gói vá `v0.7` lên trạng thái source `v0.6.1` và so sánh code camera/player/combat.

## Phần 1 — File

Tổng thay đổi: **6 file**

File ghi đè — **5**:

- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticMeleeCombat.cs`
- `Assets/MidnightChaos/Runtime/Player/DiagnosticCameraFollow.cs`
- `Assets/MidnightChaos/Runtime/Player/DiagnosticNetworkPlayer.cs`
- `Assets/MidnightChaos/Runtime/UI/DiagnosticLanUI.cs`

File mới — **1**:

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_7.md`

File xóa: **0**

### Thay đổi chính

- Thêm Gate G: nền camera góc nhìn thứ nhất cho Player local.
- Camera đặt tại eye offset `(0, 0.75, 0.08)` và dùng near clip `0.05`.
- Chuột điều khiển yaw/pitch; pitch giới hạn mặc định `-80°` đến `80°`.
- Chỉ yaw xoay Player network root; pitch chỉ thuộc camera local nên không làm nghiêng `CharacterController`.
- `WASD` chuyển sang di chuyển theo `transform.right/forward`, tức theo hướng nhìn thay vì world axis.
- `Esc` thả chuột; chuột phải khóa lại; mất focus tự thả cursor.
- Bỏ qua frame delta đầu sau khi khóa chuột để tránh camera giật do cursor warp.
- Renderer thân capsule của owner được ẩn cục bộ để không nhìn thấy mặt trong; peer khác vẫn thấy Player.
- Chuột trái chỉ gửi attack khi cursor đang locked; phím `F` vẫn đánh độc lập.
- Builder cập nhật camera và mô tả Gate G.
- `NetworkConfig.ProtocolVersion`: `4 → 5` vì contract movement/rotation của owner thay đổi.

## Phần 2 — Thiết lập Unity và kiểm thử

### Thiết lập

1. Chép gói vá lên project `v0.6.1` và ghi đè.
2. Chờ Unity compile không còn lỗi đỏ.
3. Chạy lại `Create or Refresh LAN Test Scene` để camera gần/near clip mới được tạo.
4. Build lại mọi Client; `ProtocolVersion 4` không tương thích.

### Test bắt buộc

- Khi spawn, camera nằm tại đầu Player owner và cursor được khóa.
- Di chuột trái/phải xoay Player theo yaw; lên/xuống chỉ đổi pitch camera.
- `WASD` luôn đi theo hướng nhìn; `Shift` và `Space` vẫn hoạt động.
- Owner không thấy capsule của mình; peer khác vẫn thấy capsule đó.
- `Esc` thả chuột và hiện cursor; chuột phải khóa lại mà không tự đánh.
- Chuột trái khi cursor thả không attack; phím `F` vẫn gửi attack.
- Host và Client đều có camera chỉ bám Player local của mình.
- Chaos Evolution, combat, craft, harvest và reconnect không hồi quy.

## Phần 3 — Nói thêm

- Đây mới là FPS foundation — nền FPS — chưa có PlayerVisual/Animator/viewmodel hoàn chỉnh.
- Yaw vẫn do owner điều khiển và được kiểm tra ở mức transform diagnostic; chưa phải mô hình chống cheat production.
- Source diff xác nhận 5 file gốc bị sửa; changelog là file mới thứ 6.
- Chưa thể xác nhận compile/runtime thật trong Unity ở lần tái dựng changelog này.
