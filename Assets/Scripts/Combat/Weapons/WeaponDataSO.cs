using System.Collections.Generic;
using System.Text;
using UnityEngine;
using LightSouls.Common;

namespace LightSouls.Combat.Weapons
{
    [CreateAssetMenu(fileName = "WeaponData_", menuName = "LightSouls/Combat/Weapon Data")]
    public class WeaponDataSO : ItemDefinitionSO
    {
        public override ItemCategory Category => ItemCategory.Weapon;

        [Header("Loại vũ khí")]
        [SerializeField] private WeaponType weaponType = WeaponType.Melee;

        [Header("Chỉ số chiến đấu chung")]
        [Tooltip("Lượng sát thương gây ra mỗi đòn.")]
        [SerializeField] private float damage = 20f;
        [Tooltip("Thời gian hồi giữa 2 lần tấn công (giây).")]
        [SerializeField] private float attackRate = 0.5f;
        [Tooltip("Tầm với tối đa của đòn đánh hoặc tầm ngắm hiệu quả.")]
        [SerializeField] private float attackRange = 2f;

        [Header("Hiển thị & Visual")]
        [Tooltip("Prefab hiển thị mô hình vũ khí khi trang bị trên tay.")]
        [SerializeField] private GameObject weaponPrefab;
        [Tooltip("Màu sắc định danh của vũ khí (dùng cho visual placeholder).")]
        [SerializeField] private Color weaponColor = Color.white;

        [Header("Thông số cận chiến (Melee Only)")]
        [Tooltip("Góc quét của nhát chém (độ).")]
        [Range(30f, 360f)]
        [SerializeField] private float swingArcAngle = 90f;
        [Tooltip("Bán kính vùng quét sát thương trước mặt.")]
        [SerializeField] private float hitRadius = 1.5f;
        [Tooltip("Lực đẩy lùi đối thủ khi trúng đòn.")]
        [SerializeField] private float knockbackForce = 2f;

        [Header("Thông số tầm xa (Ranged Only)")]
        [Tooltip("Prefab của viên đạn bắn ra.")]
        [SerializeField] private GameObject projectilePrefab;
        [Tooltip("Tốc độ bay của đạn (m/s).")]
        [SerializeField] private float projectileSpeed = 18f;
        [Tooltip("Thời gian tồn tại tối đa của đạn trước khi tự hủy (giây).")]
        [SerializeField] private float projectileLifetime = 3f;
        [Tooltip("Số lượng tia đạn bắn ra cùng lúc (1: bắn đơn, >1: shotgun).")]
        [Range(1, 12)]
        [SerializeField] private int projectileCount = 1;
        [Tooltip("Độ mở góc tản đạn khi bắn nhiều tia (độ).")]
        [Range(0f, 90f)]
        [SerializeField] private float spreadAngle = 25f;
        [Tooltip("Độ lệch ngẫu nhiên mỗi phát bắn (độ). Dùng cho súng bắn nhanh.")]
        [Range(0f, 30f)]
        [SerializeField] private float inaccuracy = 0f;
        [Tooltip("Giữ nút tấn công để bắn liên tục.")]
        [SerializeField] private bool autoFire = false;
        [Tooltip("Số mục tiêu đạn xuyên qua thêm trước khi biến mất (0 = dừng ở mục tiêu đầu).")]
        [Range(0, 10)]
        [SerializeField] private int pierceCount = 0;

        [Header("Đạn nổ (Ranged Only, bán kính > 0 thì đạn bay vòng cung và nổ)")]
        [SerializeField, Min(0f)] private float explosionRadius = 0f;
        [Tooltip("Vụ nổ có làm bị thương chính người bắn không.")]
        [SerializeField] private bool explosionDamagesOwner = false;
        [SerializeField, Min(0f)] private float arcHeight = 2.5f;

        [Header("Đòn đánh nhiều nhát (Melee Only)")]
        [Tooltip("Số nhát chém mỗi lần bấm (Song Dao = 2).")]
        [Range(1, 5)]
        [SerializeField] private int strikeCount = 1;
        [SerializeField, Min(0f)] private float strikeInterval = 0.12f;

        [Header("Hiệu ứng khi trúng đòn")]
        [Tooltip("Các hiệu ứng chạy mỗi khi trúng mục tiêu (đốt, làm chậm, choáng, hút máu...).")]
        [SerializeField] private List<WeaponHitEffectSO> hitEffects = new List<WeaponHitEffectSO>();

        [Header("Hiệu ứng hình ảnh")]
        [SerializeField] private WeaponVfxProfile vfx = new WeaponVfxProfile();

        // Tương thích ngược với code cũ
        public string WeaponId => ItemId;
        public string WeaponName => DisplayName;
        public WeaponType Type => weaponType;
        public float Damage => damage;
        public float AttackRate => attackRate;
        public float AttackRange => attackRange;
        public GameObject WeaponPrefab => weaponPrefab;
        public Color WeaponColor => weaponColor;

        // Melee Getters
        public float SwingArcAngle => swingArcAngle;
        public float HitRadius => hitRadius;
        public float KnockbackForce => knockbackForce;

        // Ranged Getters
        public GameObject ProjectilePrefab => projectilePrefab;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLifetime => projectileLifetime;
        public int ProjectileCount => projectileCount;
        public float SpreadAngle => spreadAngle;
        public float Inaccuracy => inaccuracy;
        public bool AutoFire => autoFire;
        public int PierceCount => pierceCount;
        public float ExplosionRadius => explosionRadius;
        public bool IsExplosive => explosionRadius > 0f;
        public bool ExplosionDamagesOwner => explosionDamagesOwner;
        public float ArcHeight => arcHeight;

        // Đòn nhiều nhát
        public int StrikeCount => Mathf.Max(1, strikeCount);
        public float StrikeInterval => strikeInterval;

        public IReadOnlyList<WeaponHitEffectSO> HitEffects => hitEffects;
        public WeaponVfxProfile Vfx => vfx ?? (vfx = new WeaponVfxProfile());

        public override string GetStatsText()
        {
            var sb = new StringBuilder();
            bool melee = weaponType == WeaponType.Melee;
            sb.Append(melee ? "Cận chiến" : "Tầm xa");
            sb.Append("\nSát thương: ").Append(damage.ToString("0.#"));
            if (melee && StrikeCount > 1) sb.Append(" x").Append(StrikeCount).Append(" nhát");
            if (!melee && projectileCount > 1) sb.Append(" x").Append(projectileCount).Append(" tia");
            sb.Append("\nTốc đánh: ").Append(attackRate.ToString("0.##")).Append(" giây/đòn");
            sb.Append("\nTầm: ").Append(attackRange.ToString("0.#")).Append(" m");
            if (melee && swingArcAngle >= 179f) sb.Append("\nQuét ").Append(Mathf.RoundToInt(swingArcAngle)).Append(" độ");
            if (pierceCount > 0) sb.Append("\nXuyên ").Append(pierceCount).Append(" mục tiêu");
            if (explosionRadius > 0f)
            {
                sb.Append("\nNổ bán kính ").Append(explosionRadius.ToString("0.#")).Append(" m");
                if (explosionDamagesOwner) sb.Append(" (gây thương cả bản thân)");
            }

            foreach (WeaponHitEffectSO effect in hitEffects)
            {
                if (effect != null) sb.Append("\n• ").Append(effect.Describe());
            }

            return sb.ToString();
        }
    }
}
