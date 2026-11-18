using System.Collections.Generic;
using System.Text;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Stats;
using UnityEngine;

namespace LightSouls.Combat.Items
{
    /// <summary>Tăng (hoặc giảm) chỉ số trong một thời gian. Dùng lại khi còn hiệu lực thì làm mới thời gian, không cộng chồng.</summary>
    [CreateAssetMenu(fileName = "Effect_Buff", menuName = "LightSouls/Combat/Consumable Effects/Stat Buff")]
    public class StatBuffEffectSO : ConsumableEffectSO
    {
        [SerializeField] private List<StatModifier> modifiers = new List<StatModifier>();
        [SerializeField, Min(0.1f)] private float duration = 15f;

        [Header("Hiệu ứng")]
        [Tooltip("Màu hào quang bao quanh nhân vật trong thời gian hiệu lực.")]
        [SerializeField] private Color auraColor = new Color(1f, 0.25f, 0.2f);

        public override void Apply(ConsumableUseContext context)
        {
            var stats = PlayerStats.GetOrAdd(context.User);
            if (stats == null)
            {
                return;
            }

            stats.SetModifiers(this, modifiers, duration);
            VfxFactory.TimedAura(context.User.transform, name, auraColor, duration, 22f, 0.55f);
            VfxFactory.Burst(context.User.transform.position + Vector3.up, auraColor, 24, 3.5f, 0.2f, 0.5f, -0.3f);
            VfxFactory.Ring(context.User.transform.position, auraColor, 2.2f, 0.35f, 40);
        }

        public override string Describe()
        {
            var sb = new StringBuilder();
            foreach (StatModifier modifier in modifiers)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(modifier.Describe());
            }

            sb.Append(" trong ").Append(duration.ToString("0.#")).Append(" giây");
            return sb.ToString();
        }
    }
}
