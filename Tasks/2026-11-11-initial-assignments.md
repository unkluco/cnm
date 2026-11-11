# Phân công ban đầu — 2026-11-11

Mục tiêu của đợt này là tạo hai prototype độc lập, có thể kiểm tra trong Unity và ghép vào Scene chơi chính sau khi được review.

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

## Đạt — Format địa hình và sinh màn

**Nhánh:** `feature/map-generation`

### Việc cần làm

1. Nghiên cứu và chọn cách biểu diễn màn chơi phù hợp cho prototype, ví dụ lưới ô hoặc các đoạn/chunk ghép lại. Ghi rõ lý do chọn và giới hạn của format.
2. Định nghĩa dữ liệu tối thiểu của một màn: kích thước, loại địa hình/ô, chướng ngại vật, vị trí bắt đầu, vị trí đích và các điểm có thể sinh quái.
3. Tạo bộ sinh màn prototype có thể tạo các bố cục khác nhau từ seed. Cùng một seed cần tạo lại cùng một bố cục để tiện tái hiện lỗi.
4. Bảo đảm màn có điểm bắt đầu và đích đến, đồng thời có đường đi hợp lệ giữa chúng. Bố trí chướng ngại vật và điểm sinh quái sao cho có thể kiểm tra được.
5. Tạo Scene thử riêng `MapGenTest`, có cách sinh lại màn với seed khác và nhìn thấy rõ các điểm bắt đầu/đích/spawn.
6. Thống nhất cách trả dữ liệu hoặc các điểm spawn để nhánh nhân vật/chiến đấu có thể đặt người chơi và quái vào màn mà không phụ thuộc vào Scene thử của Đạt.

### Tiêu chí hoàn thành

- Có tài liệu mô tả format và cách cấu hình màn.
- Có thể sinh ít nhất vài bố cục khác nhau; cùng seed cho kết quả lặp lại được.
- Mỗi màn thử có bắt đầu, đích và đường đi hợp lệ giữa hai điểm.
- Scene thử cho phép xem kết quả và sinh lại màn.

### Bàn giao

- Tài liệu format dữ liệu màn và hướng dẫn cấu hình.
- Script/bộ sinh màn cùng dữ liệu cấu hình cần thiết.
- Scene `MapGenTest` để nhóm chạy thử.
- Mô tả cách lấy vị trí bắt đầu, đích và điểm sinh quái; ghi rõ giới hạn hoặc vấn đề còn tồn tại.
- PR từ `feature/map-generation` về `main`, mô tả cách chạy thử và seed dùng để tái hiện kết quả.
