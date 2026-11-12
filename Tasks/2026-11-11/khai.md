# Khải — Nhân vật và chiến đấu (2026-11-11)

**Nhánh:** `feature/character-combat`

Mục tiêu của đợt này là tạo hai prototype độc lập, có thể kiểm tra trong Unity và ghép vào Scene chơi chính sau khi được review.

> **Quy tắc nhánh:** Không phát triển hoặc commit trực tiếp lên `main` hay `develop`. Mỗi người chỉ làm trên nhánh tính năng được phân công (bao gồm người phụ trách giao diện); thay đổi được gửi bằng PR về `develop`.

## Việc cần làm

1. Tạo nhân vật người chơi và một loại quái vật ở mức prototype. Có thể dùng model hoặc hình khối tạm; ưu tiên hành vi và tương tác.
2. Tạo hệ thống máu và sát thương cơ bản. Nhân vật và quái vật có thể nhận sát thương; khi máu về 0 thì có trạng thái chết rõ ràng.
3. Cho người chơi chọn và trang bị một trong hai vũ khí: súng hoặc kiếm. Chỉ cần đổi vũ khí trực tiếp bằng phím/nút đơn giản; chưa làm túi đồ, nhặt đồ hay quản lý nhiều vật phẩm.
4. Làm cách tấn công cơ bản phù hợp với từng vũ khí: kiếm đánh cự ly gần, súng đánh mục tiêu ở xa. Có thể dùng hiệu ứng/hình khối tạm cho đạn và đòn đánh.
5. Cho quái vật phản hồi khi bị đánh và có cách tấn công hoặc gây sát thương lên người chơi để kiểm tra tương tác hai chiều.
6. Tạo Scene thử riêng `CharacterCombatTest`; không cần chỉnh Scene `Game` chính trong giai đoạn prototype.

## Tiêu chí hoàn thành

- Có thể vào Play Mode và điều khiển nhân vật.
- Có thể đổi giữa súng và kiếm, sau đó tấn công quái.
- Sát thương làm giảm máu; khi máu quái về 0, quái chuyển sang trạng thái chết hoặc biến mất.
- Người chơi có thể bị quái gây sát thương.
- Các giá trị như máu, sát thương và tầm đánh có thể chỉnh trong Inspector.

## Bàn giao

- Prefab nhân vật, quái vật, súng và kiếm (dùng placeholder được nếu chưa có asset cuối).
- Script liên quan đến máu, sát thương, tấn công, chết và chọn vũ khí.
- Scene `CharacterCombatTest` để nhóm có thể tự chạy thử.
- Hướng dẫn ngắn về phím điều khiển, cách thử và các giá trị cần chỉnh; ghi rõ phần nào còn tạm thời.
- PR từ `feature/character-combat` về `develop`, mô tả cách kiểm tra trong Unity.
