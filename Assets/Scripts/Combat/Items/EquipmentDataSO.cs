using UnityEngine;
using LightSouls.Common;

namespace LightSouls.Combat.Items
{
    [CreateAssetMenu(fileName = "Equip_", menuName = "LightSouls/Combat/Equipment Data")]
    public class EquipmentDataSO : ItemDefinitionSO
    {
        public override ItemCategory Category => ItemCategory.Equipment;

        [Header("Loại ô trang bị")]
        [SerializeField] private EquipmentSlotType slotType = EquipmentSlotType.Armor;

        [Header("Chỉ số cộng thêm")]
        [Tooltip("Tỉ lệ giảm sát thương (0.2 = giảm 20% sát thương nhận vào). Dùng cho Giáp.")]
        [Range(0f, 0.8f)]
        [SerializeField] private float damageReduction = 0.2f;

        [Tooltip("Hệ số tăng tốc chạy (1.25 = chạy nhanh hơn 25%). Dùng cho Giày.")]
        [Range(1f, 2.5f)]
        [SerializeField] private float speedMultiplier = 1.25f;

        [Tooltip("Lượng máu tối đa cộng thêm.")]
        [SerializeField] private float bonusMaxHealth = 0f;

        public EquipmentSlotType SlotType => slotType;
        public float DamageReduction => damageReduction;
        public float SpeedMultiplier => speedMultiplier;
        public float BonusMaxHealth => bonusMaxHealth;
    }
}
