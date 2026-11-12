# Light Souls

Dự án game Unity về một nhân vật vượt qua các màn chơi có quái vật, vũ khí và địa hình được sinh ngẫu nhiên.

## Quy trình GitHub

- `main` luôn giữ phiên bản ổn định có thể mở và chơi được; chỉ nhận nội dung đã kiểm tra từ `develop`.
- `develop` là nhánh tích hợp chung để kiểm tra các tính năng đang phát triển.
- Mỗi thành viên chỉ làm việc và commit trên nhánh tính năng được phân công; không phát triển hoặc commit trực tiếp lên `main` hay `develop`.
- Tạo Pull Request (PR) từ nhánh tính năng về `develop`; nhóm trưởng xem xét và là người duy nhất merge các PR tính năng vào `develop`.
- Khi `develop` đã được kiểm tra và ổn định, nhóm trưởng tạo PR từ `develop` vào `main`. Có thể mở PR tính năng sớm để tích hợp và nhận góp ý.

Quy ước kỹ thuật chung (phiên bản Unity, đơn vị, tổ chức code, hợp đồng giữa các hệ thống, tag/layer, sở hữu file) nằm trong [`Docs/Conventions.md`](Docs/Conventions.md).

## Tổ chức Scene

- `MainMenu`: giao diện đầu game và nút bắt đầu.
- `Game`: Scene chơi chính, nơi lắp ghép nhân vật, hệ thống chiến đấu và bản đồ được sinh.
- Scene thử riêng như `CharacterCombatTest` và `MapGenTest` dùng để phát triển từng hệ thống trước khi đưa vào `Game`.

Ưu tiên đóng gói nhân vật, quái vật và thành phần dùng lại thành Prefab. Các hệ thống cần thống nhất cách trao đổi dữ liệu trước khi tích hợp, ví dụ bộ sinh màn cung cấp vị trí bắt đầu, điểm sinh quái và đích đến.

## Unity

Mở thư mục này bằng Unity Hub. Phiên bản Unity của dự án được ghi trong `ProjectSettings/ProjectVersion.txt`.

Các thư mục Unity có thể tạo lại như `Library`, `Temp` và `Logs` được bỏ qua trong Git. Nội dung trong `Assets`, `Packages` và `ProjectSettings` được lưu trong repo.

## Nhật ký task

Task được lưu theo từng đợt, mỗi người một file:

```
Tasks/
  YYYY-MM-DD/      ngày giao đợt việc
    kiet.md
    khai.md
    dat.md
```

- Mỗi đợt mới là một thư mục mới; không sửa nội dung giao việc của đợt cũ để giữ lịch sử phân công.
- Một người không có việc trong đợt thì không cần file.
- Mỗi file ghi rõ nhánh, việc cần làm, tiêu chí hoàn thành và những gì phải bàn giao.
- Việc của một đợt cũ chưa xong vẫn còn hiệu lực, trừ khi file của đợt mới ghi khác.
