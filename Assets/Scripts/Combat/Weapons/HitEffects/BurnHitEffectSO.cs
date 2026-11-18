using LightSouls.Combat.Effects;
using UnityEngine;

namespace LightSouls.Combat.Weapons
{
    [CreateAssetMenu(fileName = "HitEffect_Burn", menuName = "LightSouls/Combat/Hit Effects/Burn")]
    public class BurnHitEffectSO : WeaponHitEffectSO
    {
        [SerializeField, Min(0f)] private float damagePerSecond = 6f;
        [SerializeField, Min(0f)] private float duration = 3f;

        public override void OnHit(WeaponHitContext context)
        {
            var receiver = StatusEffectReceiver.GetOrAdd(context.Target);
            if (receiver != null)
            {
                receiver.ApplyBurn(damagePerSecond, duration, context.Attacker);
            }
        }

        public override string Describe()
        {
            return "Đốt cháy: " + damagePerSecond.ToString("0.#") + " sát thương/giây trong " + duration.ToString("0.#") + " giây";
        }
    }
}
