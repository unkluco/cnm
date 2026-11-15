using LightSouls.Common;
using UnityEngine;

namespace LightSouls.Lobby.Testing
{
    /// <summary>
    /// Vật phẩm dùng riêng cho thử nghiệm cửa hàng ở Sảnh khi phần của Khải chưa merge.
    /// Kế thừa ItemDefinitionSO từ LightSouls.Common.
    /// </summary>
    [CreateAssetMenu(fileName = "TestItem", menuName = "LightSouls/Lobby/Testing/Test Item")]
    public class TestItemDefinitionSO : ItemDefinitionSO
    {
        [SerializeField] private ItemCategory category = ItemCategory.Weapon;

        public override ItemCategory Category => category;

        public void Configure(string id, string name, string desc, int price, ItemCategory cat)
        {
            // Cho phép cấu hình qua code nếu cần
            category = cat;
        }
    }
}
