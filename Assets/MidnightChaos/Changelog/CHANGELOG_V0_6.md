# CHANGELOG V0.6 — Chaos Evolution

> Changelog này được tái dựng bằng cách phủ gói vá `v0.6` lên trạng thái source `v0.5`, đọc toàn bộ logic evolution và đối chiếu lịch sử test của dự án.

## Phần 1 — File

Tổng thay đổi: **11 file**

File ghi đè — **6**:

- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`
- `Assets/MidnightChaos/Runtime/Combat/NetworkHealth.cs`
- `Assets/MidnightChaos/Runtime/Enemies/DiagnosticEnemySpawner.cs`
- `Assets/MidnightChaos/Runtime/Enemies/DiagnosticMeleeEnemy.cs`
- `Assets/MidnightChaos/Runtime/UI/DiagnosticLanUI.cs`
- `Assets/MidnightChaos/Runtime/UI/DiagnosticWorldHealthLabel.cs`

File mới — **5**:

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_6.md`
- `Assets/MidnightChaos/Runtime/Enemies/DiagnosticChaosEvolutionService.cs`
- `Assets/MidnightChaos/Runtime/Enemies/DiagnosticChaosShard.cs`
- `Assets/MidnightChaos/Runtime/Enemies/DiagnosticEnemyEvolution.cs`
- `Assets/MidnightChaos/Runtime/UI/DiagnosticWorldChaosShardLabel.cs`

File xóa: **0**

### Thay đổi chính

- Thêm Gate F: Chaos Evolution do Host quyết định.
- Host sinh đúng một cụm 7 quái: 1 ở tâm và 6 xung quanh.
- Tất cả bắt đầu ở stage `Small`, `66 HP`, charge `0/2`.
- Khi một Small/Mature chết, Host chuyển một Chaos Charge cho cá thể sống gần nhất cùng species trong bán kính `12 m`.
- Nếu khoảng cách bằng nhau, `NetworkObjectId` nhỏ hơn thắng để kết quả ổn định.
- Không có cá thể hợp lệ thì charge của cái chết đó bị mất.
- `Small` nhận đủ 2 charge thành `Mature`, max HP `120`.
- `Mature` nhận thêm đủ 3 charge thành `Alpha`, max HP `264`.
- Khi tiến hóa, charge về 0 và tỷ lệ HP hiện tại được bảo toàn.
- `Alpha` không nhận charge; khi chết chỉ được drop đúng một Chaos Shard.
- Stage điều chỉnh scale, màu, damage, tốc độ và attack reach của Enemy; nhận charge/tiến hóa tạo flash phản hồi.
- `NetworkHealth` thêm max health được replicate, death event phía Server và API đổi max health giữ tỷ lệ.
- Builder tạo body visual, collider, Chaos Shard prefab/material, evolution service và cụm spawner.
- UI hiển thị stage, charge, HP và số Chaos Shard trong world.
- `NetworkConfig.ProtocolVersion`: `3 → 4`.

## Phần 2 — Thiết lập Unity và kiểm thử

### Thiết lập

1. Chép gói vá lên project `v0.5` và ghi đè.
2. Chờ Unity compile không còn lỗi đỏ.
3. Chạy lại `Create or Refresh LAN Test Scene` để tạo enemy/shard prefab và service mới.
4. Build lại mọi Client; build `ProtocolVersion 3` không tương thích.

### Test bắt buộc

- Bắt đầu Host sinh đúng 7 Small, mỗi con `66 HP`.
- Giết một Small gần đàn: đúng một Small sống nhận `C1/2` và flash.
- Cho cùng cá thể nhận charge thứ hai: chuyển `Mature`, max HP `120`, charge về 0.
- Cho Mature nhận thêm 3 charge: chuyển `Alpha`, max HP `264`.
- Giết Alpha: sinh đúng một Chaos Shard trên Host và Client.
- Quái khác species không nhận charge.
- Không có receiver trong `12 m`: Console ghi charge bị mất, không tự dồn cho cá thể bất kỳ.
- Stat/scale/label thay đổi đúng stage và đồng bộ giữa các peer.
- PvE/PvP, harvest, craft và LAN session không hồi quy.

## Phần 3 — Nói thêm

- Lỗi đã biết của chính `v0.6`, được sửa ở `v0.6.1`: commit side effect của death phụ thuộc đường event/config chưa đủ bền; trong một số trạng thái scene/service, charge/evolution có thể không được xử lý như dự kiến.
- `v0.6` cũng chưa có fallback PlayerPrefab/callback đủ chắc cho chu kỳ refresh/re-host; `v0.6.1` gia cố phần này.
- Chaos Shard ở đây mới là NetworkObject/visual; chưa có pickup hoặc công dụng gameplay cho Player.
- Chưa thể xác nhận compile/runtime thật trong Unity ở lần tái dựng changelog này.
