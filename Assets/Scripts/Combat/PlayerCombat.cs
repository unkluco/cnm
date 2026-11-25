using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using LightSouls.Common;
using LightSouls.Combat.Weapons;
using LightSouls.Combat.Inventory;
using LightSouls.Combat.Items;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Stats;
using LightSouls.Combat.Runes;

namespace LightSouls.Combat
{
    /// <summary>
    /// Quản lý chiến đấu của Player: trang bị từ túi đồ / thanh nhanh,
    /// tấn công có hỗ trợ Auto-target quái gần nhất và phản hồi xúc giác.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Kho vũ khí trang bị (Fallback)")]
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
        private PlayerInventory _inventory;
        private AimRangeIndicator _aimIndicator;
        private bool _isInputActive = true;
        private bool _runeInputMode;
        private int _runeContextVersion;

        public WeaponDataSO CurrentWeaponData => (_activeWeapon != null) ? _activeWeapon.Data : null;
        public bool RuneInputMode=>_runeInputMode;
        public int RuneContextVersion=>_runeContextVersion;
        public bool ConsumableSelected=>_inventory!=null&&_inventory.SelectedItem is ConsumableDataSO;
        public bool RuneInputReady=>_isInputActive&&isActiveAndEnabled&&Time.timeScale>0&&
            (_movement==null||!_movement.IsDashing)&&GetComponent<PlayerController>()?.IsDead!=true&&CurrentRuneSkills.Count>0;
        public IReadOnlyList<RuneSkill> CurrentRuneSkills
        {
            get
            {
                var kit=RuneSkillCatalog.For(CurrentWeaponData);
                // Dodging remains available even when briefly using a potion
                // or selecting an empty hotbar slot; other casts do not.
                return ConsumableSelected||kit.Count==0?RuneSkillCatalog.DashOnly:kit;
            }
        }
        private const string GlobalAttackKey = "combat.global_attack";
        public static float CooldownFor(WeaponDataSO data) => data != null ? RunState.CooldownRemaining("weapon." + data.ItemId) : 0f;
        public float AttackCooldownRemaining => Mathf.Max(RunState.CooldownRemaining(GlobalAttackKey),
            _inventory != null && _inventory.SelectedItem is ConsumableDataSO ? ConsumableDataSO.CooldownRemaining : CooldownFor(CurrentWeaponData));
        public float AttackCooldownRatio => Mathf.Clamp01(AttackCooldownRemaining / Mathf.Max(.1f,
            _inventory != null && _inventory.SelectedItem is ConsumableDataSO potion ? potion.Cooldown : _activeWeapon != null ? _activeWeapon.EffectiveAttackInterval : .4f));
        public float TacticalCooldownRemaining
        {
            get
            {
                var weapon=CurrentWeaponData;if(weapon==null)return 0f;
                float time=WeaponTactics.CastCooldownRemaining(weapon);
                foreach(var effect in weapon.HitEffects)if(effect is TacticalHitEffectSO tactic)time=Mathf.Max(time,tactic.Remaining);
                return time;
            }
        }
        public float AutoAimRadius => autoAimEnabled ? GetAutoAimRadius(CurrentWeaponData, autoAimMaxDistance) : 0f;

        public static float GetAutoAimRadius(WeaponDataSO weapon, float maximum)
        {
            if (weapon == null) return 0f;
            float reach = weapon.Type == WeaponType.Melee ? weapon.AttackRange * 1.8f : weapon.AttackRange;
            return Mathf.Max(0f, Mathf.Min(maximum, reach));
        }
        public IReadOnlyList<WeaponDataSO> Inventory => weaponInventory;

        /// <summary>
        /// Trả về thông tin vũ khí tiếp theo trong danh sách nếu đổi vũ khí (để tương thích ngược).
        /// </summary>
        public WeaponDataSO NextWeaponData => null;

        /// <summary>Phát sự kiện khi đổi vũ khí mới (dùng cập nhật UI HUD/Nút bấm).</summary>
        public event Action<WeaponDataSO> OnWeaponChanged;

        /// <summary>Phát sự kiện khi Player thực hiện một đòn tấn công.</summary>
        public event Action OnAttackExecuted;
        public event Action<RuneSkill> OnRuneCast;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _inventory = GetComponent<PlayerInventory>();
            _aimIndicator = GetComponent<AimRangeIndicator>() ?? gameObject.AddComponent<AimRangeIndicator>();

            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }
        }

        private void Start()
        {
            if (_inventory != null)
            {
                _inventory.OnHotbarSelectionChanged += HandleHotbarSelectionChanged;
                HandleHotbarSelectionChanged(_inventory.SelectedHotbarIndex, _inventory.SelectedItem);
            }
            else if (weaponInventory.Count > 0)
            {
                EquipWeapon(currentWeaponIndex);
            }
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.OnHotbarSelectionChanged -= HandleHotbarSelectionChanged;
            }
        }

        private void HandleHotbarSelectionChanged(int slotIndex, ItemDefinitionSO item)
        {
            if (item is WeaponDataSO weapon)
            {
                EquipWeapon(weapon);
            }
            else
            {
                UnequipCurrentWeapon();
                OnWeaponChanged?.Invoke(null);
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
            // Vũ khí bắn tự động (Súng Tiểu Liên): giữ nút tấn công là bắn liên tục theo tốc bắn.
            if (_runeInputMode||!_isInputActive || attackAction == null || attackAction.action == null) return;
            WeaponDataSO current = CurrentWeaponData;
            bool holdingConsumable = _inventory != null && _inventory.SelectedItem is ConsumableDataSO;
            if (current != null && current.AutoFire && !holdingConsumable && attackAction.action.IsPressed())
            {
                PerformAttack();
            }
        }

        private void OnAttackInput(InputAction.CallbackContext context)
        {
            if (!_isInputActive) return;
            // In the actual rune HUD, the old Attack action is only the Use
            // button for a selected consumable. No keyboard/gamepad auto-fire
            // bypasses recognition for the equipped weapon.
            if(_runeInputMode&&!ConsumableSelected)return;
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
            EquipWeapon(data);
        }

        public void EquipWeapon(WeaponDataSO data)
        {
            if (data == null)
            {
                UnequipCurrentWeapon();
                OnWeaponChanged?.Invoke(null);
                return;
            }

            if (_activeWeapon != null && _activeWeapon.Data == data) return;
            // Xóa vũ khí cũ trên tay
            UnequipCurrentWeapon();

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

        public void UnequipCurrentWeapon()
        {
            _runeContextVersion++;
            if (_aimIndicator != null) _aimIndicator.Hide();
            if (_activeWeaponInstance != null)
            {
                if(_activeWeapon != null) _activeWeapon.OnUnequipped();
                _activeWeaponInstance.SetActive(false);
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
        }

        public void SetRuneInputMode(bool enabled)
        {
            if(_runeInputMode==enabled)return;
            _runeInputMode=enabled;_runeContextVersion++;_movement?.SetRuneDashMode(enabled);
        }

        private static string RuneKey(WeaponDataSO data,RuneSkill skill)=>"combat.rune."+data.ItemId+"."+RuneGlyphs.Id(skill.Glyph);
        public float RuneCooldownDuration(RuneSkill skill)
        {
            if(skill!=null&&skill.Form==RuneForm.Dash)return _movement!=null?_movement.EffectiveDashCooldown:skill.Cooldown;
            if(skill==null||CurrentWeaponData==null||_activeWeapon==null)return 0;
            float stat=GetComponent<PlayerStats>()?.Get(StatType.AttackCooldown)??1;
            float adjusted=skill.BaseCooldown(CurrentWeaponData)*(skill.Basic?stat:Mathf.Clamp(stat,.85f,1.2f));
            return Mathf.Max(.35f,adjusted,skill.Basic?_activeWeapon.EffectiveAttackInterval:0f);
        }

        public float RuneCooldownRemaining(RuneSkill skill)
        {
            if(skill!=null&&skill.Form==RuneForm.Dash)return _movement!=null?_movement.DashCooldownRemaining:0f;
            if(skill==null||CurrentWeaponData==null)return 0;
            return Mathf.Max(RunState.CooldownRemaining(GlobalAttackKey),CooldownFor(CurrentWeaponData),
                RunState.CooldownRemaining(RuneKey(CurrentWeaponData,skill)),RunState.CooldownRemaining(RuneSkillExecutor.FamilyKey(skill)),
                WeaponTactics.CastCooldownRemaining(RuneSkillExecutor.TacticalKind(skill.Form)));
        }

        private float RuneAimRadius(RuneSkill skill,WeaponDataSO data)
        {
            float reach=skill.Reach(data);
            if(skill.Form==RuneForm.Weapon&&data.Type==WeaponType.Melee)reach*=1.8f;
            if(skill.Form==RuneForm.Blink||skill.Form==RuneForm.StoneWall||skill.Form==RuneForm.IceWall)reach=Mathf.Max(reach,AutoAimRadius);
            return Mathf.Clamp(reach,0f,autoAimMaxDistance);
        }

        /// <summary>
        /// Only the active weapon's four authored glyphs and the shared dodge can dispatch. The HUD
        /// calls this after a successful model result. Cooldowns persist in
        /// RunState across item swaps and scene changes.
        /// </summary>
        public bool TryCastRune(RuneGlyph glyph,out string feedback)
        {
            feedback="Không thể thi triển";
            if(!RuneInputReady)return false;
            if(glyph==RuneSkillCatalog.DashSkill.Glyph)
            {
                var dodge=RuneSkillCatalog.DashSkill;
                float remainingDash=RuneCooldownRemaining(dodge);
                if(remainingDash>.025f){feedback=dodge.Name+" · Hồi "+remainingDash.ToString("0.0")+"s";return false;}
                // Do not share the attack lock: a defensive dodge must remain
                // usable immediately after an attack, and swapping cannot reset it.
                if(_movement==null||!_movement.TryPerformDash())return false;
                OnRuneCast?.Invoke(dodge);feedback=dodge.Name;return true;
            }
            if(ConsumableSelected){feedback="Chọn vũ khí để thi triển chiêu";return false;}
            var data=CurrentWeaponData;var skill=RuneSkillCatalog.Find(data,glyph);
            if(skill==null){feedback="Vũ khí không có cổ ngữ này";return false;}
            float remaining=RuneCooldownRemaining(skill);
            if(remaining>.025f)
            {feedback=skill.Name+" · Hồi "+remaining.ToString("0.0")+"s";return false;}
            if(!_activeWeapon.CanAttack){feedback="Đang hồi đòn";return false;}

            float range=RuneAimRadius(skill,data);
            Transform target=autoAimEnabled&&range>0?FindNearestEnemy(range):null;
            Vector3 aim=transform.forward;
            if(target!=null)
            {
                Vector3 delta=target.position-transform.position;delta.y=0;
                if(delta.sqrMagnitude>.01f){aim=delta.normalized;_movement?.RotateTowards(aim);}
            }
            float cooldown=RuneCooldownDuration(skill);
            float recovery=Mathf.Max(.50f,_activeWeapon.EffectiveAttackInterval);
            if(skill.Basic)recovery=Mathf.Max(recovery,cooldown);
            if(data.Type==WeaponType.Melee&&skill.Form==RuneForm.Weapon)
                recovery=Mathf.Max(recovery,(data.StrikeCount+skill.ExtraHits-1)*data.StrikeInterval+.32f);
            if(!RuneSkillExecutor.Execute(_activeWeapon,attackOrigin,aim,target,enemyLayer,gameObject,skill,cooldown,out feedback))
            {
                if(string.IsNullOrEmpty(feedback))feedback="Chưa thể thi triển "+skill.Name;
                return false;
            }
            RunState.StartCooldown(RuneKey(data,skill),Mathf.Max(cooldown,recovery));
            RunState.StartCooldown(GlobalAttackKey,recovery);
            RunState.StartCooldown("weapon."+data.ItemId,recovery);
            RunState.StartCooldown(RuneSkillExecutor.FamilyKey(skill),RuneSkillExecutor.FamilyDelay(skill));
            if(range>.05f)_aimIndicator.Show(range);
            OnAttackExecuted?.Invoke();OnRuneCast?.Invoke(skill);
            feedback=skill.Name;
            if(GameSettings.VibrationEnabled)
            {
                #if UNITY_ANDROID && !UNITY_EDITOR
                Handheld.Vibrate();
                #endif
            }
            return true;
        }

        public void PerformAttack()
        {
            if (!_isInputActive || Time.timeScale <= 0f || (_movement!=null&&_movement.IsDashing)) return;
            if (RunState.CooldownRemaining(GlobalAttackKey) > 0f)
            {
                if(CurrentWeaponData!=null) _aimIndicator.Show(AutoAimRadius);
                return;
            }
            // Nếu ô đang chọn trên thanh nhanh là đồ dùng (ví dụ: Bình máu) -> uống hồi máu
            if (_inventory != null && _inventory.SelectedItem is ConsumableDataSO consumable)
            {
                if (consumable.Use(gameObject))
                {
                    _inventory.TryRemoveFromSlot(_inventory.SelectedSlotKey, 1);
                    RunState.StartCooldown(GlobalAttackKey,.45f);
                    OnAttackExecuted?.Invoke();
                }
                return;
            }

            if (_activeWeapon == null) return;
            _aimIndicator.Show(AutoAimRadius);
            if (!_activeWeapon.CanAttack || CooldownFor(CurrentWeaponData)>0f) return;

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

            var firedWeapon=CurrentWeaponData;
            // The cast and the ordinary shot/strike share the same input, but a stronger
            // tactical action has its own longer cooldown even when the weapon is swapped.
            WeaponTactics.TryCast(firedWeapon, gameObject, aimDirection);
            bool success = _activeWeapon.ExecuteAttack(attackOrigin, aimDirection, enemyLayer, gameObject);
            if (success)
            {
                float interval=_activeWeapon.EffectiveAttackInterval;
                RunState.StartCooldown("weapon."+firedWeapon.ItemId,interval);
                RunState.StartCooldown(GlobalAttackKey, interval);
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

        private Transform FindNearestEnemy(float searchRadius=-1)
        {
            if(searchRadius<0)searchRadius=AutoAimRadius;
            if (searchRadius <= 0f) return null;

            Collider[] hits = Physics.OverlapCapsule(transform.position, transform.position + Vector3.up * 2f,
                searchRadius, enemyLayer, QueryTriggerInteraction.Ignore);
            Transform nearest = null;
            float minDist = float.MaxValue;

            foreach (var col in hits)
            {
                IDamageable damageable = col.GetComponent<IDamageable>();
                if (damageable == null) damageable = col.GetComponentInParent<IDamageable>();
                if (damageable == null || damageable.IsDead || WeaponHitResolver.IsFriendly(gameObject,damageable)) continue;

                var target = damageable as Component;
                Transform targetTransform = target != null ? target.transform : col.transform;
                Vector3 offset = targetTransform.position - transform.position;
                offset.y = 0f;
                float dist = offset.magnitude;
                if (dist > searchRadius) continue;
                if(Physics.Linecast(transform.position+Vector3.up*.85f,col.bounds.center,1<<8,QueryTriggerInteraction.Ignore))continue;
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = targetTransform;
                }
            }

            return nearest;
        }

        public void SetInputActive(bool active)
        {
            if(_isInputActive!=active)_runeContextVersion++;
            _isInputActive = active;
            if (!active && _aimIndicator != null) _aimIndicator.Hide();
        }

        public void AddWeapon(WeaponDataSO newWeapon)
        {
            if (newWeapon == null || weaponInventory.Contains(newWeapon)) return;
            weaponInventory.Add(newWeapon);
        }
    }
}
