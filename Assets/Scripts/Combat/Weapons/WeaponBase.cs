using System;
using UnityEngine;

namespace LightSouls.Combat.Weapons
{
    /// <summary>
    /// Lớp cơ sở trừu tượng cho mọi vũ khí (Cận chiến & Tầm xa).
    /// </summary>
    public abstract class WeaponBase : MonoBehaviour
    {
        [SerializeField] protected WeaponDataSO weaponData;

        protected float lastAttackTime = -999f;

        public WeaponDataSO Data => weaponData;

        public virtual bool CanAttack
        {
            get
            {
                if (weaponData == null) return false;
                return Time.time >= lastAttackTime + weaponData.AttackRate;
            }
        }

        public virtual void SetData(WeaponDataSO data)
        {
            weaponData = data;
        }

        /// <summary>
        /// Thực thi đòn tấn công. Trả về true nếu tấn công thành công.
        /// </summary>
        public abstract bool ExecuteAttack(Transform attackOrigin, Vector3 aimDirection, LayerMask targetLayer, GameObject attacker);

        /// <summary>
        /// Khởi tạo trạng thái vũ khí khi được rút ra / trang bị.
        /// </summary>
        public virtual void OnEquipped()
        {
            lastAttackTime = Time.time - (weaponData != null ? weaponData.AttackRate : 0.5f);
        }

        /// <summary>
        /// Dọn dẹp trạng thái khi cất vũ khí.
        /// </summary>
        public virtual void OnUnequipped()
        {
        }
    }
}
