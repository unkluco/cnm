# Light Souls — Lò rèn Ánh Tàn

## Cách chơi

- Lò rèn đặt ở chính giữa phía sau sảnh, giữa khu cửa hàng và cổng vào ngục. Đến vòng linh văn phía trước rồi bấm **Chế tạo** (hoặc `E`).
- Chọn công thức trong các nhóm Vũ khí, Trang bị, Tiêu hao hoặc Tinh luyện. Màn hình cho biết nguyên liệu đang có / cần, độ hiếm và thành phẩm. Có thể chọn chế tạo từ 1 đến 20 lượt cùng lúc.
- Công thức nâng cấp tiêu thụ món cũ và trả món mới. Nếu nâng cấp vũ khí ở thanh nhanh, hệ thống ưu tiên đặt thành phẩm vào ô vừa dùng. Giáp hoặc giày đang mặc cần tháo ra trước khi dùng làm nguyên liệu.
- Vật phẩm nguyên liệu không chiếm 15 ô balo hay 5 ô thanh nhanh: mỗi loại có ngăn riêng (tối đa 999). Mở balo và chọn **Nguyên liệu** để xem, hoặc dùng mục tương ứng trong giao diện hòm để chuyển nguyên liệu giữa túi và hòm.
- Người mới nhận 2 Gỗ Cổ, 3 Quặng Sắt, 2 Thảo Dược và 1 Mảnh Thủy Tinh, đủ thử một công thức rèn và một công thức giả kim. Chế tạo không thu xu; xu dành cho cửa hàng.

## Nội dung ban đầu

20 nguyên liệu, 31 công thức: 15 vũ khí, 8 trang bị phòng hộ, 6 đồ tiêu hao, 2 công thức tinh luyện. Ví dụ về chi phí **trong trò chơi**:

| Thành phẩm | Chi phí | Nhóm |
| --- | --- | --- |
| Kiếm Sắt | 2 Gỗ Cổ + 3 Quặng Sắt | Chế tạo |
| Kiếm Lửa | 1 Kiếm Sắt + 1 Đá Lửa | Nâng cấp |
| Trượng Băng | 2 Gỗ Cổ + 2 Tinh Thạch Băng + 1 Bụi Linh Hồn | Chế tạo |
| Giáp Chịu Nhiệt | 1 Giáp Xích + 2 Đá Lửa + 1 Linh Hồn Thủ Vệ | Nâng cấp |
| Bình hồi máu | 2 Thảo Dược + 1 Mảnh Thủy Tinh | Giả kim |
| 2 Thép Tinh Luyện | 3 Quặng Sắt + 1 Than Hồng | Tinh luyện |

**Nguồn nguyên liệu:** hoàn thành một phòng chiến đấu sẽ thả một phần thưởng cơ bản và một lần quay nguyên liệu bổ sung, với trọng số theo loại ngục. Boss luôn thả một Linh Hồn Thủ Vệ cùng nguyên liệu đặc trưng của mình: Varkhan (Đá Lửa), Nhalis (Tinh Thạch Băng), Orgrim (Thép Tinh Luyện), Noctyr (Tinh Thể Hư Không), Aurex (Mảnh Lôi Điện). Các nguyên liệu thông thường còn có thể tìm trong hòm; cửa hàng bán 9 loại cơ bản. Mục **Nguyên liệu** ở lò rèn giải thích nguồn và công thức sử dụng từng loại.

## Cách mở rộng cho nhóm

- `CraftMaterialSO` mô tả nguyên liệu; `CraftingCatalogSO` chứa danh sách công thức dùng chung cho giao diện rèn và phần thưởng vượt phòng. Catalog được tải từ `Resources/Combat/Crafting/CraftingCatalog`.
- Asset nguyên liệu: `Assets/ScriptableObjects/Combat/Materials`. Khi thêm loại mới, đặt `ItemId` ổn định dạng `material.xxx`, biểu tượng, độ hiếm, nguồn và giới hạn ngăn; đăng ký với ItemDatabase để lưu qua các scene.
- Công thức có ID riêng, loại, thành phẩm, số lượng và danh sách nguyên liệu. Dùng ItemDefinitionSO hiện có (vũ khí, giáp, giày, đồ dùng hoặc nguyên liệu). Không cần viết riêng mã cho từng công thức.
- Công cụ `Light Souls > Art > Build Crafting Forge` dựng lại danh mục gốc, biểu tượng và prefab lò rèn từ dữ liệu tác giả trong `DarkFantasyCraftingBuilder.cs`. Khi chỉnh danh mục gốc, cập nhật dữ liệu ở công cụ này để tránh bị ghi đè khi tái tạo. Không chạy lại công cụ chỉ để chỉnh một công thức trong Inspector.
- `PlayerInventory` triển khai `ICraftingInventory` để Lobby không phụ thuộc vào Combat. `EvaluateCraft` lập kế hoạch trên bản chụp các ô; `TryCraft` chỉ cập nhật khi tất cả nguyên liệu và chỗ chứa thành phẩm đều hợp lệ. Save `RunState` và phát sự kiện Changed một lần mỗi giao dịch; nguyên liệu không bị mất khi không đủ chỗ.
- Lò rèn dùng các mesh ghép theo vật liệu và một nguồn sáng tham gia ngân sách đèn chung của Dark Fantasy. Biểu tượng tài nguyên dùng hai lượt render màu và mặt nạ hình học; cả 20 sprite trong suốt kích thước 192×192 được kiểm tra không cắt sát viền.

## Kiểm thử

- EditMode: danh mục và nguồn nguyên liệu, độ hiếm/biểu tượng, chi phí công thức mẫu, phần thưởng đủ 5 boss, prefab và ngân sách đèn.
- PlayMode: tương tác lò rèn bằng chính PlayerInteraction, chế tạo và nâng cấp, balo đầy và giới hạn nguyên liệu, bảo vệ giáp đang mặc, mua nguyên liệu, vật phẩm rơi sau khi vượt phòng, chuyển nguyên liệu qua hòm và lưu khi đổi scene.
