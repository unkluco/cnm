# Ánh sáng sảnh, bảng tên ngục và trạm tiếp tế

- Sảnh giữ toàn bộ nguồn sáng trang trí, lò rèn và cổng hoạt động độc lập vị trí người chơi. Lửa vẫn dao động nhẹ, các đèn điểm không đổ bóng. Ngục sinh ngẫu nhiên giữ giới hạn 4 đèn điểm gần người chơi để phù hợp mobile.
- Khi một ngục sinh thành công, `GameBootstrap` tìm `DungeonEntry` đúng với config thực tế trong `DungeonCatalogSO` và hiện `DungeonArrivalBanner`. Ba ngục có ấn ký và màu riêng; bảng giữ khoảng 4 giây, tự ẩn khi hết thời gian hoặc mở menu. Không khóa điều khiển và không tiết lộ đích cổng bí ẩn trước khi bước vào.
- `WeaponVendingMachine` ở phòng Start là điểm `IInteractable`. Bấm **Cửa hàng** hoặc `E` khi đến gần sẽ mở chính giao diện mua đồ của sảnh, dùng chung `Panel_Shop.prefab`, `ShopCatalog_Main`, lượng xu trong `RunState` và túi đồ hiện tại. Máy có collider thân riêng và trigger ở `InteractionPoint`. Không tự cấp đồ hoặc xử lý giá trong phần sinh màn.
- Mở bảng cửa hàng khóa điều khiển bằng `BackButtonStack`, nút Back/Esc đóng bảng; không mở chồng balo hoặc pause. Thiếu xu/balo đầy không bị trừ tiền; nguyên liệu dùng túi riêng như ở sảnh.

## Bảo trì

Chỉnh bố cục cửa hàng trong prefab `Assets/Prefabs/Lobby/Panel_Shop.prefab`; cả Lobby và Game dùng đúng prefab này. Chỉnh vật phẩm và giá trong catalog/ItemDefinitionSO như cũ. Nếu dựng lại cả hai scene bằng bộ công cụ prototype, chạy `Light Souls > Art > Build Shared Dungeon Shop` để nối lại prefab và chế độ sáng cố định của sảnh. Khi thêm loại ngục vào catalog, tên và mô tả bảng báo ngục cập nhật theo catalog; loại mới dùng ấn ký mặt trời mặc định.
