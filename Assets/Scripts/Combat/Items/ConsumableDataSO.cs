using UnityEngine;
using LightSouls.Common;

namespace LightSouls.Combat.Items
{
    [CreateAssetMenu(fileName = "Consumable_", menuName = "LightSouls/Combat/Consumable Data")]
    public class ConsumableDataSO : ItemDefinitionSO
    {
        public override ItemCategory Category => ItemCategory.Consumable;

        [Header("Tác dụng dược phẩm")]
        [Tooltip("Lượng máu hồi phục ngay lập tức.")]
        [SerializeField] private float healAmount = 35f;

        public float HealAmount => healAmount;

        /// <summary>
        /// Sử dụng vật phẩm lên người dùng (user). Trả về true nếu dùng thành công.
        /// </summary>
        public bool Use(GameObject user)
        {
            if (user == null) return false;

            HealthComponent health = user.GetComponent<HealthComponent>();
            if (health != null && !health.IsDead)
            {
                // Nếu máu đã đầy thì không cần uống (tránh lãng phí)
                if (health.CurrentHealth >= health.MaxHealth)
                {
                    return false;
                }

                health.Heal(healAmount);
                return true;
            }

            return false;
        }
    }
}
