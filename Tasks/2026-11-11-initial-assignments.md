# Phân công ban đầu — 2026-11-11

Mục tiêu của đợt này là tạo hai prototype độc lập, có thể kiểm tra trong Unity và ghép vào Scene chơi chính sau khi được review.

> **Quy tắc nhánh:** Không phát triển hoặc commit trực tiếp lên `main`. Mỗi người chỉ làm trên nhánh tính năng được phân công (bao gồm người phụ trách giao diện); thay đổi được gửi bằng PR về `main`.

## Khải — Nhân vật và chiến đấu

**Nhánh:** `feature/character-combat`

### Việc cần làm

1. Tạo nhân vật người chơi và một loại quái vật ở mức prototype. Có thể dùng model hoặc hình khối tạm; ưu tiên hành vi và tương tác.
2. Tạo hệ thống máu và sát thương cơ bản. Nhân vật và quái vật có thể nhận sát thương; khi máu về 0 thì có trạng thái chết rõ ràng.
3. Cho người chơi chọn và trang bị một trong hai vũ khí: súng hoặc kiếm. Chỉ cần đổi vũ khí trực tiếp bằng phím/nút đơn giản; chưa làm túi đồ, nhặt đồ hay quản lý nhiều vật phẩm.
4. Làm cách tấn công cơ bản phù hợp với từng vũ khí: kiếm đánh cự ly gần, súng đánh mục tiêu ở xa. Có thể dùng hiệu ứng/hình khối tạm cho đạn và đòn đánh.
5. Cho quái vật phản hồi khi bị đánh và có cách tấn công hoặc gây sát thương lên người chơi để kiểm tra tương tác hai chiều.
6. Tạo Scene thử riêng `CharacterCombatTest`; không cần chỉnh Scene `Game` chính trong giai đoạn prototype.

### Tiêu chí hoàn thành

- Có thể vào Play Mode và điều khiển nhân vật.
- Có thể đổi giữa súng và kiếm, sau đó tấn công quái.
- Sát thương làm giảm máu; khi máu quái về 0, quái chuyển sang trạng thái chết hoặc biến mất.
- Người chơi có thể bị quái gây sát thương.
- Các giá trị như máu, sát thương và tầm đánh có thể chỉnh trong Inspector.

### Bàn giao

- Prefab nhân vật, quái vật, súng và kiếm (dùng placeholder được nếu chưa có asset cuối).
- Script liên quan đến máu, sát thương, tấn công, chết và chọn vũ khí.
- Scene `CharacterCombatTest` để nhóm có thể tự chạy thử.
- Hướng dẫn ngắn về phím điều khiển, cách thử và các giá trị cần chỉnh; ghi rõ phần nào còn tạm thời.
- PR từ `feature/character-combat` về `main`, mô tả cách kiểm tra trong Unity.

## Đạt — Format room và sinh màn

**Nhánh:** `feature/map-generation`

### Phạm vi ưu tiên

Một màn chơi được ghép từ nhiều room nối với nhau. Ở giai đoạn này, tập trung làm format room, sinh bố cục gồm nhiều room và bảo đảm các room nối được thành một màn hoàn chỉnh. Chưa cần làm phức tạp địa hình, vật thể hay chi tiết bên trong từng room; có thể dùng room mẫu đơn giản hoặc placeholder để kiểm tra luồng sinh và kết nối.

### Việc cần làm

1. Nghiên cứu và chọn format để mô tả một room cùng các điểm/cửa kết nối của room. Ghi rõ dữ liệu nào cần có để đặt room và nối room với nhau.
2. Định nghĩa dữ liệu tối thiểu cho một màn gồm nhiều room: danh sách room, vị trí/chiều xoay của từng room, các cửa kết nối và điểm bắt đầu/đích.
3. Tạo bộ sinh prototype để chọn và bố trí nhiều room, sau đó nối các cửa tương ứng thành một bố cục hợp lệ.
4. Bảo đảm các kết nối không bị hở hoặc đè sai vị trí và người chơi có thể đi từ room bắt đầu đến room đích. Ưu tiên kết nối hoạt động trước; chưa cần tối ưu độ đa dạng hay làm nội dung bên trong room phong phú.
5. Tạo Scene thử riêng `MapGenTest`, hiển thị rõ từng room, các kết nối và tuyến đường tổng thể; thêm cách sinh lại bố cục để kiểm tra.
6. Thống nhất cách cung cấp điểm bắt đầu, đích và dữ liệu/vị trí room cho các hệ thống khác dùng khi tích hợp.

### Tiêu chí hoàn thành

- Có tài liệu mô tả format room, cửa kết nối và cách một màn được tạo từ nhiều room.
- Có thể sinh một màn gồm nhiều room được nối đúng với nhau.
- Có đường đi từ room bắt đầu đến room đích.
- Scene thử cho phép quan sát kết quả và sinh lại màn.
- Nội thất room có thể còn đơn giản hoặc dùng placeholder; đây không phải tiêu chí hoàn thành của prototype này.

### Bàn giao

- Tài liệu format room và quy tắc kết nối.
- Script/bộ sinh bố cục room cùng dữ liệu room mẫu cần thiết.
- Scene `MapGenTest` để nhóm chạy thử.
- Mô tả cách lấy điểm bắt đầu, đích và dữ liệu kết nối giữa các room; ghi rõ giới hạn còn tồn tại.
- PR từ `feature/map-generation` về `main`, mô tả cách chạy thử và cách kiểm tra các room đã nối đúng.
