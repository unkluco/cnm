using LightSouls.Combat.Effects;
using UnityEngine;

namespace LightSouls.Combat.Items
{
    /// <summary>Dịch chuyển người chơi về điểm an toàn (phòng xuất phát). Không dùng được khi phòng đang khóa vì giao tranh.</summary>
    [CreateAssetMenu(fileName = "Effect_Teleport", menuName = "LightSouls/Combat/Consumable Effects/Teleport Home")]
    public class TeleportEffectSO : ConsumableEffectSO
    {
        [SerializeField] private Color color = new Color(0.55f, 0.4f, 1f);

        public override bool CanApply(ConsumableUseContext context, out string reason)
        {
            reason = null;
            var player = context.User != null ? context.User.GetComponent<PlayerController>() : null;
            if (player == null || !player.HasSafePoint)
            {
                reason = "Không có nơi để dịch chuyển";
                return false;
            }

            if (player.InLockedCombat)
            {
                reason = "Không thể dịch chuyển khi đang giao tranh";
                return false;
            }

            return true;
        }

        public override void Apply(ConsumableUseContext context)
        {
            var player = context.User.GetComponent<PlayerController>();
            Vector3 from = player.transform.position;

            VfxFactory.Burst(from + Vector3.up, color, 36, 3f, 0.22f, 0.7f, -0.8f);
            VfxFactory.Ring(from, color, 2f, 0.4f, 48);

            player.Teleport(player.SafePoint);

            Vector3 to = player.transform.position;
            VfxFactory.Burst(to + Vector3.up, color, 36, 3f, 0.22f, 0.7f, -0.8f);
            VfxFactory.Ring(to, color, 2.5f, 0.45f, 48);
        }

        public override string Describe()
        {
            return "Dịch chuyển về phòng xuất phát (không dùng được khi đang giao tranh)";
        }
    }
}
