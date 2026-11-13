using System;
using UnityEngine;
using LightSouls.Common;
using LightSouls.Combat.Weapons;

namespace LightSouls.Combat
{
    /// <summary>
    /// Điểm truy cập trung tâm của nhân vật Người chơi (Player).
    /// Gắn trên Root GameObject của Player: đảm bảo Tag "Player", Layer "Player".
    /// Cung cấp các cổng giao tiếp chuẩn cho GameBootstrap và UI HUD của Kiệt.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerCombat))]
    [RequireComponent(typeof(HealthComponent))]
    public class PlayerController : MonoBehaviour
    {
        private PlayerMovement _movement;
        private PlayerCombat _combat;
        private HealthComponent _health;

        // Dữ liệu cho HUD của Kiệt
        public float CurrentHealth => _health != null ? _health.CurrentHealth : 100f;
        public float MaxHealth => _health != null ? _health.MaxHealth : 100f;
        public bool IsDead => _health != null && _health.IsDead;
        public WeaponDataSO CurrentWeapon => _combat != null ? _combat.CurrentWeaponData : null;
        public WeaponDataSO NextWeapon => _combat != null ? _combat.NextWeaponData : null;
        public PlayerMovement Movement => _movement;
        public PlayerCombat Combat => _combat;

        public event Action<float, float> OnHealthChanged
        {
            add { if (_health != null) _health.OnHealthChanged += value; }
            remove { if (_health != null) _health.OnHealthChanged -= value; }
        }

        public event Action<IDamageable> OnDied
        {
            add { if (_health != null) _health.Died += value; }
            remove { if (_health != null) _health.Died -= value; }
        }

        public event Action<WeaponDataSO> OnWeaponChanged
        {
            add { if (_combat != null) _combat.OnWeaponChanged += value; }
            remove { if (_combat != null) _combat.OnWeaponChanged -= value; }
        }

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _combat = GetComponent<PlayerCombat>();
            _health = GetComponent<HealthComponent>();

            // Đảm bảo đúng Tag và Layer theo hợp đồng với Đạt và Kiệt
            gameObject.tag = "Player";
            gameObject.layer = 6; // Layer Player = 6

            if (_health != null)
            {
                _health.Died += HandlePlayerDied;
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.Died -= HandlePlayerDied;
            }
        }

        /// <summary>
        /// Hàm công khai bật/tắt toàn bộ điều khiển của người chơi.
        /// GameBootstrap gọi hàm này khi mở hoặc đóng menu tạm dừng (Pause).
        /// </summary>
        public void SetInputActive(bool active)
        {
            if (_movement != null) _movement.SetInputActive(active);
            if (_combat != null) _combat.SetInputActive(active);
        }

        private void HandlePlayerDied(IDamageable damageable)
        {
            Debug.Log("[PlayerController] Người chơi đã tử trận!");
            SetInputActive(false);
        }
    }
}
