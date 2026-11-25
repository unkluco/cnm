using System;
using LightSouls.Combat.Stats;
using LightSouls.Combat.Runes;
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
        public float EffectiveAttackInterval => weaponData == null ? 0f : Mathf.Max(.18f,
            weaponData.Type==WeaponType.Melee?(weaponData.StrikeCount-1)*weaponData.StrikeInterval+.32f:0f,
            weaponData.AttackRate * (GetComponentInParent<PlayerStats>()?.Get(StatType.AttackCooldown) ?? 1f));

        public virtual bool CanAttack
        {
            get
            {
                if (weaponData == null) return false;
                return Time.time >= lastAttackTime + EffectiveAttackInterval;
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
        public virtual bool ExecuteRuneAttack(Transform attackOrigin,Vector3 aimDirection,LayerMask targetLayer,GameObject attacker,
            RuneSkill skill,RuneImpact impact)=>false;
        public virtual void PlayRuneVisual(Transform attackOrigin,Vector3 aimDirection,RuneSkill skill){lastAttackTime=Time.time;}

        /// <summary>
        /// Khởi tạo trạng thái vũ khí khi được rút ra / trang bị.
        /// </summary>
        public virtual void OnEquipped()
        {
            lastAttackTime = -999f;
        }

        /// <summary>
        /// Dọn dẹp trạng thái khi cất vũ khí.
        /// </summary>
        public virtual void OnUnequipped()
        {
            StopAllCoroutines();
        }
    }
}
