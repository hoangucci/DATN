# CHANGELOG V0.8.1.1 — Sword Visual Integration and Attack Layer Blend

> Changelog này được tái dựng bằng cách phủ patch `v0.8.1.1` lên trạng thái source `v0.8.1` và đọc diff của Builder, Equipment và Animation.

## Phần 1 — File

Tổng thay đổi: **4 file**

File ghi đè — **3**:

- `Assets/MidnightChaos/Editor/MidnightChaosBootstrapBuilder.cs`
- `Assets/MidnightChaos/Runtime/Equipment/DiagnosticPlayerEquipment.cs`
- `Assets/MidnightChaos/Runtime/Player/DiagnosticPlayerAnimation.cs`

File mới — **1**:

- `Assets/MidnightChaos/Changelog/CHANGELOG_V0_8_1_1.md`

File xóa: **0**

### Thay đổi chính

- `DiagnosticPlayerEquipment` không còn phụ thuộc SwordVisual nằm trực tiếp dưới root.
- Thêm reference `World Sword Visual`; code có thể tìm object tên `SwordVisual` ở bất kỳ descendant nào, thường dưới `RightHandSocket` của model.
- Builder ưu tiên bảo toàn/tìm SwordVisual trong `PlayerVisual`; chỉ tạo primitive sword dự phòng khi không tìm thấy.
- Thêm `First Person Sword Prefab` tùy chọn. Nếu để trống, local owner clone World Sword Visual làm visual dưới camera.
- Thêm position/rotation/scale riêng cho kiếm góc nhìn thứ nhất.
- World sword chỉ hiển thị cho remote peer hoặc khi owner bật full-body debug.
- First-person sword chỉ hiển thị cho owner khi đã có Sword và full-body debug đang tắt.
- Chặn visual source có `NetworkObject` để tránh clone nhầm network prefab dưới camera.
- Thêm warning rõ khi thiếu world sword, camera hoặc visual source.
- `DiagnosticPlayerAnimation` phát event khi trạng thái local debug thay đổi để Equipment cập nhật đúng hai visual.
- UpperBody Attack chuyển sang state machine nội bộ: `BlendIn → Playing → BlendOut`.
- Mặc định blend in `0.08 s`, exit normalized time `0.95`, blend out `0.10 s`.
- Không đổi RPC/`NetworkVariable`; `NetworkConfig.ProtocolVersion` giữ nguyên `6`.

## Phần 2 — Thiết lập Unity và kiểm thử

### Thiết lập

1. Chép patch lên project `v0.8.1` và ghi đè.
2. Chờ Unity compile không còn lỗi đỏ.
3. Chạy lại `Create or Refresh LAN Test Scene` để Builder serialize `World Sword Visual` vào Equipment.
4. Kiểm tra SwordVisual nằm dưới `PlayerVisual/…/RightHandSocket` hoặc gán trực tiếp vào field.
5. Nếu world sword là SkinnedMesh phụ thuộc armature, gán một prefab mesh độc lập vào `First Person Sword Prefab`; clone trực tiếp SkinnedMesh không có bone đúng dưới camera.
6. Build lại Client vì runtime script đã đổi.

### Test bắt buộc

- Trước khi craft, cả world sword và first-person sword đều ẩn.
- Sau khi Host cấp Sword, owner thấy first-person sword; peer khác thấy world sword trên tay model.
- Nhấn `F8`: owner thấy full body/world sword và first-person sword ẩn; nhấn lại đảo ngược.
- Không xuất hiện hai kiếm cùng lúc trên owner.
- Attack UpperBody blend vào/ra thay vì bật/tắt layer tức thời.
- Disconnect/despawn xóa first-person clone và không để object rác dưới camera.
- Host/Client đều thấy trạng thái Sword đồng nhất.

## Phần 3 — Nói thêm

- Lỗi đã biết của `v0.8.1.1`, sửa ở `v0.8.1.2`: gọi `Animator.Play` để restart Attack rồi đọc `normalizedTime` cũ trong cùng frame có thể làm đòn mới blend out ngay.
- Bản này chưa có animation vung kiếm góc nhìn thứ nhất; kiếm chỉ được gắn theo camera.
- Các thông số first-person transform vẫn nằm trực tiếp trong component Equipment.
- Chưa thể xác nhận compile/runtime thật trong Unity ở lần tái dựng changelog này.
