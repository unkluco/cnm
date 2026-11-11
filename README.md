# Light Souls

Dự án game Unity về một nhân vật vượt qua các màn chơi có quái vật, vũ khí và địa hình được sinh ngẫu nhiên.

## Quy trình GitHub

- `main` là nhánh tích hợp ổn định.
- Mỗi thành viên chỉ làm việc và commit trên nhánh tính năng được phân công; không phát triển hoặc commit trực tiếp lên `main`. Điều này áp dụng cho cả người phụ trách giao diện.
- Khi có thay đổi cần tích hợp, tạo Pull Request (PR) từ nhánh tính năng về `main`.
- Nhóm trưởng xem xét và là người duy nhất merge PR vào `main`.
- Có thể tạo PR khi một phần tính năng đã chạy được để tích hợp và nhận góp ý sớm.

## Tổ chức Scene

- `MainMenu`: giao diện đầu game và nút bắt đầu.
- `Game`: Scene chơi chính, nơi lắp ghép nhân vật, hệ thống chiến đấu và bản đồ được sinh.
- Scene thử riêng như `CharacterCombatTest` và `MapGenTest` dùng để phát triển từng hệ thống trước khi đưa vào `Game`.

Ưu tiên đóng gói nhân vật, quái vật và thành phần dùng lại thành Prefab. Các hệ thống cần thống nhất cách trao đổi dữ liệu trước khi tích hợp, ví dụ bộ sinh màn cung cấp vị trí bắt đầu, điểm sinh quái và đích đến.

## Unity

Mở thư mục này bằng Unity Hub. Phiên bản Unity của dự án được ghi trong `ProjectSettings/ProjectVersion.txt`.

Các thư mục Unity có thể tạo lại như `Library`, `Temp` và `Logs` được bỏ qua trong Git. Nội dung trong `Assets`, `Packages` và `ProjectSettings` được lưu trong repo.

## Nhật ký task

Task được lưu theo từng đợt trong thư mục `Tasks/`, dùng tên file bắt đầu bằng ngày theo định dạng `YYYY-MM-DD`. Task mới được thêm thành file mới để giữ lịch sử phân công.
