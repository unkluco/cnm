# Light Souls — Ngũ Đại Thủ Vệ

Mỗi phòng boss (`RoomType.Exit`) sinh **đúng một** boss khổng lồ. Năm boss được xáo trộn theo lượt chơi, không gặp lại trước khi đi hết vòng và không lặp ngay ở lượt kế. Phòng thường vẫn sử dụng hệ thống quái cũ. Hòm thưởng 1–2 chiếc và cổng bí ẩn chỉ mở khi boss đã chết.

## Danh sách boss

| Boss | Máu gốc | Phong cách | Bộ kỹ năng |
| --- | ---: | --- | --- |
| Varkhan — Chúa Tể Tro Tàn | 480 | Hiệp sĩ giáp nham thạch, đại kiếm cháy | Hỏa đao: vung cung kiếm; Vành tro đỏ: vòng lửa có tâm an toàn; Tam ấn dung nham: ba dấu nổ; Hỏa xa: lao theo đường đã khóa; Mưa thiên thạch: nhiều đợt thiên thạch. |
| Nhalis — Nữ Vương Băng Vĩnh Cửu | 420 | Vương miện tinh thể, trượng băng và váy áo nổi | Vương miện băng: sáu trụ băng; Băng tuyến: ba tia; Tuyết ấn: dấu nổ rồi vành lạnh; Băng vỡ: bốn vết cắt chữ thập; Lưới pha lê: các dải song song chuyển sang khe trống ở đợt sau. |
| Orgrim — Cự Thần Thạch Ấn | 550 | Golem cổ đại với vai đá, nắm đấm và lõi rune | Nghiền đất: đập rộng quanh thân; Song quyền: hai nắm đấm liên tiếp; Vết nứt cổ đại: sóng nứt tỏa ba hướng; Cự thạch lao: lao nặng; Vòng địa chấn: các đợt vòng chấn động lan ra. |
| Noctyr — Vua Hư Không | 455 | Vua bóng tối lơ lửng, mão rỗng, vòng thiên thể | Bước qua hư vô: dịch chuyển tới dấu đã báo; Nguyệt thực: vành hư không; Tam bóng kiếm: ba vệt cắt từ những góc khác nhau; Mảnh gương: bốn dấu nổ đổi vị trí; Liềm hư không: hai cung đối diện đổi hướng ở nhịp sau. |
| Aurex — Đại Long Lôi Ấn | 510 | Rồng khổng lồ có cánh gấp khúc, sừng, đuôi dài | Lôi tức: hơi thở hình quạt; Cánh bão: hai làn cánh quét có khe ở giữa; Thiên lôi: dấu sét nối tiếp; Đuôi lôi long: quật đuôi thành vành; Sét phân nhánh: năm tia tỏa. |

Ở 50% máu, boss chuyển sang giai đoạn II sau đòn hiện tại: thời gian tụ chiêu/hồi ngắn hơn, nhiều chiêu có thêm nhịp được báo riêng. Tất cả các boss có thể chịu hiệu ứng súng/kiếm hiện có. Lửa và băng có thể gây bỏng/chậm nhưng chỉ khi người chơi thực sự nhận sát thương.

## Báo chiêu và khả năng né

- Mỗi nhịp chụp vị trí người chơi lúc bắt đầu tụ lực; dấu trên sàn không chạy theo người chơi sau đó. Vùng hình tròn, hình quạt, hành lang và vành có vùng an toàn dùng cùng `BossZone` khi vẽ và khi tính trúng đòn. Các vùng chồng nhau trong một nhịp chỉ gây một lần sát thương. Chướng ngại/tường vẫn chặn đòn; pháp trận cũng được cắt theo ranh giới phòng.
- Boss di chuyển trong giới hạn phòng/Navigable Mesh; đường lao được cắt tới điểm đi được, dịch chuyển không đặt boss chồng lên người chơi. Dấu báo chiêu và hành động đang chạy được hủy khi boss chết, choáng hoặc bị vô hiệu hóa.
- Đòn thường xuất hiện theo chu kỳ 5 kỹ năng để tránh chỉ dùng đi dùng lại một chiêu. VFX dùng vật liệu chia sẻ, giới hạn số hạt phụ, không thêm nguồn sáng động.

## Đồ họa và giao diện

- `Assets/Resources/Combat/Bosses` chứa năm prefab độc lập; mỗi thân dài/cao hơn 4m, phối khối low-poly mới, không phải nhân bản quái thường. `LightSouls.Art.Editor.BossForgeBuilder.Build` tạo lại bộ prefab và mesh cánh/áo choàng từ bộ tài nguyên hiện có.
- Thanh máu thường có viền đồng mỏng, lõi đỏ, tên màu ngà và phần máu vừa mất chuyển vàng rồi tụt dần. Boss có bảng máu lớn trên màn hình, tên, số máu, mốc nửa máu, giai đoạn và tên chiêu/nhịp hiện hành. Bảng chỉ xuất hiện trong đợt boss, không chặn điều khiển.
- Camera nhẹ nhàng lùi ra và hướng giữa boss với người chơi trong phòng boss, trở về cự ly thường khi trận kết thúc.

## Kiểm thử

Unity EditMode: kiểm tra danh sách prefab, đủ 25 kỹ năng, hình học vùng trúng, dấu cảnh báo, luân phiên boss. Unity PlayMode: sinh boss trong ngục thật, mở đúng hòm/cổng, kiểm tra chuyển giai đoạn, vòng kỹ năng của đủ 5 boss và thanh máu thường. Đặt `LIGHTSOULS_BOSS_CAPTURE_DIR` khi chạy PlayMode có graphics để chụp ở thời điểm tụ lực/giải chiêu trong game.
