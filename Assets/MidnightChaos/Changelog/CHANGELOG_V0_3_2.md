# CHANGELOG V0.3.2 — Unified Attack and Harvest

> Changelog này được tái dựng bằng cách so sánh `v0.3.2` với `v0.3.1` và đối chiếu tài liệu Gate C.1 trong ZIP.

## Phần 1 — File

Tổng thay đổi: **8 file**

File ghi đè — **7**:

- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`
- `Assets/MidnightChaos/Runtime/Combat/DiagnosticMeleeCombat.cs`
- `Assets/MidnightChaos/Runtime/Resources/DiagnosticResourceGatherer.cs`
- `Assets/MidnightChaos/Runtime/Resources/DiagnosticResourceNode.cs`
- `Assets/MidnightChaos/Runtime/UI/DiagnosticLanUI.cs`
- `Assets/MidnightChaos/Runtime/UI/DiagnosticWorldResourceLabel.cs`
- `BOOTSTRAP_SETUP.txt`

File mới — **1**:

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_3_2.md`

File xóa: **0**

### Thay đổi chính

- Hợp nhất combat và harvest vào cùng một input: `F` hoặc chuột trái.
- `E` không còn gửi request gather và được giữ trống cho interaction sau này.
- Combat và harvest dùng chung một RPC và cooldown Host `0.65 s`.
- Host tự tìm toàn bộ Player/Tree hợp lệ rồi chọn mục tiêu gần nhất.
- Một attack được chấp nhận chỉ commit tối đa một hậu quả: damage Player hoặc harvest Tree, không thể cả hai.
- Client vẫn không gửi target, damage, resource ID, item ID hoặc số lượng.
- Tree label và UI không còn hướng dẫn nhấn `E` để gather.
- Giữ cơ chế tự sửa UnityTransport của `v0.3.1`.
- `NetworkConfig.ProtocolVersion` giữ nguyên `1`.

## Phần 2 — Thiết lập Unity và kiểm thử

### Thiết lập

1. Chép `Assets/MidnightChaos` vào `Assets` và ghi đè.
2. Chờ Unity compile không còn lỗi đỏ.
3. Chạy lại `Create or Refresh LAN Test Scene`.
4. Mở scene tạo mới và build lại Client.

### Test bắt buộc

- Nhấn `F` hoặc chuột trái vào Tree hợp lệ: Tree mất 1 hit và Player nhận 1 Wood.
- Đúng ba attack hợp lệ làm Tree cạn.
- Khi Player và Tree cùng nằm trong vùng đánh, chỉ mục tiêu hợp lệ gần nhất chịu tác động.
- Một attack không bao giờ vừa damage Player vừa harvest Tree.
- Ngoài `2.6 m` hoặc quay lưng thì không gây tác động.
- Spam không vượt cooldown chung `0.65 s`.
- Hai client tranh hit cuối Tree không nhận Wood hai lần.
- Player chết không attack/harvest được.
- Nhấn riêng `E` không damage hoặc harvest.
- Disconnect/reconnect của Gate A vẫn hoạt động.

## Phần 3 — Nói thêm

- Bản này thống nhất transaction attack/harvest nhưng vẫn là hệ thống chẩn đoán, chưa có weapon hitbox hoặc timing theo animation.
- Inventory vẫn chỉ lưu Wood; Tree không respawn và chưa yêu cầu tool.
- Theo lịch sử dự án, `v0.3.1` và `v0.3.2` đã được test chạy ổn; changelog vẫn không coi đó là bằng chứng compile/runtime cho project hiện tại.
