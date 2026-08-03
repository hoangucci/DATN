# CHANGELOG V0.5 — Host-Authoritative Melee Enemy

> Changelog này được tái dựng bằng cách phủ gói vá `v0.5` lên trạng thái source `v0.4` và so sánh toàn bộ code liên quan.

## Phần 1 — File

Tổng thay đổi: **8 file**

File ghi đè — **5**:

- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticMeleeCombat.cs`
- `Assets/MidnightChaos/Runtime/Combat/NetworkHealth.cs`
- `Assets/MidnightChaos/Runtime/UI/DiagnosticLanUI.cs`
- `Assets/MidnightChaos/Runtime/UI/DiagnosticWorldHealthLabel.cs`

File mới — **3**:

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_5.md`
- `Assets/MidnightChaos/Runtime/Enemies/DiagnosticEnemySpawner.cs`
- `Assets/MidnightChaos/Runtime/Enemies/DiagnosticMeleeEnemy.cs`

File xóa: **0**

### Thay đổi chính

- Thêm Gate E: một melee enemy chạy hoàn toàn trên Host.
- Thêm state đồng bộ: `Idle`, `Chase`, `Attack`, `Recover`, `Dead`.
- Enemy phát hiện Player sống trong `7.5 m`, bỏ target ngoài `12 m`, di chuyển `2.7 m/s` và xoay tối đa `540°/s`.
- Enemy đánh trong `1.8 m`, gây `20 damage`, cooldown `1.15 s`, attack pose `0.18 s`.
- Enemy chọn Player sống gần nhất; khi target chết hoặc mất hợp lệ, Host tìm target khác.
- Builder tạo một enemy tím có `120 HP`, prefab dùng `NetworkTransform` quyền Server và spawner sinh enemy khi Server bắt đầu.
- `NetworkHealth` được tổng quát hóa bằng `displayName` và cấu hình max health cho Player/Enemy.
- Player melee không còn chỉ quét danh sách Player; Host tìm mọi `NetworkHealth` hợp lệ nên có thể đánh cả PvP lẫn Enemy.
- World health label hiển thị tên, HP và state của Enemy.
- UI chẩn đoán thêm hướng dẫn PvE.
- `NetworkConfig.ProtocolVersion`: `2 → 3`.

## Phần 2 — Thiết lập Unity và kiểm thử

### Thiết lập

1. Chép gói vá lên project `v0.4` và ghi đè.
2. Chờ Unity compile không còn lỗi đỏ.
3. Chạy lại `Create or Refresh LAN Test Scene` để tạo enemy prefab và spawner.
4. Build lại Host/Client; build `ProtocolVersion 2` không được kết nối với bản này.

### Test bắt buộc

- Khi Server bắt đầu, sinh đúng một enemy tại vùng test; Client nhìn thấy cùng một NetworkObject.
- Ngoài detection range, enemy ở `Idle`.
- Đi vào range: enemy chuyển `Chase`, xoay và di chuyển do Host điều khiển.
- Vào attack reach: mỗi hit giảm đúng `20 HP`, không vượt cooldown `1.15 s`.
- State và màu chẩn đoán giống nhau trên Host/Client.
- Enemy ưu tiên Player sống gần nhất và bỏ qua Player chết.
- Player tay không/Sword có thể gây damage lên Enemy; Enemy về `0 HP` chuyển `Dead` và ngừng AI.
- PvP, harvest, craft Sword và reconnect LAN không hồi quy.

## Phần 3 — Nói thêm

- AI di chuyển bằng cách cập nhật transform trên Host; chưa có NavMesh, avoidance, pathfinding, animation hoặc obstacle handling.
- Enemy chết chưa có loot/despawn/respawn ở bản này.
- Source diff xác nhận 7 đường dẫn gốc thay đổi; changelog là file mới thứ 8.
- Chưa thể xác nhận compile/runtime thật trong Unity ở lần tái dựng changelog này.
