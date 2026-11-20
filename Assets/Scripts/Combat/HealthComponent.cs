using System;
using UnityEngine;
using LightSouls.Common;

namespace LightSouls.Combat
{
    /// <summary>
    /// Quản lý máu và nhận sát thương cho thực thể (Player, Enemy, v.v.).
    /// Cài đặt interface chuẩn IDamageable từ LightSouls.Common.
    /// </summary>
    public class HealthComponent : MonoBehaviour, IDamageable
    {
        [Header("Chỉ số máu")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        [SerializeField] private float damageReduction = 0f;

        [Header("Hiệu ứng phản hồi")]
        [SerializeField] private bool invincibleAfterHit = false;
        [SerializeField] private float invincibilityDuration = 0.2f;

        private float _lastHitTime = -999f;
        private bool _isDead = false;
        private float exposedUntil, incomingMultiplier=1f;

        public bool IsDead => _isDead;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float DamageReduction
        {
            get => damageReduction;
            set => damageReduction = Mathf.Clamp(value, 0f, 0.95f);
        }

        /// <summary>Phát sự kiện khi máu thay đổi (current, max).</summary>
        public event Action<float, float> OnHealthChanged;

        /// <summary>
        /// Gọi khi máu sắp về 0, trước khi chết. Trả true nếu đã cứu (ví dụ Lông Phượng Hoàng hồi máu);
        /// khi đó nếu máu &gt; 0 thì nhân vật không chết.
        /// </summary>
        public Func<bool> DeathPrevention;

        /// <summary>Phát sự kiện chết đúng một lần duy nhất theo chuẩn IDamageable.</summary>
        public event Action<IDamageable> Died;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        private void Start()
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void ScaleMaximum(float multiplier)
        {
            if(_isDead)return;float ratio=currentHealth/Mathf.Max(1f,maxHealth);
            maxHealth=Mathf.Max(1f,maxHealth*multiplier);currentHealth=maxHealth*ratio;
            OnHealthChanged?.Invoke(currentHealth,maxHealth);
        }

        public void RestoreFraction(float fraction)
        {
            if(_isDead)return;currentHealth=Mathf.Max(1f,maxHealth*Mathf.Clamp01(fraction));OnHealthChanged?.Invoke(currentHealth,maxHealth);
        }

        public void Expose(float multiplier,float duration)
        {
            incomingMultiplier=Time.time<exposedUntil?Mathf.Max(incomingMultiplier,multiplier):multiplier;
            exposedUntil=Mathf.Max(exposedUntil,Time.time+duration);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (_isDead) return;

            if (invincibleAfterHit && Time.time - _lastHitTime < invincibilityDuration)
            {
                return;
            }

            _lastHitTime = Time.time;
            float effectiveAmount = Mathf.Max(0f,info.Amount) * (1f - damageReduction) * (Time.time<exposedUntil?incomingMultiplier:1f);
            currentHealth = Mathf.Max(0f, currentHealth - effectiveAmount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f && !_isDead)
            {
                if (DeathPrevention != null && DeathPrevention() && currentHealth > 0f)
                {
                    return;
                }

                _isDead = true;
                Died?.Invoke(this);
            }
        }

        public void Heal(float amount)
        {
            if (_isDead || amount <= 0f) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void ResetHealth()
        {
            _isDead = false;
            exposedUntil=0f;incomingMultiplier=1f;_lastHitTime=-999f;
            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }
}
