using LightSouls.Common;
using UnityEngine;

namespace LightSouls.Combat.Weapons
{
    /// <summary>Thông tin một lần vũ khí trúng mục tiêu, truyền cho các WeaponHitEffectSO.</summary>
    public struct WeaponHitContext
    {
        public GameObject Attacker;
        /// <summary>GameObject của mục tiêu (nơi có IDamageable).</summary>
        public GameObject Target;
        public IDamageable Damageable;
        /// <summary>Sát thương đã gây ra lần này (sau hệ số của người tấn công).</summary>
        public float DamageDealt;
        public Vector3 HitPoint;
        public WeaponDataSO Weapon;
    }

    /// <summary>
    /// Hiệu ứng xảy ra mỗi khi vũ khí trúng mục tiêu (đốt, làm chậm, choáng, hút máu...).
    /// Mỗi hiệu ứng là một asset; gắn vào danh sách Hit Effects của WeaponDataSO. Một vũ khí có thể có nhiều hiệu ứng,
    /// và một hiệu ứng dùng chung cho nhiều vũ khí.
    /// Thêm hiệu ứng mới: tạo lớp con, ghi đè OnHit và Describe, rồi tạo asset từ menu Create.
    /// </summary>
    public abstract class WeaponHitEffectSO : ScriptableObject
    {
        public abstract void OnHit(WeaponHitContext context);

        /// <summary>Một dòng mô tả cho bảng chi tiết vật phẩm.</summary>
        public abstract string Describe();
    }
}
