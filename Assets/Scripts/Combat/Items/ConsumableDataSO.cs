using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using LightSouls.Combat.Weapons;
using LightSouls.Common;

namespace LightSouls.Combat.Items
{
    /// <summary>
    /// Vật phẩm dùng một lần. Tác dụng là danh sách ConsumableEffectSO (hồi máu, tăng lực, ném bom, dịch chuyển...)
    /// nên vật phẩm mới chỉ cần ghép các tác dụng có sẵn, không phải viết code.
    /// Danh sách rỗng thì dùng healAmount như bình máu cũ (tương thích asset cũ).
    /// triggersOnDeath: không bấm dùng được; tự kích hoạt khi người chơi sắp chết (Lông Phượng Hoàng).
    /// </summary>
    [CreateAssetMenu(fileName = "Consumable_", menuName = "LightSouls/Combat/Consumable Data")]
    public class ConsumableDataSO : ItemDefinitionSO
    {
        private const float AutoAimRange = 12f;

        public override ItemCategory Category => ItemCategory.Consumable;

        [Header("Tác dụng dược phẩm")]
        [Tooltip("Lượng máu hồi phục ngay lập tức (chỉ dùng khi danh sách Effects rỗng).")]
        [SerializeField] private float healAmount = 35f;

        [Tooltip("Các tác dụng chạy lần lượt khi dùng.")]
        [SerializeField] private List<ConsumableEffectSO> effects = new List<ConsumableEffectSO>();

        [Tooltip("Tự kích hoạt khi người chơi sắp chết thay vì bấm dùng.")]
        [SerializeField] private bool triggersOnDeath;
        [SerializeField, Min(1f)] private float cooldown = 7f;
        private const string CooldownKey = "combat.consumable";
        public float Cooldown => Mathf.Max(1f,cooldown);
        public static float CooldownRemaining => RunState.CooldownRemaining(CooldownKey);

        public float HealAmount => healAmount;
        public bool TriggersOnDeath => triggersOnDeath;
        public IReadOnlyList<ConsumableEffectSO> Effects => effects;

        /// <summary>Phát khi người chơi bấm dùng nhưng không dùng được (câu lý do), để UI hiện thông báo.</summary>
        public static event Action<GameObject, string> UseRejected;

        /// <summary>
        /// Sử dụng vật phẩm lên người dùng (user). Trả về true nếu dùng thành công (khi đó mới trừ vật phẩm).
        /// </summary>
        public bool Use(GameObject user)
        {
            if (user == null) return false;

            if (triggersOnDeath)
            {
                UseRejected?.Invoke(user, "Tự kích hoạt khi bạn gục ngã");
                return false;
            }

            if (user.CompareTag("Player") && CooldownRemaining > 0f)
            {
                UseRejected?.Invoke(user,"Đang hồi dược phẩm: "+Mathf.CeilToInt(CooldownRemaining)+"s");
                return false;
            }

            if (!HasEffects())
            {
                bool used = UseLegacyHeal(user);
                if(used && user.CompareTag("Player")) RunState.StartCooldown(CooldownKey,Cooldown);
                return used;
            }

            ConsumableUseContext context = BuildContext(user, false);
            foreach (ConsumableEffectSO effect in effects)
            {
                string reason;
                if (effect != null && !effect.CanApply(context, out reason))
                {
                    UseRejected?.Invoke(user, reason);
                    return false;
                }
            }

            ApplyAll(context);
            if(user.CompareTag("Player")) RunState.StartCooldown(CooldownKey,Cooldown);
            return true;
        }

        /// <summary>Kích hoạt khi người dùng sắp chết (máu về 0). Trả về true nếu đã kích hoạt.</summary>
        public bool TriggerOnDeath(GameObject user)
        {
            if (!triggersOnDeath || user == null)
            {
                return false;
            }

            ApplyAll(BuildContext(user, true));
            return true;
        }

        public override string GetStatsText()
        {
            var sb = new StringBuilder("Tiêu hao");
            if (!HasEffects())
            {
                sb.Append("\nHồi ").Append(healAmount.ToString("0")).Append(" máu");
            }

            foreach (ConsumableEffectSO effect in effects)
            {
                if (effect != null) sb.Append("\n• ").Append(effect.Describe());
            }

            if (triggersOnDeath) sb.Append("\n• Tự kích hoạt khi gục ngã");
            else sb.Append("\nHồi dược phẩm: ").Append(Cooldown.ToString("0.#")).Append("s (dùng chung)");
            return sb.ToString();
        }

        private bool HasEffects()
        {
            foreach (ConsumableEffectSO effect in effects)
            {
                if (effect != null) return true;
            }

            return false;
        }

        private void ApplyAll(ConsumableUseContext context)
        {
            foreach (ConsumableEffectSO effect in effects)
            {
                if (effect != null)
                {
                    effect.Apply(context);
                }
            }
        }

        private bool UseLegacyHeal(GameObject user)
        {
            HealthComponent health = user.GetComponent<HealthComponent>();
            if (health == null || health.IsDead) return false;

            // Nếu máu đã đầy thì không cần uống (tránh lãng phí)
            if (health.CurrentHealth >= health.MaxHealth)
            {
                UseRejected?.Invoke(user, "Máu đang đầy");
                return false;
            }

            health.Heal(healAmount);
            LightSouls.Combat.Effects.VfxFactory.Burst(user.transform.position + Vector3.up, new Color(0.35f, 1f, 0.45f), 18, 2f, 0.2f, 0.8f, -0.6f);
            return true;
        }

        private ConsumableUseContext BuildContext(GameObject user, bool byDeath)
        {
            Vector3 origin = user.transform.position;
            Vector3 aim = user.transform.forward;
            aim.y = 0f;
            Vector3? target = null;

            // Ngắm kẻ địch gần nhất (Layer Enemy) giống cách người chơi tự ngắm khi đánh.
            int enemyMask = LayerMask.GetMask("Enemy");
            float best = float.MaxValue;
            foreach (Collider col in Physics.OverlapSphere(origin, AutoAimRange, enemyMask))
            {
                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable == null || damageable.IsDead || WeaponHitResolver.IsFriendly(user,damageable)) continue;
                float distance = (col.bounds.center - origin).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    target = col.bounds.center;
                }
            }

            if (target.HasValue)
            {
                Vector3 flat = target.Value - origin;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f) aim = flat;
            }

            return new ConsumableUseContext
            {
                User = user,
                Item = this,
                AimDirection = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector3.forward,
                AimTarget = target,
                TriggeredByDeath = byDeath,
            };
        }
    }
}
