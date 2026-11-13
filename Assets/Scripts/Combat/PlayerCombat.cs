using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using LightSouls.Common;
using LightSouls.Combat.Weapons;

namespace LightSouls.Combat
{
    /// <summary>
    /// Quản lý chiến đấu của Player: trang bị, đổi vũ khí một nút bấm,
    /// tấn công có hỗ trợ Auto-target quái gần nhất và phản hồi xúc giác.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Kho vũ khí trang bị")]
        [SerializeField] private List<WeaponDataSO> weaponInventory = new List<WeaponDataSO>();
        [SerializeField] private int currentWeaponIndex = 0;

        [Header("Điểm gắn vũ khí")]
        [SerializeField] private Transform weaponSocket;
        [SerializeField] private Transform attackOrigin;

        [Header("Mục tiêu & Tự động ngắm (Auto-Aim)")]
        [Tooltip("Layer nhận sát thương từ Player (Layer Enemy = 7).")]
        [SerializeField] private LayerMask enemyLayer = 1 << 7;
        [Tooltip("Bật tính năng tự động xoay nhẹ và ngắm về phía quái gần nhất trong tầm.")]
        [SerializeField] private bool autoAimEnabled = true;
        [SerializeField] private float autoAimMaxDistance = 12f;

        [Header("Input System Actions")]
        [SerializeField] private InputActionReference attackAction;
        [SerializeField] private InputActionReference switchWeaponAction;

        private WeaponBase _activeWeapon;
        private GameObject _activeWeaponInstance;
        private PlayerMovement _movement;
        private bool _isInputActive = true;

        public WeaponDataSO CurrentWeaponData => (_activeWeapon != null) ? _activeWeapon.Data : null;
        public IReadOnlyList<WeaponDataSO> Inventory => weaponInventory;

        /// <summary>
        /// Trả về thông tin vũ khí tiếp theo trong danh sách nếu đổi vũ khí (để cập nhật Icon nút bấm).
        /// </summary>
        public WeaponDataSO NextWeaponData
        {
            get
            {
                if (weaponInventory == null || weaponInventory.Count <= 1) return null;
                int nextIndex = (currentWeaponIndex + 1) % weaponInventory.Count;
                return weaponInventory[nextIndex];
            }
        }

        /// <summary>Phát sự kiện khi đổi vũ khí mới (dùng cập nhật UI HUD/Nút bấm).</summary>
        public event Action<WeaponDataSO> OnWeaponChanged;

        /// <summary>Phát sự kiện khi Player thực hiện một đòn tấn công.</summary>
        public event Action OnAttackExecuted;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();

            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }
        }

        private void Start()
        {
            if (weaponInventory.Count > 0)
            {
                EquipWeapon(currentWeaponIndex);
            }
        }

        private void OnEnable()
        {
            if (attackAction != null && attackAction.action != null)
            {
                attackAction.action.Enable();
                attackAction.action.performed += OnAttackInput;
            }

            if (switchWeaponAction != null && switchWeaponAction.action != null)
            {
                switchWeaponAction.action.Enable();
                switchWeaponAction.action.performed += OnSwitchWeaponInput;
            }
        }

        private void OnDisable()
        {
            if (attackAction != null && attackAction.action != null)
            {
                attackAction.action.performed -= OnAttackInput;
                attackAction.action.Disable();
            }

            if (switchWeaponAction != null && switchWeaponAction.action != null)
            {
                switchWeaponAction.action.performed -= OnSwitchWeaponInput;
                switchWeaponAction.action.Disable();
            }
        }

        private void Update()
        {
            // Fallback đọc trực tiếp phím nếu không dùng InputActionReference
            if (!_isInputActive) return;

            if (attackAction == null || attackAction.action == null)
            {
                bool attackPressed = false;
                if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
                {
                    attackPressed = true;
                }
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    attackPressed = true;
                }
                if (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.buttonWest.wasPressedThisFrame))
                {
                    attackPressed = true;
                }

                if (attackPressed)
                {
                    PerformAttack();
                }
            }

            if (switchWeaponAction == null || switchWeaponAction.action == null)
            {
                bool switchPressed = false;
                if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
                {
                    switchPressed = true;
                }
                if (Gamepad.current != null && (Gamepad.current.buttonNorth.wasPressedThisFrame || Gamepad.current.rightShoulder.wasPressedThisFrame))
                {
                    switchPressed = true;
                }

                if (switchPressed)
                {
                    SwitchWeapon();
                }
            }
        }

        private void OnAttackInput(InputAction.CallbackContext context)
        {
            if (!_isInputActive) return;
            PerformAttack();
        }

        private void OnSwitchWeaponInput(InputAction.CallbackContext context)
        {
            if (!_isInputActive) return;
            SwitchWeapon();
        }

        /// <summary>
        /// Đổi sang vũ khí tiếp theo trong danh sách (chu trình vòng lặp 1 nút bấm).
        /// </summary>
        public void SwitchWeapon()
        {
            if (weaponInventory.Count <= 1) return;

            currentWeaponIndex = (currentWeaponIndex + 1) % weaponInventory.Count;
            EquipWeapon(currentWeaponIndex);

            // Rung phản hồi nhẹ khi đổi vũ khí nếu bật rung
            if (GameSettings.VibrationEnabled)
            {
                #if UNITY_ANDROID && !UNITY_EDITOR
                Handheld.Vibrate();
                #endif
            }
        }

        public void EquipWeapon(int index)
        {
            if (index < 0 || index >= weaponInventory.Count) return;

            WeaponDataSO data = weaponInventory[index];
            if (data == null) return;

            currentWeaponIndex = index;

            // Xóa vũ khí cũ trên tay
            if (_activeWeaponInstance != null)
            {
                Destroy(_activeWeaponInstance);
                _activeWeaponInstance = null;
                _activeWeapon = null;
            }

            if (weaponSocket != null)
            {
                for (int i = weaponSocket.childCount - 1; i >= 0; i--)
                {
                    Destroy(weaponSocket.GetChild(i).gameObject);
                }
            }

            Transform parent = weaponSocket != null ? weaponSocket : transform;

            // Tạo model vũ khí mới
            if (data.WeaponPrefab != null)
            {
                _activeWeaponInstance = Instantiate(data.WeaponPrefab, parent);
                _activeWeapon = _activeWeaponInstance.GetComponent<WeaponBase>();
            }

            // Nếu prefab chưa có script WeaponBase hoặc dùng visual tạm
            if (_activeWeapon == null)
            {
                if (_activeWeaponInstance == null)
                {
                    // Tạo visual hình hộp/hình trụ placeholder
                    PrimitiveType primType = data.Type == WeaponType.Melee ? PrimitiveType.Cube : PrimitiveType.Cylinder;
                    _activeWeaponInstance = GameObject.CreatePrimitive(primType);
                    _activeWeaponInstance.transform.SetParent(parent, false);

                    if (data.Type == WeaponType.Melee)
                    {
                        _activeWeaponInstance.transform.localPosition = new Vector3(0.4f, 0.9f, 0.4f);
                        _activeWeaponInstance.transform.localScale = new Vector3(0.12f, 0.8f, 0.12f);
                        _activeWeaponInstance.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
                    }
                    else
                    {
                        _activeWeaponInstance.transform.localPosition = new Vector3(0.35f, 0.9f, 0.35f);
                        _activeWeaponInstance.transform.localScale = new Vector3(0.15f, 0.3f, 0.15f);
                        _activeWeaponInstance.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    }

                    // Tắt collider trên visual vũ khí gắn tay (tránh cản trở character controller)
                    var col = _activeWeaponInstance.GetComponent<Collider>();
                    if (col != null) Destroy(col);

                    Renderer r = _activeWeaponInstance.GetComponent<Renderer>();
                    if (r != null) r.material.color = data.WeaponColor;
                }

                if (data.Type == WeaponType.Melee)
                {
                    _activeWeapon = _activeWeaponInstance.AddComponent<MeleeWeapon>();
                }
                else
                {
                    _activeWeapon = _activeWeaponInstance.AddComponent<RangedWeapon>();
                }
            }

            _activeWeapon.SetData(data);
            _activeWeapon.OnEquipped();

            OnWeaponChanged?.Invoke(data);
        }

        public void PerformAttack()
        {
            if (_activeWeapon == null || !_activeWeapon.CanAttack) return;

            Vector3 aimDirection = transform.forward;

            // Cơ chế Auto-Aim (Phương án 1A): Tìm quái gần nhất trong tầm quét
            if (autoAimEnabled)
            {
                Transform nearestEnemy = FindNearestEnemy();
                if (nearestEnemy != null)
                {
                    Vector3 dirToEnemy = (nearestEnemy.position - transform.position);
                    dirToEnemy.y = 0;
                    if (dirToEnemy.sqrMagnitude > 0.01f)
                    {
                        aimDirection = dirToEnemy.normalized;
                        if (_movement != null)
                        {
                            _movement.RotateTowards(aimDirection);
                        }
                    }
                }
            }

            bool success = _activeWeapon.ExecuteAttack(attackOrigin, aimDirection, enemyLayer, gameObject);
            if (success)
            {
                OnAttackExecuted?.Invoke();

                // Rung nhẹ khi bắn/chém nếu máy cho phép
                if (GameSettings.VibrationEnabled)
                {
                    #if UNITY_ANDROID && !UNITY_EDITOR
                    Handheld.Vibrate();
                    #endif
                }
            }
        }

        private Transform FindNearestEnemy()
        {
            float searchRadius = Mathf.Min(autoAimMaxDistance, _activeWeapon.Data.AttackRange * 1.8f);
            if (_activeWeapon.Data.Type == WeaponType.Ranged)
            {
                searchRadius = autoAimMaxDistance;
            }

            Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius, enemyLayer);
            Transform nearest = null;
            float minDist = float.MaxValue;

            foreach (var col in hits)
            {
                IDamageable damageable = col.GetComponent<IDamageable>();
                if (damageable == null) damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.IsDead) continue;

                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = col.transform;
                }
            }

            return nearest;
        }

        public void SetInputActive(bool active)
        {
            _isInputActive = active;
        }

        public void AddWeapon(WeaponDataSO newWeapon)
        {
            if (newWeapon == null || weaponInventory.Contains(newWeapon)) return;
            weaponInventory.Add(newWeapon);
        }
    }
}
