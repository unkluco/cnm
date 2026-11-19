# Light Souls — Rune & Ember Combat VFX

Tiếp nối Dark Fantasy Low-Poly: bề mặt đá tối làm nền để vệt kiếm sắc, lõi sáng, tàn lửa và pháp trận đọc rõ trên màn hình nhỏ. Hiệu ứng không làm thay đổi chỉ số sát thương, tầm đánh, thời gian hồi hay thời gian tụ chiêu của vũ khí/quái.

- Cận chiến: vệt cung có mép sáng và đầu thon, bóng lưỡi kiếm theo đúng đường chuyển động của mô hình, tia va chạm định hướng. Búa và đại kiếm có vòng xung kích riêng. Đòn trúng tạo phản hồi camera rất ngắn, không dừng thời gian.
- Nguyên tố: lửa có ngọn lửa, tàn lửa và khói nhẹ; băng có mảnh tinh thể, hạt lạnh; đá có bụi/mảnh văng; phép hư không, hút máu và hồi phục mang bảng màu riêng. Trạng thái đang bỏng/chậm/choáng dùng hạt liên tục đến khi hết trạng thái.
- Báo chiêu: viền ngoài chính xác, hoa văn rune bên trong và vệt tiến độ tụ chiêu. Vùng nguy hiểm có viền đỏ ấm; vùng hồi phục của tư tế có biểu tượng hỗ trợ, vòng tự ngắm của người chơi là xanh ngọc mờ. Các đường lao/bắn có mũi tên hướng về đích. Luôn dọn dấu báo chiêu khi giải chiêu, bị ngắt hoặc đối tượng bị hủy.
- Đạn có lõi màu và đuôi sáng thuôn, đuôi tan trong thời gian ngắn sau khi đạn biến mất. Lướt dùng bóng mờ hợp nhất thay vì hàng chục đối tượng/material cho mỗi bóng.

## Mở rộng

Chọn `Vfx.element` trong `WeaponDataSO` để đổi phong cách hình ảnh (Steel, Ember, Frost, Void, Storm, Stone, Life, Blood). Màu nhát chém, tia trúng, đuôi đạn và hào quang vẫn chỉnh trong cùng profile. `ArcaneTelegraph.Circle/Lane` nhận bán kính/độ rộng thật của chiêu; không dùng collider cho hiệu ứng. `VfxFactory.SpellImpact` là phần giải chiêu hiển thị, logic sát thương gọi riêng.

Shader ở `Assets/Resources/VFX/Arcane`, dùng chung material và property block; không thêm đèn động cho phép thuật. Dấu cảnh báo chỉ dùng mesh bốn đỉnh + viền, vệt kiếm có tối đa 18 mẫu, mỗi hệ hạt có giới hạn. Khi nhiều hiệu ứng đồng thời, giảm hạt phụ nhưng không giấu vùng cảnh báo. Các hiệu ứng ngắn tự dọn sau khi tan.

Kiểm thử bằng Unity CLI EditMode + PlayMode. Đặt biến môi trường `LIGHTSOULS_VFX_CAPTURE_DIR` khi chạy PlayMode có graphics để lưu các khung hình từ đòn đánh thật và quái đang tụ chiêu.
