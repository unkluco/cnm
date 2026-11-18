using LightSouls.Combat.Stats;
using LightSouls.Combat.Weapons;
using UnityEngine;

namespace LightSouls.Combat.Items
{
    /// <summary>Ném một quả bom theo hướng ngắm (về phía kẻ địch gần nhất), nổ khi chạm đất.</summary>
    [CreateAssetMenu(fileName = "Effect_ThrowBomb", menuName = "LightSouls/Combat/Consumable Effects/Throw Bomb")]
    public class ThrowBombEffectSO : ConsumableEffectSO
    {
        [SerializeField, Min(0f)] private float damage = 60f;
        [SerializeField, Min(0.5f)] private float radius = 3f;
        [Tooltip("Tầm ném tối đa (m).")]
        [SerializeField, Min(1f)] private float range = 7f;
        [Tooltip("Thời gian bay trước khi nổ (giây).")]
        [SerializeField, Min(0.1f)] private float flightTime = 1f;
        [SerializeField, Min(0f)] private float arcHeight = 3f;
        [SerializeField] private bool damagesUser;
        [SerializeField] private Color color = new Color(0.15f, 0.15f, 0.18f);

        public override void Apply(ConsumableUseContext context)
        {
            Transform user = context.User.transform;
            Vector3 from = user.position + Vector3.up * 1.2f + context.AimDirection * 0.5f;
            Vector3 to = user.position + context.AimDirection * range;
            if (context.AimTarget.HasValue)
            {
                Vector3 target = context.AimTarget.Value;
                Vector3 flat = target - user.position;
                flat.y = 0f;
                to = user.position + Vector3.ClampMagnitude(flat, range);
            }

            to.y = user.position.y;
            float finalDamage = damage * PlayerStats.DamageMultiplierOf(context.User);
            ExplosiveProjectile.Launch(from, to, flightTime, arcHeight, finalDamage, radius, context.User, damagesUser, color, null, 0.45f);
        }

        public override string Describe()
        {
            return "Ném bom gây " + damage.ToString("0") + " sát thương nổ, bán kính " + radius.ToString("0.#") + " m";
        }
    }
}
