# Light Souls — Expanded Dungeon Terrain

## Mục tiêu

Giữ phong cách Dark Fantasy Low-Poly của Light Souls, mở rộng thư viện từ 12 lên 30 mẫu phòng. Địa hình cần ảnh hưởng cách giao chiến nhưng vẫn dễ đọc trên màn hình điện thoại. Mỗi lượt chơi vẫn được sinh từ topology và cửa nối hiện có; không ghép cố định một scene lớn.

## Các bộ địa hình

| Hầm ngục | Bản sắc | Mẫu chiến đấu mới |
| --- | --- | --- |
| Thành cổ (`dungeon.standard`) | Đá xanh xám, kim loại đồng, tượng đổ, nước và thư viện | Hành Lang Tu Viện, Thư Viện Hoang Phế, Cầu Đá Cổ, Điện Thử Lửa |
| Dung nham (`dungeon.lava`) | Đá basalt, tro đỏ, khe dung nham, bệ rèn | Đảo Hắc Thạch, Cầu Hỏa Ngục, Xưởng Rèn Tro Tàn |
| Mê cung (`dungeon.maze`) | Rêu đá, tinh thể ngọc, nước tù, các vách thấp đổ | Sân Điện Ngập Nước, Mê Cung Rêu Phong, Ngã Tư Thủy Môn, Mật Điện Bẫy Cổ |

Hai bộ Dung nham và Mê cung sử dụng sảnh đầu ngục và điện thờ boss riêng (4 mẫu). Cả ba bộ có một kho báu ẩn riêng (3 mẫu). Tổng cộng: 11 phòng chiến đấu + 4 phòng vai trò đặc biệt + 3 kho báu mới = 18; 12 mẫu phòng cũ cũng được bổ sung vật liệu, đèn, chân tường và kiến trúc.

## Địa hình có gameplay thật

- Cột, kệ sách, bệ rèn và vách đổ có collider thật; chúng tạo đường vòng nhưng không đè lên cửa nối.
- Các cầu đá có sàn nâng 0,24–0,26m và bốn dốc MeshCollider, không chỉ là họa tiết. Cả người chơi lẫn NavMesh có thể đi lên cầu từ mọi ngả.
- Dung nham gây sát thương, nước làm chậm và bẫy gai hoạt động lệch nhịp bằng hệ thống hazard sẵn có. Những vùng nguy hiểm đều có đường khô tránh được và các điểm spawn bên ngoài vùng trigger. Bề mặt dung nham có mảng màu phát sáng và lớp vỏ basalt; mặt nước có mảng phản quang, gợn và bờ rêu, đi cùng tối đa hai nguồn sáng khu vực chịu ngân sách đèn chung.
- Các phòng chiến đấu được khóa/mở theo `RoomLifecycleController` như trước. Các phòng thủ công bỏ `ProceduralRoomApplicator` để tránh nội thất ngẫu nhiên ghi đè bố cục có chủ đích; những mẫu cũ vẫn dùng nội thất ngẫu nhiên.
- `Room_RoyalReliquary`, `Room_EmberVault` và `Room_DrownedCache` là nhánh cụt kho báu: mỗi lượt sinh đúng một phòng tương ứng vùng, có hòm thật trên bệ riêng. Loot có độ hiếm Rare trở lên, không bỏ qua điều kiện nhận nguyên liệu chuyên rơi từ boss.
- Cả năm thủ vệ được biểu diễn bằng họa tiết sàn riêng và tinh thạch theo màu boss thực sự xuất hiện. Sàn giữa đấu trường phải luôn rộng và trống cho các chiêu của boss; họa tiết không phải vùng sát thương. Cổng bí ẩn, hòm thưởng boss và trạm tiếp tế sử dụng lại luồng gameplay đang có.

## Camera, mốc đường đi và hiệu suất

`DungeonWallOcclusion` kiểm tra giữa camera và người chơi. Chỉ các mặt tường trên cao thật sự chắn nhân vật được chuyển sang vật liệu trong suốt; phần chân tường, collider và cửa khóa vẫn giữ nguyên. Tường tự trở về trạng thái đục khi nhân vật đi khỏi. Không tắt toàn bộ phòng, không tạo lối đi xuyên tường.

Màu cửa dẫn đường vẫn theo `RoomLifecycleController`. Minimap có ba icon riêng cho cửa hàng, kho báu và boss. Phòng lân cận hiện bằng màu trung tính; biểu tượng chỉ hiện sau khi đi vào phòng đó. Nhánh kho báu ẩn không được vẽ trước khi khám phá.

Mesh kiến trúc được gộp theo vật liệu, tách theo hướng tường để mờ độc lập. Vật liệu, mesh cơ bản, đèn và ánh sáng dùng chung bộ Dark Fantasy; đèn dungeon vẫn bị giới hạn bởi ngân sách runtime hiện có. `Art_...` chứa mesh trang trí không collider. Nội thất tương tác nằm trong `TerrainColliders`; trigger địa hình nằm trong `AuthoredHazards`.

## Mở rộng tiếp

- `RoomTerrainProfile`: vùng, tên hiển thị, mô tả bố cục, cờ kho báu ẩn và cờ nội thất được thiết kế thủ công. Thông tin kết nối vẫn do `RoomModule`/`RoomTemplateSO` quản lý.
- Các asset `DefaultDungeonConfig`, `Config_LavaDungeon`, `Config_MazeDungeon` quản lý pool phòng riêng. Cửa và footprint tương thích với format cũ. Những mẫu mới nhận trọng số cao hơn và không lặp quá một lần trong cùng màn.
- Dùng **Light Souls → Art → Build Expanded Dungeon Terrain** khi chủ động tái dựng cả bộ từ mã authoring. Prefab đã lưu có thể chơi ngay, không cần chạy menu này. Không chạy đè khi đang sửa prefab thủ công chưa lưu.

## Kiểm tra

`ExpandedTerrainTests` kiểm thử đủ 30 template, các pool, 32 seed cho mỗi trong ba ngục, nhánh kho báu, đường khô, đường đi NavMesh và collider dốc ở phòng chiến đấu. `ExpandedTerrainPlayTests` kiểm tra build Game thực tế, cửa hàng, hòm hiếm, trạng thái khóa/mở cửa, biểu tượng minimap, trang trí boss và việc tường mờ không làm mất va chạm.

Khi chạy PlayMode có GPU, đặt `LIGHTSOULS_TERRAIN_CAPTURE_DIR` để lưu ảnh chụp sảnh chiến đấu, kho báu, điện thờ boss và tường mờ.
