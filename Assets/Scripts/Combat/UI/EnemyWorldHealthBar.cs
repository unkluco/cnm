using UnityEngine;
using UnityEngine.UI;
using LightSouls.Common;

namespace LightSouls.Combat.UI
{
    /// <summary>
    /// Thanh máu lơ lửng trên đầu quái vật (World-Space) và tự động kích hoạt
    /// hiệu ứng số sát thương bay lên (Damage Popup) khi quái bị đánh.
    /// </summary>
    public class EnemyWorldHealthBar : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private HealthComponent healthComponent;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image fillImage;
        [SerializeField] private GameObject popupPrefab;

        [Header("Tùy biến hiển thị")]
        [SerializeField] private Vector3 offsetAboveHead = new Vector3(0f, 2.2f, 0f);
        [SerializeField] private bool hideWhenFull = false;

        private Camera _mainCamera;
        private Canvas _worldCanvas;
        private float _lastHealth = -1f;

        private void Awake()
        {
            if (healthComponent == null)
            {
                healthComponent = GetComponentInParent<HealthComponent>();
            }

            _worldCanvas = GetComponentInChildren<Canvas>();
        }

        private void Start()
        {
            _mainCamera = Camera.main;

            if (healthComponent != null)
            {
                _lastHealth = healthComponent.CurrentHealth;
                healthComponent.OnHealthChanged += HandleHealthChanged;
                healthComponent.Died += HandleDied;
                UpdateHealthBar(healthComponent.CurrentHealth, healthComponent.MaxHealth);
            }
        }

        private void OnDestroy()
        {
            if (healthComponent != null)
            {
                healthComponent.OnHealthChanged -= HandleHealthChanged;
                healthComponent.Died -= HandleDied;
            }
        }

        private void LateUpdate()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;

            if (_mainCamera != null && transform != null)
            {
                // Luôn quay mặt về Camera
                transform.rotation = _mainCamera.transform.rotation;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (_lastHealth > current)
            {
                float damageTaken = _lastHealth - current;
                Vector3 spawnPos = transform.position + Vector3.up * 0.3f;
                DamagePopup.Create(popupPrefab, spawnPos, damageTaken);
            }

            _lastHealth = current;
            UpdateHealthBar(current, max);
        }

        private void UpdateHealthBar(float current, float max)
        {
            if (healthSlider != null && max > 0f)
            {
                healthSlider.value = current / max;
            }

            if (fillImage != null && max > 0f)
            {
                float ratio = current / max;
                // Chuyển màu từ xanh lá sang cam sang đỏ khi máu giảm
                fillImage.color = Color.Lerp(Color.red, Color.green, ratio);
            }

            if (_worldCanvas != null && hideWhenFull)
            {
                _worldCanvas.enabled = current < max && current > 0;
            }
        }

        private void HandleDied(IDamageable damageable)
        {
            if (_worldCanvas != null)
            {
                _worldCanvas.enabled = false;
            }
        }
    }
}
