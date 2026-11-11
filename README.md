# CNM — Game vượt ải

Dự án game Unity nơi người chơi điều khiển một nhân vật vượt qua các màn chơi với quái vật, vũ khí và địa hình được sinh ngẫu nhiên.

## Nhóm và phân công

| Thành viên | Nhánh | Phạm vi |
|---|---|---|
| Nhóm trưởng (bạn) | `feature/menu-ui` | Giao diện đầu game, điều hướng bắt đầu chơi và các màn hình menu cơ bản. |
| Khải | `feature/character-combat` | Nhân vật và quái vật; cơ chế chiến đấu, tính sát thương/máu và tiêu diệt quái; chọn vũ khí súng/kiếm ở mức cơ bản. Chưa cần túi đồ hoàn chỉnh. |
| Đạt | `feature/map-generation` | Nghiên cứu cấu trúc/format bản đồ địa hình và xây dựng cơ chế sinh màn ngẫu nhiên. |

## Quy trình GitHub

- `main` là nhánh tích hợp ổn định.
- Mỗi thành viên làm việc trên nhánh tính năng được phân công.
- Thành viên tạo Pull Request (PR) về `main` để nhóm trưởng xem xét.
- Chỉ nhóm trưởng merge PR vào `main`.
- Tạo PR sớm khi đã có một phần chạy được; không cần chờ toàn bộ tính năng hoàn tất mới tích hợp.

## Tổ chức Scene gợi ý

- `MainMenu`: giao diện đầu game và nút bắt đầu.
- `Game`: Scene chơi chính, nơi lắp ghép nhân vật, hệ thống chiến đấu và bản đồ được sinh.
- `CharacterCombatTest` và `MapGenTest`: Scene thử riêng trong quá trình phát triển; không thay thế Scene chơi chính.

Đóng gói nhân vật, quái vật và thành phần có thể tái sử dụng thành Prefab. Các hệ thống nên có đầu ra/giao diện thống nhất để tích hợp thuận lợi, ví dụ bộ sinh bản đồ cung cấp vị trí bắt đầu, điểm sinh quái và đích đến.

## Dự án Unity

Mở thư mục này bằng Unity Hub. Phiên bản dự án hiện tại được ghi trong `ProjectSettings/ProjectVersion.txt`.

Các thư mục do Unity tạo lại như `Library`, `Temp` và `Logs` được bỏ qua trong Git; nội dung trong `Assets`, `Packages` và `ProjectSettings` được lưu trong repo.
