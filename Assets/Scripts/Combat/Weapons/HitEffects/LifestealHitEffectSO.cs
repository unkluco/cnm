using LightSouls.Combat.Effects;
using UnityEngine;

namespace LightSouls.Combat.Weapons
{
    [CreateAssetMenu(fileName = "HitEffect_Lifesteal", menuName = "LightSouls/Combat/Hit Effects/Lifesteal")]
    public class LifestealHitEffectSO : WeaponHitEffectSO
    {
        [Tooltip("Tỉ lệ sát thương gây ra được hồi thành máu (0.05 = 5%).")]
        [SerializeField, Range(0f, 1f)] private float percent = 0.05f;

        private static readonly Color HealColor = new Color(0.9f, 0.15f, 0.3f);

        public override void OnHit(WeaponHitContext context)
        {
            if (context.Attacker == null || context.DamageDealt <= 0f)
            {
                return;
            }

            var health = context.Attacker.GetComponent<HealthComponent>();
            if (health == null || health.IsDead)
            {
                return;
            }

            health.Heal(context.DamageDealt * percent);
            VfxFactory.Burst(context.Attacker.transform.position + Vector3.up * 1.1f, HealColor, 6, 1.2f, 0.14f, 0.5f, -0.4f);
        }

        public override string Describe()
        {
            return "Hút máu " + Mathf.RoundToInt(percent * 100f) + "% sát thương gây ra";
        }
    }
}
