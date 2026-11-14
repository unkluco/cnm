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
    }
}
