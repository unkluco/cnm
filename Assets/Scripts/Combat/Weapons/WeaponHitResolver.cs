using LightSouls.Combat.Effects;
using LightSouls.Combat.Stats;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.Combat.Weapons
{
    /// <summary>
    /// Một điểm duy nhất xử lý "vũ khí trúng mục tiêu": nhân hệ số sát thương của người tấn công,
    /// gây sát thương, phát tia trúng đòn và chạy các WeaponHitEffectSO của vũ khí.
    /// Cận chiến, đạn và vụ nổ đều gọi vào đây nên thêm cơ chế mới chỉ cần sửa một chỗ.
    /// </summary>
    public static class WeaponHitResolver
    {
        /// <summary>Tìm IDamageable trên collider hoặc cha của nó.</summary>
        public static IDamageable FindDamageable(Component collider)
        {
            if (collider == null)
            {
                return null;
            }

            var damageable = collider.GetComponent<IDamageable>();
            return damageable ?? collider.GetComponentInParent<IDamageable>();
        }

        /// <summary>
        /// Gây sát thương lên mục tiêu. baseDamage là sát thương gốc của vũ khí;
        /// applyAttackerMultiplier = false khi sát thương đã được nhân sẵn (ví dụ vụ nổ tính lúc phóng).
        /// Trả về sát thương thực đã gây (trước giảm sát thương của mục tiêu).
        /// </summary>
        public static float Apply(WeaponDataSO weapon, float baseDamage, GameObject attacker, IDamageable target,
            Vector3 hitPoint, bool applyAttackerMultiplier = true)
        {
            if (target == null || target.IsDead)
            {
                return 0f;
            }

            float damage = baseDamage * (applyAttackerMultiplier ? PlayerStats.DamageMultiplierOf(attacker) : 1f);
            target.TakeDamage(new DamageInfo(damage, attacker, hitPoint));

            Color hitColor = weapon != null ? weapon.Vfx.hitColor : Color.white;
            if (hitColor.a > 0f)
            {
                VfxFactory.Burst(hitPoint, hitColor, 10, 3.5f, 0.14f, 0.3f, 0.4f);
            }

            var targetComponent = target as Component;
            if (weapon != null && weapon.HitEffects != null && targetComponent != null)
            {
                var context = new WeaponHitContext
                {
                    Attacker = attacker,
                    Target = targetComponent.gameObject,
                    Damageable = target,
                    DamageDealt = damage,
                    HitPoint = hitPoint,
                    Weapon = weapon,
                };

                foreach (WeaponHitEffectSO effect in weapon.HitEffects)
                {
                    if (effect != null)
                    {
                        effect.OnHit(context);
                    }
                }
            }

            return damage;
        }
    }
}
