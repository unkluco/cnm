using LightSouls.Combat.Effects;
using LightSouls.Combat.Stats;
using LightSouls.Combat.Enemies;
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

        public static bool IsFriendly(GameObject attacker, IDamageable target)
        {
            var component=target as Component;
            return attacker!=null&&attacker.CompareTag("Player")&&component!=null&&component.GetComponent<EnemyBase>()?.IsCharmed==true;
        }

        /// <summary>
        /// Gây sát thương lên mục tiêu. baseDamage là sát thương gốc của vũ khí;
        /// applyAttackerMultiplier = false khi sát thương đã được nhân sẵn (ví dụ vụ nổ tính lúc phóng).
        /// Trả về sát thương thực đã gây (trước giảm sát thương của mục tiêu).
        /// </summary>
        public static float Apply(WeaponDataSO weapon, float baseDamage, GameObject attacker, IDamageable target,
            Vector3 hitPoint, bool applyAttackerMultiplier = true, Vector3 impactDirection = default,
            CombatVfxElement fallbackElement = CombatVfxElement.Steel, Color? fallbackColor = null)
        {
            if (target == null || target.IsDead || IsFriendly(attacker,target))
            {
                return 0f;
            }

            float damage = baseDamage * (applyAttackerMultiplier ? PlayerStats.DamageMultiplierOf(attacker) : 1f);
            var targetComponent = target as Component;
            var health=targetComponent!=null?targetComponent.GetComponent<HealthComponent>():null;
            float prior=health!=null?health.CurrentHealth:0f;
            target.TakeDamage(new DamageInfo(damage, attacker, hitPoint));
            float actual=health!=null?Mathf.Max(0f,prior-health.CurrentHealth):damage;

            Color hitColor = weapon != null ? weapon.Vfx.hitColor : fallbackColor ?? Color.white;
            CombatVfxElement element = weapon != null ? weapon.Vfx.element : fallbackElement;
            Vector3 direction = impactDirection.sqrMagnitude > .0001f ? impactDirection :
                attacker != null && targetComponent != null ? targetComponent.transform.position - attacker.transform.position : Vector3.up;
            float strength = Mathf.Clamp01(damage / 72f);
            if (hitColor.a > 0f)
            {
                VfxFactory.HitImpact(hitPoint, direction, hitColor, element, strength);
            }
            if ((attacker != null && attacker.CompareTag("Player")) || (targetComponent != null && targetComponent.CompareTag("Player")))
                CombatImpactFeedback.Emit(hitPoint, Mathf.Lerp(.21f, .7f, strength));

            if (actual>0f && weapon != null && weapon.HitEffects != null && targetComponent != null)
            {
                var context = new WeaponHitContext
                {
                    Attacker = attacker,
                    Target = targetComponent.gameObject,
                    Damageable = target,
                    DamageDealt = actual,
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

            return actual;
        }
    }
}
