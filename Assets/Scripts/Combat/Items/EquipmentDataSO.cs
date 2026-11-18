using System.Collections.Generic;
using System.Text;
using UnityEngine;
using LightSouls.Common;
using LightSouls.Combat.Stats;

namespace LightSouls.Combat.Items
{
    /// <summary>
    /// Trang bị mặc vào ô Giáp hoặc Giày. Khi mặc, mọi chỉ số (GetModifiers) được đăng ký vào PlayerStats
    /// dưới khóa là ô trang bị, nên tháo ra là tự gỡ. Chỉ số đặc biệt (kháng dung nham, lướt xa...) thêm qua extraModifiers,
    /// không cần viết code.
    /// </summary>
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

        [Tooltip("Hệ số tốc độ chạy (1.25 = nhanh hơn 25%, 0.95 = chậm hơn 5%).")]
        [Range(0.5f, 2.5f)]
        [SerializeField] private float speedMultiplier = 1.25f;

        [Tooltip("Lượng máu tối đa cộng thêm.")]
        [SerializeField] private float bonusMaxHealth = 0f;

        [Tooltip("Chỉ số đặc biệt khác (kháng dung nham, hồi lướt nhanh, lướt xa, miễn bẫy gai...).")]
        [SerializeField] private List<StatModifier> extraModifiers = new List<StatModifier>();

        [Header("Hiệu ứng")]
        [Tooltip("Hào quang trên người khi mặc (alpha 0 = không có).")]
        [SerializeField] private Color auraColor = new Color(1f, 1f, 1f, 0f);

        public EquipmentSlotType SlotType => slotType;
        public float DamageReduction => damageReduction;
        public float SpeedMultiplier => speedMultiplier;
        public float BonusMaxHealth => bonusMaxHealth;
        public IReadOnlyList<StatModifier> ExtraModifiers => extraModifiers;
        public Color AuraColor => auraColor;

        /// <summary>Toàn bộ hệ số chỉ số của trang bị này, để đăng ký vào PlayerStats.</summary>
        public IEnumerable<StatModifier> GetModifiers()
        {
            if (damageReduction > 0f)
            {
                yield return new StatModifier(StatType.DamageTaken, 1f - damageReduction);
            }

            if (!Mathf.Approximately(speedMultiplier, 1f))
            {
                yield return new StatModifier(StatType.MoveSpeed, speedMultiplier);
            }

            foreach (StatModifier modifier in extraModifiers)
            {
                yield return modifier;
            }
        }

        public override string GetStatsText()
        {
            var sb = new StringBuilder(slotType == EquipmentSlotType.Boots ? "Giày" : "Giáp");
            if (damageReduction > 0f) sb.Append("\nGiảm sát thương: ").Append(Mathf.RoundToInt(damageReduction * 100f)).Append("%");
            if (!Mathf.Approximately(speedMultiplier, 1f)) sb.Append("\n").Append(StatNames.Describe(StatType.MoveSpeed, speedMultiplier));
            if (bonusMaxHealth > 0f) sb.Append("\nMáu tối đa +").Append(bonusMaxHealth.ToString("0"));
            foreach (StatModifier modifier in extraModifiers)
            {
                sb.Append("\n• ").Append(modifier.Describe());
            }

            return sb.ToString();
        }
    }
}
