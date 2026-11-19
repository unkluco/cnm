# Cổng bí ẩn sau phòng boss

- Khi phòng boss chưa được dọn sạch, cổng chưa hiện và không thể chuyển màn.
- Khi dọn sạch, hòm thưởng xuất hiện như cũ và cổng bí ẩn mở. Mọi cổng đều có cùng màu và hiệu ứng, không hiện đích đến. Nếu người chơi đứng ngay chỗ cổng khi nó mở thì cần rời khỏi vòng và bước lại vào, để có thời gian lấy đồ.
- Khi bước vào vòng cổng, game mới quay xác suất (mặc định 50% về sảnh mua đồ, 50% sang loại ngục khác). Tỉ lệ `Return To Shop Chance` chỉnh được trong Inspector của `GameBootstrap` tại Scene `Game`.
- Nếu sang ngục khác, game chọn từ `DungeonCatalog` trừ ngục đang chơi, tải lại Scene `Game` và sinh bố cục mới. Nếu catalog không có ngục hợp lệ khác, cổng đưa người chơi về sảnh an toàn.
- Thông báo tên điểm đến chỉ hiện sau khi Scene mới đã nạp. Không có màn chiến thắng xen giữa; người chơi tiếp tục lượt chơi.
- Xu và toàn bộ túi đồ được giữ lại khi đi qua cổng. Chết vẫn hiện màn thất bại và bắt đầu lại lượt chơi theo quy tắc cũ.
