using System;

namespace LightSouls.Common
{
    /// <summary>
    /// Túi đồ của người chơi, nhìn từ bên ngoài. Hệ thống chiến đấu (Khải) cài đặt interface này
    /// trên đối tượng gốc của nhân vật người chơi (tag Player).
    /// Hệ thống khác (cửa hàng, rương, phần thưởng) tìm nó bằng GetComponent&lt;IInventory&gt;() trên người chơi
    /// và chỉ dùng các hàm dưới đây; cách xếp đồ vào thanh nhanh, balo hay ô trang bị là việc bên trong túi đồ.
    /// </summary>
    public interface IInventory
    {
        /// <summary>True nếu thêm được đủ count món mà không phải bỏ món nào.</summary>
        bool HasSpaceFor(ItemDefinitionSO item, int count = 1);

        /// <summary>Thêm đủ count món, hoặc không thêm gì và trả về false khi không đủ chỗ.</summary>
        bool TryAdd(ItemDefinitionSO item, int count = 1);

        /// <summary>Bớt đủ count món, hoặc không bớt gì và trả về false khi không đủ.</summary>
        bool TryRemove(ItemDefinitionSO item, int count = 1);

        /// <summary>Tổng số món này trong mọi ô (thanh nhanh, balo, ô trang bị).</summary>
        int GetCount(ItemDefinitionSO item);

        /// <summary>Phát mỗi khi nội dung túi đồ thay đổi.</summary>
        event Action Changed;
    }
}
