# CHANGELOG V0.1.1 — Unity Transport Assembly Fix

> Changelog này được tái dựng bằng cách so sánh `v0.1.1` với `v0.1` và đối chiếu `BOOTSTRAP_SETUP.txt`.

## Phần 1 — File

Tổng thay đổi: **4 file**

File ghi đè — **3**:

- `Assets/MidnightChaos/Editor/MidnightChaos.Editor.asmdef`
- `Assets/MidnightChaos/Runtime/MidnightChaos.Runtime.asmdef`
- `BOOTSTRAP_SETUP.txt`

File mới — **1**:

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_1_1.md`

File xóa: **0**

### Thay đổi chính

- Thêm tham chiếu assembly `Unity.Networking.Transport` vào cả Runtime và Editor asmdef.
- Sửa lỗi `CS0012` liên quan đến `NetworkEndpoint`.
- Khôi phục khả năng compile Editor assembly và hiển thị menu Bootstrap.
- Không thay đổi gameplay, RPC, `NetworkVariable` hoặc cấu trúc prefab.
- `NetworkConfig.ProtocolVersion` giữ nguyên `1`.

## Phần 2 — Thiết lập Unity và kiểm thử

### Thiết lập

1. Chép `Assets/MidnightChaos` vào `Assets` và ghi đè file cũ.
2. Xác nhận package `Unity Transport` đi kèm NGO đã được cài.
3. Chờ Unity compile không còn lỗi đỏ.
4. Chạy lại:

   `Midnight Chaos > Bootstrap > Create or Refresh LAN Test Scene`

5. Build lại Client nếu bản build trước được tạo từ `v0.1` lỗi compile.

### Test bắt buộc

- Console không còn `CS0012` về `NetworkEndpoint`.
- Menu `Midnight Chaos > Bootstrap` xuất hiện.
- Builder tạo scene và prefab thành công.
- Host/Join LAN, owner movement, disconnect/reconnect của `v0.1` vẫn hoạt động.

## Phần 3 — Nói thêm

- Đây là hotfix phụ thuộc assembly; không có lý do gameplay để giữ cả hai bản `v0.1` và `v0.1.1`.
- Source diff xác nhận chỉ hai asmdef và tài liệu thiết lập thay đổi.
- Chưa thể xác nhận compile/runtime thật trong Unity ở lần tái dựng changelog này.
