using UnityEngine;

namespace LightSouls.Common
{
    /// <summary>Nhóm vật phẩm. Chỉ thêm giá trị mới vào cuối, không đổi thứ tự (Unity lưu enum dạng số).</summary>
    public enum ItemCategory
    {
        Weapon,
        Equipment,
        Consumable,
        Misc,
    }

    /// <summary>
    /// Lớp gốc của mọi vật phẩm (vũ khí, trang bị, đồ dùng). Mỗi vật phẩm là một asset ScriptableObject.
    /// Hệ thống chiến đấu (Khải) tạo các lớp con, ví dụ WeaponDataSO kế thừa lớp này.
    /// Hệ thống khác (cửa hàng ở sảnh, rương, phần thưởng) chỉ làm việc với lớp gốc này,
    /// nên không cần tham chiếu LightSouls.Combat.
    /// </summary>
    public abstract class ItemDefinitionSO : ScriptableObject
    {
        [Tooltip("Mã định danh duy nhất, không đổi sau khi đã dùng. Dùng để lưu túi đồ giữa các Scene. Ví dụ: weapon.iron_sword")]
        [SerializeField] private string itemId;

        [SerializeField] private string displayName;

        [SerializeField, TextArea] private string description;

        [SerializeField] private Sprite icon;

        [Tooltip("Giá mua ở cửa hàng, tính bằng xu.")]
        [SerializeField, Min(0)] private int price;

        [Tooltip("Số lượng tối đa trong một ô. Vũ khí và trang bị là 1.")]
        [SerializeField, Min(1)] private int maxStack = 1;

        public string ItemId => itemId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public int Price => price;
        public int MaxStack => maxStack;

        public abstract ItemCategory Category { get; }
    }
}
