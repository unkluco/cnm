using UnityEngine;

namespace LightSouls.Combat.Items
{
    /// <summary>Thông tin một lần dùng vật phẩm tiêu hao.</summary>
    public struct ConsumableUseContext
    {
        public GameObject User;
        public ConsumableDataSO Item;
        /// <summary>Hướng ngắm (về phía kẻ địch gần nhất nếu có, không thì hướng nhân vật đang nhìn).</summary>
        public Vector3 AimDirection;
        /// <summary>Vị trí kẻ địch gần nhất trong tầm, nếu có.</summary>
        public Vector3? AimTarget;
        /// <summary>True khi được kích hoạt tự động lúc chết (Lông Phượng Hoàng).</summary>
        public bool TriggeredByDeath;
    }

    /// <summary>
    /// Một tác dụng của vật phẩm tiêu hao (hồi máu, tăng lực, ném bom, dịch chuyển...).
    /// Một vật phẩm có danh sách tác dụng, chạy lần lượt. Tác dụng là asset nên dùng chung được giữa nhiều vật phẩm.
    /// Thêm tác dụng mới: tạo lớp con, ghi đè Apply/Describe (và CanApply nếu có điều kiện), tạo asset từ menu Create.
    /// </summary>
    public abstract class ConsumableEffectSO : ScriptableObject
    {
        /// <summary>
        /// Có dùng được lúc này không. Trả false thì vật phẩm không bị tiêu hao; reason là câu báo cho người chơi.
        /// </summary>
        public virtual bool CanApply(ConsumableUseContext context, out string reason)
        {
            reason = null;
            return true;
        }

        public abstract void Apply(ConsumableUseContext context);

        /// <summary>Một dòng mô tả cho bảng chi tiết vật phẩm.</summary>
        public abstract string Describe();
    }
}
