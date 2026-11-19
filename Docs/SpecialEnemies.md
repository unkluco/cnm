# Light Souls — Quái đặc biệt và vòng tự nhắm

## Vòng tự nhắm

- Khi bấm Đánh với một vũ khí đang trang bị, vòng xanh nhạt xuất hiện quanh chân nhân vật và mờ dần sau khoảng 0,65 giây. Vũ khí bắn tự động làm mới vòng trong lúc giữ nút.
- Bán kính vòng là đúng bán kính tìm mục tiêu trong `PlayerCombat`: cận chiến bằng 1,8 lần tầm vũ khí, tầm xa bằng `AttackRange`, cùng bị giới hạn bởi `autoAimMaxDistance`. Chỉ các quái còn sống có tâm nằm trong vòng mới được xét.
- Đây là phạm vi tìm mục tiêu, không phải vùng bảo đảm trúng đòn. Đòn chém vẫn có góc quét; đạn vẫn có thể bị tường chặn.
- Đồ dùng, ô trống, tắt tự nhắm hoặc khóa điều khiển không hiện vòng.

## Danh sách quái

| Quái | Chiêu | Cách nhận biết và né |
|---|---|---|
| Kỵ Sĩ Xung Kích | Lao nhanh theo một hành lang đã báo trước | Rời khỏi hành lang chữ nhật đỏ |
| Thạch Cự Nhân | Dậm đất tạo sóng chấn động lớn | Rời vòng cam quanh thân |
| Hỏa Pháp Sư | Đặt ba vòng lửa ở vị trí người chơi, gây bỏng | Né ra khỏi các vùng tròn cam đỏ |
| Băng Linh | Đóng băng diện rộng, làm chậm tốc độ | Rời vòng xanh băng trước khi nó kích hoạt |
| Xạ Thủ Bão Tia | Bắn năm luồng đạn theo hình quạt | Nhìn các đường báo màu vàng, tránh hướng bắn |
| Bóng Ma Dịch Chuyển | Báo trước vị trí đáp rồi dịch chuyển và chém | Rời vùng tròn tím |
| Huyết Quỷ | Gây sát thương gần và hồi máu theo lượng máu cướp được | Giữ khoảng cách khi vòng hồng hiện ra |
| Hộ Vệ Khiên | Giảm sát thương khi phòng thủ, hạ khiên trong lúc vung đòn và hồi phục | Tấn công trong khoảng trống sau đòn |
| Tu Sĩ Hồi Phục | Hồi máu cho một đồng minh bị thương; khi không có mục tiêu sẽ dùng xung ánh sáng tấn công | Ưu tiên tiêu diệt tu sĩ; né vòng xanh lá |
| Lõi Nổ | Tiến sát, đếm thời gian báo trước rồi tự nổ | Rời vòng cam khi lõi đang tích năng lượng |

Mỗi phòng chiến đấu có ít nhất một quái đặc biệt; các điểm sinh còn lại có thể là quái thường hoặc thêm quái đặc biệt. Bộ sinh dùng một túi ngẫu nhiên để không lặp lại quái đặc biệt trong cùng chu kỳ cho tới khi dùng hết cả 10 loại.

Prefab và vật liệu nằm trong `Assets/Prefabs/Combat/SpecialEnemies/`. Chỉ số, thời gian báo trước và vùng ảnh hưởng chỉnh được trong Inspector. Menu `LightSouls > Combat > Build Special Enemy Set` tạo lại bộ prefab và kết nối chúng với `Game` và `CharacterCombatTest`.
