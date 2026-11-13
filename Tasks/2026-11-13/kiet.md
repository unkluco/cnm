# Kiệt — Nhóm trưởng, giao diện (2026-11-13)

**Nhánh làm giao diện:** `feature/menu-ui`

Đọc trước: [`Docs/Conventions.md`](../../Docs/Conventions.md).

## Đã xong trong đợt này

- Quy ước chung được duyệt và merge (`Docs/Conventions.md`, `.gitattributes`).
- Khung dự án:
  - Assembly `LightSouls.Common` với `IDamageable` và `DamageInfo`.
  - Assembly `LightSouls.Game` với `GameBootstrap` còn rỗng.
  - Scene `MainMenu` và `Game` là hai Scene duy nhất trong Build Settings.
  - Tag `Enemy`, các layer `Player`/`Enemy`/`Environment`/`PlayerAttack`/`EnemyAttack` và ma trận va chạm.
  - Đã bỏ Scene và tài liệu mẫu của template URP.
- GitHub: `main` và `develop` chỉ nhận thay đổi qua PR, PR cần một lượt duyệt.

## Việc cần làm

### 1. Giao diện (trên `feature/menu-ui`)

1. Code đặt trong `Assets/Scripts/UI/`, assembly `LightSouls.UI`, chỉ tham chiếu `LightSouls.Common`.
2. Scene `MainMenu`: tên game, nút **Chơi** (nạp Scene `Game`) và nút **Thoát**.
3. Menu tạm dừng dùng trong Scene `Game`: mở/đóng bằng action `Cancel` của action map `UI` (phím Esc), gồm **Tiếp tục** và **Về menu chính**. Khi tạm dừng thì `Time.timeScale = 0`.
4. Màn hình **Thua** và **Qua màn** dạng placeholder, mỗi màn có nút **Chơi lại** và **Về menu chính**. Chưa tự bật; để `GameBootstrap` gọi khi ghép, nên cần hàm công khai để hiện từng màn.
5. Đóng gói các màn hình thành prefab trong `Assets/Prefabs/UI/` để đặt vào Scene `Game` khi ghép.
6. Theo hướng Android, màn hình ngang (xem mục 1 của quy ước):
   1. Nút tạm dừng trên màn hình ở góc phải trên, trong vùng an toàn, vì điện thoại không có phím Esc. Nút ẩn khi đang tạm dừng.
   2. Màn **Cài đặt** mở từ menu chính và menu tạm dừng: âm lượng tổng, nhạc, hiệu ứng; chọn 30 hoặc 60 FPS; bật/tắt rung. Giá trị đọc và ghi qua `LightSouls.Common.GameSettings`. Nút Back của Android đóng màn Cài đặt.
   3. Mọi nút cao tối thiểu 96 px, các nút sát cạnh màn hình nằm trong `SafeAreaFitter`.
   4. Thử bằng Device Simulator với một máy Android có tai thỏ.
   5. Cài module **Android Build Support** trong Unity Hub để build APK thử trên máy thật.

Chưa làm HUD thanh máu: cần biết hệ thống máu của Khải lộ ra những gì, nên để sang đợt ghép.

### 2. Review

- Review PR của Đạt và Khải theo checklist ở mục 7 của quy ước, rồi merge vào `develop`.
- Merge PR giao diện của chính mình: GitHub cho phép admin bỏ qua lượt duyệt, nhưng vẫn tự kiểm tra theo checklist trước.

## Tiêu chí hoàn thành (giao diện)

- Mở `MainMenu`, vào Play Mode, bấm **Chơi** thì chuyển sang `Game`; bấm **Thoát** thì thoát (trong Editor thì dừng Play Mode).
- Trong `Game`, Esc mở và đóng menu tạm dừng; **Về menu chính** quay lại `MainMenu`.
- Gọi được hàm hiện màn **Thua** và **Qua màn** (thử bằng nút tạm hoặc từ Inspector).

## Kế hoạch đợt ghép (sau khi PR của Đạt và Khải được merge)

1. Thêm `LightSouls.MapGeneration` và `LightSouls.Combat` vào references của `LightSouls.Game.asmdef`.
2. Viết `GameBootstrap` theo bốn bước ghi trong comment của lớp:
   1. Sinh màn.
   2. Đặt người chơi tại điểm bắt đầu.
   3. Nối vòng đời phòng với việc sinh quái.
   4. Xử lý tới đích.
3. Nối màn **Thua** với sự kiện `Died` của người chơi; nối màn **Qua màn** với việc tới đích.
4. Đặt prefab `Combat_TouchControls` của Khải vào Scene `Game`; nối `PauseMenuController.PausedChanged` với hàm bật/tắt điều khiển của Khải.
5. Thêm HUD thanh máu.
6. Tiêu chí: từ `MainMenu` vào `Game`, đi qua các phòng, đánh hết quái để mở cửa, tới phòng đích là qua màn; chết thì hiện màn thua.
7. Khi bắt đầu đợt ghép, tạo thư mục `Tasks/<ngày>/` mới và chia việc theo kế hoạch trên.
