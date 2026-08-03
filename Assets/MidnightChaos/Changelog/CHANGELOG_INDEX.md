# Midnight Chaos — Changelog Index V0.1 đến V0.8.2.1

## Phạm vi và độ tin cậy

Bộ changelog này được **tái dựng**, không phải release notes — ghi chú phát hành — nguyên bản. Nguồn bằng chứng được ưu tiên theo thứ tự:

1. Source code trong 16 ZIP được cung cấp.
2. Diff giữa các trạng thái source liên tiếp.
3. `BOOTSTRAP_SETUP.txt` nằm trong các gói đầy đủ.
4. Nội dung bàn giao và phản hồi runtime còn giữ được trong lịch sử trò chuyện.

Không có file `v0.8` trong bộ đính kèm. Vì vậy `v0.8.1` được so sánh với mốc gần nhất có source là `v0.7`; changelog `v0.8.1` là thay đổi tổng hợp giữa hai artifact đó. Bộ này không bịa thêm `CHANGELOG_V0_8.md`.

Các gói `v0.4`, `v0.5`, `v0.6`, `v0.6.1`, `v0.7`, `v0.8.1` và `v0.8.1.1` là patch package. Để tránh báo sai “file mới/ghi đè”, chúng được phủ lần lượt lên source tích lũy trước khi diff.

Không quan sát thấy file source nào bị xóa ở các mốc có bằng chứng.

## Mục lục phiên bản

| Phiên bản | Trọng tâm | Protocol | Đường dẫn gốc thay đổi | Bản sau sửa lỗi gì |
|---|---|---:|---:|---|
| V0.1 | LAN bootstrap, owner movement, reconnect | 1 | 11 file mới | V0.1.1 sửa asmdef Transport |
| V0.1.1 | Sửa `Unity.Networking.Transport` reference | 1 | 3 | Không đổi gameplay |
| V0.2 | Host-validated PvP melee và health | 1 | 7 | — |
| V0.3 | Resource gather bằng `E`, inventory Wood | 1 | 8 | V0.3.2 hợp nhất input |
| V0.3.1 | Tự sửa/lưu UnityTransport | 1 | 3 | — |
| V0.3.2 | Một attack cho combat hoặc harvest | 1 | 7 | — |
| V0.4 | Craft Sword tại Workbench | 2 | 8 | — |
| V0.5 | Host-authoritative melee enemy | 3 | 7 | — |
| V0.6 | Chaos Evolution, 7 Small, Alpha shard | 4 | 10 | V0.6.1 sửa death/config/re-host |
| V0.6.1 | Evolution death + session configuration hotfix | 4 | 4 | — |
| V0.7 | FPS camera và movement theo hướng nhìn | 5 | 5 | — |
| V0.8.1 | PlayerVisual, locomotion/attack animation mạng | 6 | 5* | V0.8.1.1 tích hợp Sword visual |
| V0.8.1.1 | World/first-person Sword và attack blend | 6 | 3 | V0.8.1.2 sửa restart `normalizedTime` |
| V0.8.1.2 | Attack restart reliability + input buffer | 6 | 3 | V0.8.2 chuyển tuning sang SO |
| V0.8.2 | ScriptableObject, held attack, 4 motion code | 7 | 10 | V0.8.2.1 sửa endpoint/live tuning |
| V0.8.2.1 | Đường vung tuyệt đối, motionIndex, tách motion set | 7 | 6 | Mốc cuối trong bộ file |

`*` Mốc `v0.8.1` là diff tổng hợp từ `v0.7` do thiếu artifact `v0.8`.

Mỗi changelog riêng tính thêm chính file changelog vào “Tổng thay đổi”. Cột “Đường dẫn gốc thay đổi” trong bảng không tính changelog được thêm khi tái dựng.

## Chuỗi tương thích mạng

| Protocol | Phiên bản |
|---:|---|
| 1 | V0.1 → V0.3.2 |
| 2 | V0.4 |
| 3 | V0.5 |
| 4 | V0.6 → V0.6.1 |
| 5 | V0.7 |
| 6 | V0.8.1 → V0.8.1.2 |
| 7 | V0.8.2 → V0.8.2.1 |

Cùng ProtocolVersion không đảm bảo nên trộn mọi patch runtime. Ví dụ `v0.8.1.1` và `v0.8.1.2` đều Protocol 6 nhưng logic combat/animation khác; cách an toàn vẫn là Host và Client dùng cùng một build.

## Giới hạn xác minh

- Diff byte/source và cấu trúc ZIP có thể xác minh trong lần tái dựng này.
- Những kết quả runtime được ghi là “đã chạy” chỉ khi lịch sử dự án có phản hồi tương ứng; chúng không thay thế regression test — kiểm thử hồi quy — trên project hiện tại.
- Không changelog nào được coi là bằng chứng Unity compile thành công.
- Bản mới nhất trong bộ này là `v0.8.2.1`; nếu tiếp tục phát triển, nên lấy đây làm baseline thay vì ghép các patch cũ thủ công.
