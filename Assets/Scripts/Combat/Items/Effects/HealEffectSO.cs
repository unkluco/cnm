using LightSouls.Combat.Effects;
using UnityEngine;

namespace LightSouls.Combat.Items
{
    [CreateAssetMenu(fileName = "Effect_Heal", menuName = "LightSouls/Combat/Consumable Effects/Heal")]
    public class HealEffectSO : ConsumableEffectSO
    {
        [Tooltip("Lượng máu hồi cố định.")]
        [SerializeField, Min(0f)] private float flatAmount = 0f;

        [Tooltip("Tỉ lệ máu tối đa được hồi (0.6 = 60%).")]
        [SerializeField, Range(0f, 1f)] private float percentOfMax = 0f;

        [Header("Hiệu ứng")]
        [SerializeField] private Color color = new Color(0.35f, 1f, 0.45f);
        [SerializeField] private bool bigEffect;

        public override bool CanApply(ConsumableUseContext context, out string reason)
        {
            reason = null;
            var health = context.User != null ? context.User.GetComponent<HealthComponent>() : null;
            if (health == null || health.IsDead)
            {
                reason = "Không thể dùng lúc này";
                return false;
            }

            if (!context.TriggeredByDeath && health.CurrentHealth >= health.MaxHealth)
            {
                reason = "Máu đang đầy";
                return false;
            }

            return true;
        }

        public override void Apply(ConsumableUseContext context)
        {
            var health = context.User.GetComponent<HealthComponent>();
            if (health == null)
            {
                return;
            }

            health.Heal(flatAmount + health.MaxHealth * percentOfMax);

            Vector3 position = context.User.transform.position;
            VfxFactory.Burst(position + Vector3.up * 1f, color, bigEffect ? 40 : 18, bigEffect ? 4f : 2f, 0.2f, 0.8f, -0.6f);
            if (bigEffect)
            {
                VfxFactory.Ring(position, color, 3f, 0.5f, 60);
                VfxFactory.Burst(position + Vector3.up * 0.2f, Color.Lerp(color, Color.white, 0.5f), 30, 1.5f, 0.25f, 1.4f, -1.2f);
            }
        }

        public override string Describe()
        {
            if (percentOfMax > 0f && flatAmount > 0f)
            {
                return "Hồi " + flatAmount.ToString("0") + " + " + Mathf.RoundToInt(percentOfMax * 100f) + "% máu tối đa";
            }

            return percentOfMax > 0f
                ? "Hồi " + Mathf.RoundToInt(percentOfMax * 100f) + "% máu tối đa"
                : "Hồi " + flatAmount.ToString("0") + " máu";
        }
    }
}
