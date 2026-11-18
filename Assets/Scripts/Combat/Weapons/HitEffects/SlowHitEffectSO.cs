using LightSouls.Combat.Effects;
using UnityEngine;

namespace LightSouls.Combat.Weapons
{
    [CreateAssetMenu(fileName = "HitEffect_Slow", menuName = "LightSouls/Combat/Hit Effects/Slow")]
    public class SlowHitEffectSO : WeaponHitEffectSO
    {
        [Tooltip("Hệ số tốc độ khi bị chậm (0.6 = chậm 40%).")]
        [SerializeField, Range(0f, 1f)] private float speedMultiplier = 0.6f;
        [SerializeField, Min(0f)] private float duration = 2f;

        public override void OnHit(WeaponHitContext context)
        {
            var receiver = StatusEffectReceiver.GetOrAdd(context.Target);
            if (receiver != null)
            {
                receiver.ApplySlow(speedMultiplier, duration);
            }
        }

        public override string Describe()
        {
            return "Đóng băng: chậm " + Mathf.RoundToInt((1f - speedMultiplier) * 100f) + "% trong " + duration.ToString("0.#") + " giây";
        }
    }
}
