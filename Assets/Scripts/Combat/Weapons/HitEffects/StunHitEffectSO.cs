using LightSouls.Combat.Effects;
using UnityEngine;

namespace LightSouls.Combat.Weapons
{
    [CreateAssetMenu(fileName = "HitEffect_Stun", menuName = "LightSouls/Combat/Hit Effects/Stun")]
    public class StunHitEffectSO : WeaponHitEffectSO
    {
        [SerializeField, Min(0f)] private float duration = 1f;

        public override void OnHit(WeaponHitContext context)
        {
            var receiver = StatusEffectReceiver.GetOrAdd(context.Target);
            if (receiver != null)
            {
                receiver.ApplyStun(duration);
            }
        }

        public override string Describe()
        {
            return "Gây choáng " + duration.ToString("0.#") + " giây";
        }
    }
}
