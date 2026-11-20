# Light Souls — Kho vũ khí, nhật ký hầm ngục và cân bằng

## Màn chọn hầm ngục

`DungeonSelectUI` là nhật ký ba thẻ có ảnh render từ chính prefab địa hình: thư viện cổ, cầu hỏa ngục, mê cung rêu phong. Thẻ hiện cấp độ nguy hiểm, đặc trưng, số phòng mục tiêu và hệ số thử thách. Chạm thẻ chỉ chọn; nút **Bước vào** mới phát `DungeonChosen`. Bảng dùng `BackButtonStack`, hỗ trợ đóng/quay lại và cuộn ngang nếu danh mục có thêm vùng.

## Nội dung mới

- 30 vũ khí (18 cận chiến, 12 tầm xa): búa, chùy, kiếm dài, dao găm, kiếm cong, lưỡi hái, giáo, rìu, kích, liên chùy, rapier, song đao, trượng, nỏ, quả cầu phép, đèn lồng, ấn trụ, cung, đũa phép và phiến vũ.
- 30 vật phẩm ngoài vũ khí: 12 nguyên liệu theo vùng, 8 giáp/giày và 10 dược phẩm. Mỗi mục có asset, icon render riêng và đăng ký trong `ItemDatabase`.
- 48 công thức mới, gồm toàn bộ vũ khí, giáp/giày và dược phẩm. Nguyên liệu mới có nguồn thưởng phòng/hòm, một số bán ở sảnh. Vũ khí huyền thoại mới chỉ mở khóa qua chế tạo có Linh hồn thủ vệ, không xuất hiện trực tiếp trong shop/hòm.
- Trang bị mới đánh đổi khả năng phòng hộ, tốc độ di chuyển, sát thương, thời gian hồi đòn/lướt và chống địa hình. Dược phẩm có nhóm hồi máu, tăng lực/phòng hộ, kháng địa hình và thanh tẩy.

## Kỹ năng chiến thuật

| Cơ chế | Quy tắc |
| --- | --- |
| Đẩy/kéo | Dịch quái thường trên NavMesh, giới hạn khoảng cách và va chạm; không đẩy boss qua tường. |
| Đóng băng | Dừng hành động của quái thường trong thời gian ngắn; boss chỉ bị làm chậm nhẹ. Có khoảng kháng khống chế sau choáng. |
| Bước Ảnh | Dịch chuyển ngắn khi đánh, không xuyên cửa khóa/tường và phải có điểm đến đi bộ an toàn. |
| Vách đá / băng | Vật cản thật có BoxCollider và NavMeshObstacle; tối đa một vách, tự tan theo thời hạn hoặc khi hết giao tranh. Không đặt ở lối hẹp/hazard hoặc đè lên nhân vật. |
| Thôi miên | Chỉ quái nhỏ đang có ít máu, một đồng minh tối đa, không boss hoặc quái hạng nặng. Đồng minh đánh quái địch, không bị người chơi ngắm vào. Nếu là người sống sót cuối cùng, đồng minh rời đi và phòng vẫn được mở khóa. |
| Sét phân nhánh / Phá phòng thủ | Sét trúng tối đa hai mục tiêu bổ sung trong tầm nhìn; phá phòng thủ tăng tạm sát thương nhận, không cộng chồng. |
| Hộ ấn | Giảm tạm 28% sát thương nhận khi kích hoạt, có thời gian hồi độc lập. |

`WeaponTactics` và `TacticalHitEffectSO` bảo vệ các hồi chiêu theo nhóm năng lực; không thể đổi sang vũ khí cùng hiệu ứng để spam. Các loại hiệu ứng khống chế giới hạn trên một lần kích hoạt, không cộng lặp bởi từng viên đạn.

## Cân bằng

- Tất cả 15 vũ khí cũ được xem lại: sát thương cơ bản, thời gian hồi đòn, đòn nhiều nhát và đạn tán xạ. Kho mới có các lựa chọn đánh đổi giữa DPS một mục tiêu và khả năng khống chế/hỗ trợ.
- Hồi đòn được lưu trong `RunState`, dùng chung nhịp hồi giữa các vũ khí khi đổi ô, đồng thời theo dõi từng vũ khí. Dược phẩm có thời gian hồi dùng chung; các kỹ năng chiến thuật có hồi riêng. HUD và hotbar hiện số giây còn lại.
- Ngục Tiêu Chuẩn / Vực Dung Nham / Mê Cung có hệ số sức sống quái 1,22 / 1,36 / 1,49 và sát thương 1,12 / 1,20 / 1,28 trước khi cộng tiến triển. Độ sâu phòng, số ngục đã vào và số phòng đã vượt tăng tiếp hệ số với mức trần. Boss được áp dụng sức sống/sát thương riêng nhưng giữ thời gian báo đòn rõ ràng.
- Lướt có hồi cơ bản 1,85 giây. Bình máu thường hồi 28, bình lớn 45% máu tối đa. Thuốc tăng lực có thời hạn ngắn hơn và chỉ số cộng dồn có trần. Chuyển trực tiếp qua ngục khác bằng cổng bí ẩn giữ tỷ lệ máu còn lại; về sảnh được hồi phục.
- Quái cận chiến/tầm xa khóa vùng báo chiêu từ lúc bắt đầu; người chơi có thể né. Lướt và sát thương có khoảng hồi công bằng, giảm việc bị nhiều quái gây sát thương cùng một khung hình.

## Hoạt ảnh và bảo trì

Melee swing sử dụng đúng tư thế ban đầu của mesh vũ khí, chém nhiều nhát không bỏ lại trail. Vũ khí tầm xa có giật lùi rồi trở về tư thế gốc. Đổi vũ khí/hủy đòn dừng coroutine cũ, cảnh báo và hạt liên quan; đóng băng/thôi miên hủy đòn đang chuẩn bị của quái. Chân/tay/thân dùng rotation gốc của rig khi chạy, đứng và lướt thay vì đặt về identity sai lệch.

Có thể tái tạo bộ asset với **Light Souls → Art → Build Arsenal and Dungeon Journal** (cần thiết bị đồ họa để render icon). Các asset đã được lưu; không cần chạy builder trước khi chơi. Builder tổng quát không ghi đè các mô hình arsenal.
