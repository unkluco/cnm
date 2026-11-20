# Light Souls — Dark Fantasy Low-Poly

## Định hướng hình ảnh

- Hầm ngục bằng đá xanh xám, các mặt phẳng có cạnh vát và đường ron gạch; không dùng mặt phẳng màu đơn để phân biệt phòng.
- Đuốc và kim loại đồng/vàng tạo điểm sáng ấm; ma thuật của hiệp sĩ dùng xanh ngọc, cổng bí ẩn dùng tím. Các bẫy và loại quái vẫn giữ tín hiệu màu riêng để dễ đọc khi chơi trên điện thoại.
- Nhân vật và vật phẩm dùng hình khối ít đa giác, có hình dáng nhận biết từ camera trên cao. Menu, HUD, ô vật phẩm và các bảng trong game dùng cùng ngôn ngữ viền đá/kim loại.

## Tổ chức asset

| Đường dẫn | Nội dung |
| --- | --- |
| `Assets/Art/DarkFantasy/Materials` | Vật liệu đá, kim loại, da, vải, ánh lửa và tinh thể |
| `Assets/Art/DarkFantasy/BatchedMeshes` | Mesh trang trí sàn, tường, bẫy được gộp theo vật liệu |
| `Assets/Art/DarkFantasy/Weapons` | Mô hình vũ khí gắn vào socket trên tay |
| `Assets/Art/DarkFantasy/Props` | Prefab đuốc dùng chung, gồm lửa/hạt sáng và đèn |
| `Assets/Art/DarkFantasy/Icons` | Sprite túi đồ/cửa hàng render từ mô hình low-poly |
| `Assets/Resources/Art/DarkFantasy` | Mesh dùng chung lúc chạy và sprite UI |
| `Assets/Scripts/Art` | Điều phối đèn, hiệu ứng dao động ánh lửa/cổng |
| `Assets/Scripts/Common/DarkFantasyTheme.cs` | Màu sắc và khung UI dùng chung |

Các prefab và scene trong dự án đã được phối sẵn, vì vậy không cần chạy công cụ tạo asset trước khi chơi. Khi cần tái dựng toàn bộ bộ đồ họa hiện tại, dùng **Light Souls → Art → Rebuild Dark Fantasy art** trong Unity Editor. Công cụ gắn lại các nhóm trang trí `Art_...` và giữ nguyên các anchor điều khiển gameplay; không chạy nó đè lên một prefab đang sửa thủ công nếu chưa lưu các chỉnh sửa riêng.

Icon vật phẩm là ảnh trong suốt 192×192, render hai lượt (màu và mặt nạ hình học) từ mô hình thật. Dùng **Light Souls → Art → Repair item meshes and icons** để sửa/tạo lại các mesh dùng chung và icon mà không dựng lại scene; **Recapture item icons** chỉ chụp lại ảnh từ tài nguyên đã lưu. Mặt đáy của các khối lăng trụ/lưỡi kiếm không được đảo thứ tự mảng đỉnh nguồn; mặt bên phải nối đúng các vòng đỉnh liên tiếp để không khuyết hình. Công cụ kiểm tra viền ảnh và bảo toàn GUID của sprite/mesh đang được prefab tham chiếu.

## Khi thêm phòng, quái và vũ khí

1. Giữ lại collider, điểm spawn, door socket, trigger, prefab/script và các tham số gameplay. Các chi tiết `Art_...` chỉ để hiển thị. Đối với địa hình, mặt sàn trang trí phải thấp hơn mặt bẫy để không che dấu lava/nước/bẫy.
2. Dùng vật liệu chung thay vì tạo vật liệu mới cho từng viên gạch. Gộp mesh tĩnh theo vật liệu trong một phòng; mặt đá chỉ có mặt ngoài và cạnh vát nhìn thấy, bỏ các mặt khuất để giảm số đa giác. Ưu tiên ít draw call.
3. Đèn đuốc runtime dùng `DarkFantasyTorchLight`. Trong Game, `DarkFantasyLightBudget` bật tối đa 4 đèn điểm gần người chơi; Lobby dùng `FixedSceneLighting` giữ toàn bộ đèn của sảnh luôn bật, không phụ thuộc vị trí nhân vật. Đèn điểm không đổ bóng; bóng chính đến từ directional light. Không thêm nhiều đèn runtime bên ngoài bộ điều phối.
4. Cổng tại phòng boss là cổng bí ẩn trung tính; không đổi màu theo điểm đến. Các dấu hiệu khóa/mở cửa, vùng ngắm, hiệu ứng nguy hiểm và khung độ hiếm phải giữ nguyên ý nghĩa.
5. UI ưu tiên kích thước nút/độ tương phản hiện có để chạm trên điện thoại. Dùng `DarkFantasyTheme` cho các bảng dựng khi chạy.

## Kiểm tra

Các bài kiểm thử EditMode xác nhận bộ phòng, mô hình vũ khí, khung UI và điểm nối cửa; bài PlayMode kiểm tra luồng MainMenu → Lobby → Game, giới hạn đèn, sinh dungeon và mở cổng boss. Có thể đặt biến môi trường `LIGHTSOULS_ART_CAPTURE_DIR` để bài PlayMode đồ họa lưu ảnh PNG kiểm tra trực quan khi chạy Unity có thiết bị đồ họa.
