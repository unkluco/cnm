using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using LightSouls.Common;

namespace LightSouls.Combat.Enemies
{
    /// <summary>
    /// Lớp cơ sở trừu tượng cho mọi loại quái vật trong Light Souls.
    /// Quản lý di chuyển bằng NavMeshAgent (Humanoid), nhận diện Player,
    /// nhận sát thương, nháy đỏ và vòng đời khi chết.
    /// Cài đặt IDamageable (ủy quyền qua HealthComponent) để đòn đánh tìm thấy ở mọi cấp.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(HealthComponent))]
    public abstract class EnemyBase : MonoBehaviour, IDamageable, ISpeedModifiable
    {
        [Header("Thông số cơ bản")]
        [SerializeField] protected string enemyName = "Quái Vật";
        [SerializeField] protected float moveSpeed = 3.0f;
        [SerializeField] protected float attackRange = 1.5f;
        [SerializeField] protected float attackCooldown = 1.0f;
        [SerializeField] protected float damage = 12f;
        [SerializeField] protected float aggroRadius = 25f;

        [Header("Hiệu ứng thị giác")]
        [SerializeField] protected Renderer[] meshRenderers;
        [SerializeField] protected Color hitFlashColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] protected float flashDuration = 0.12f;

        [Header("Khớp chi mô hình quái (Procedural Animation)")]
        [SerializeField] protected Transform torso;
        [SerializeField] protected Transform leftArm;
        [SerializeField] protected Transform rightArm;
        [SerializeField] protected Transform leftLeg;
        [SerializeField] protected Transform rightLeg;
        [SerializeField] protected float walkCycleSpeed = 10f;
        [SerializeField] protected float forwardLeanAngle = 12f;

        protected NavMeshAgent _agent;
        protected CapsuleCollider _collider;
        protected HealthComponent _health;
        protected Transform _playerTransform;
        protected PlayerController _playerController;

        protected float _lastAttackTime = -999f;
        protected bool _isDead = false;
        protected bool _isAttackingLimbAnim = false;
        private Color[] _originalColors;
        private Coroutine _flashCoroutine;

        protected Quaternion _defaultTorsoRot = Quaternion.identity;
        protected Quaternion _defaultRightArmRot = Quaternion.identity;
        protected Quaternion _defaultLeftArmRot = Quaternion.identity;

        // Triển khai IDamageable ủy quyền cho HealthComponent
        public bool IsDead => _health != null ? _health.IsDead : _isDead;
        public float CurrentHealth => _health != null ? _health.CurrentHealth : 0f;
        public float MaxHealth => _health != null ? _health.MaxHealth : 0f;

        public event Action<IDamageable> Died
        {
            add { if (_health != null) _health.Died += value; }
            remove { if (_health != null) _health.Died -= value; }
        }

        protected readonly System.Collections.Generic.Dictionary<object, float> _speedMultipliers = new System.Collections.Generic.Dictionary<object, float>();

        public void SetSpeedMultiplier(object source, float multiplier)
        {
            if (source == null) return;
            _speedMultipliers[source] = Mathf.Max(0f, multiplier);
            UpdateEffectiveSpeed();
        }

        public void RemoveSpeedMultiplier(object source)
        {
            if (source == null) return;
            _speedMultipliers.Remove(source);
            UpdateEffectiveSpeed();
        }

        protected void UpdateEffectiveSpeed()
        {
            if (_agent == null) return;
            float total = 1f;
            foreach (var kvp in _speedMultipliers)
            {
                total *= kvp.Value;
            }
            _agent.speed = moveSpeed * Mathf.Max(0f, total);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (_isDead) return;
            if (_health != null)
            {
                _health.TakeDamage(info);
            }
        }

        protected virtual void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _collider = GetComponent<CapsuleCollider>();
            _health = GetComponent<HealthComponent>();

            // Quy chuẩn: Tag "Enemy", Layer 7 ("Enemy")
            gameObject.tag = "Enemy";
            gameObject.layer = 7;

            // Quy chuẩn kích thước: cao ≤ 2m, bán kính ≤ 0.5m
            _collider.height = Mathf.Min(2.0f, _collider.height);
            _collider.radius = Mathf.Min(0.5f, _collider.radius);

            // Cấu hình NavMeshAgent chuẩn Humanoid
            if (_agent != null)
            {
                _agent.speed = moveSpeed;
                _agent.stoppingDistance = attackRange * 0.85f;
                _agent.height = _collider.height;
                _agent.radius = _collider.radius;
            }

            // Lưu màu gốc để nháy đỏ khi trúng đòn
            if (meshRenderers == null || meshRenderers.Length == 0)
            {
                meshRenderers = GetComponentsInChildren<Renderer>();
            }

            if (meshRenderers != null && meshRenderers.Length > 0)
            {
                _originalColors = new Color[meshRenderers.Length];
                for (int i = 0; i < meshRenderers.Length; i++)
                {
                    if (meshRenderers[i] != null && meshRenderers[i].sharedMaterial != null)
                    {
                        _originalColors[i] = meshRenderers[i].sharedMaterial.color;
                    }
                }
            }
            if (torso != null) _defaultTorsoRot = torso.localRotation;
            if (rightArm != null) _defaultRightArmRot = rightArm.localRotation;
            if (leftArm != null) _defaultLeftArmRot = leftArm.localRotation;
        }

        protected virtual void Start()
        {
            if (_health != null)
            {
                _health.Died += HandleDied;
                _health.OnHealthChanged += HandleHealthChanged;
            }

            FindPlayer();
        }

        protected virtual void OnDestroy()
        {
            if (_health != null)
            {
                _health.Died -= HandleDied;
                _health.OnHealthChanged -= HandleHealthChanged;
            }
        }

        protected virtual void Update()
        {
            if (_isDead) return;

            // Nếu chưa tìm thấy hoặc Player đã chết thì quét lại
            if (_playerTransform == null || (_playerController != null && _playerController.IsDead))
            {
                FindPlayer();
                if (_playerTransform == null)
                {
                    if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
                    return;
                }
            }

            float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
            if (distanceToPlayer <= aggroRadius)
            {
                HandleBehavior(distanceToPlayer);
            }
            else
            {
                // Ngoài tầm phát hiện: đứng chờ
                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                }
            }

            AnimateLimbsAndTorso();
        }

        /// <summary>
        /// Diễn hoạt cử động chân, tay và thân người nhấp nhô, nghiêng về phía trước khi quái di chuyển.
        /// </summary>
        protected virtual void AnimateLimbsAndTorso()
        {
            bool isMoving = _agent != null && _agent.isOnNavMesh && _agent.velocity.sqrMagnitude > 0.05f;

            if (isMoving)
            {
                float speedMultiplier = Mathf.Clamp(_agent.velocity.magnitude / Mathf.Max(0.1f, moveSpeed), 0.6f, 1.4f);
                float cycle = Mathf.Sin(Time.time * walkCycleSpeed * speedMultiplier);
                float bobCycle = Mathf.Sin(Time.time * walkCycleSpeed * 2f * speedMultiplier);

                // 1. Chân bước tới lui luân phiên
                if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(cycle * 30f, 0f, 0f);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-cycle * 30f, 0f, 0f);

                // 2. Tay vung tự nhiên khi không trong trạng thái vung đòn tấn công
                if (!_isAttackingLimbAnim)
                {
                    if (leftArm != null) leftArm.localRotation = _defaultLeftArmRot * Quaternion.Euler(-cycle * 22f, 0f, 0f);
                    if (rightArm != null) rightArm.localRotation = _defaultRightArmRot * Quaternion.Euler(cycle * 22f, 0f, 0f);
                }

                // 3. Thân người (Torso) nghiêng về phía trước và nhấp nhô theo nhịp di chuyển
                if (torso != null)
                {
                    float sway = bobCycle * 2.5f;
                    torso.localRotation = _defaultTorsoRot * Quaternion.Euler(forwardLeanAngle + sway, 0f, 0f);
                }
            }
            else
            {
                // Khi đứng yên (Idle): hồi phục tư thế tự nhiên, thân người thở nhẹ
                float idleBreathing = Mathf.Sin(Time.time * 2f) * 1.5f;

                if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, Quaternion.identity, 8f * Time.deltaTime);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, Quaternion.identity, 8f * Time.deltaTime);

                if (!_isAttackingLimbAnim)
                {
                    if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, _defaultLeftArmRot, 8f * Time.deltaTime);
                    if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, _defaultRightArmRot, 8f * Time.deltaTime);
                }

                if (torso != null)
                {
                    Quaternion targetIdle = _defaultTorsoRot * Quaternion.Euler(idleBreathing, 0f, 0f);
                    torso.localRotation = Quaternion.Slerp(torso.localRotation, targetIdle, 8f * Time.deltaTime);
                }
            }
        }

        public void SetLimbReferences(Transform tTorso, Transform tLeftArm, Transform tRightArm, Transform tLeftLeg, Transform tRightLeg, float leanAngle = 12f, float animSpeed = 10f)
        {
            torso = tTorso;
            leftArm = tLeftArm;
            rightArm = tRightArm;
            leftLeg = tLeftLeg;
            rightLeg = tRightLeg;
            forwardLeanAngle = leanAngle;
            walkCycleSpeed = animSpeed;

            if (torso != null) _defaultTorsoRot = torso.localRotation;
            if (rightArm != null) _defaultRightArmRot = rightArm.localRotation;
            if (leftArm != null) _defaultLeftArmRot = leftArm.localRotation;
        }

        /// <summary>
        /// Hành vi đặc trưng của từng loại quái (tiếp cận, né tránh, tấn công).
        /// </summary>
        protected abstract void HandleBehavior(float distanceToPlayer);

        /// <summary>
        /// Thực thi đòn tấn công đặc thù lên Người chơi.
        /// </summary>
        protected abstract void ExecuteAttack();

        protected void FindPlayer()
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
                _playerController = playerObj.GetComponent<PlayerController>();
            }
        }

        /// <summary>
        /// Xoay mặt nhìn về phía Người chơi (chỉ tính mặt phẳng X-Z).
        /// </summary>
        protected void LookAtPlayer()
        {
            if (_playerTransform == null) return;
            Vector3 direction = (_playerTransform.position - transform.position);
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (!_isDead && current < max)
            {
                // Nháy đỏ khi nhận sát thương
                if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
                _flashCoroutine = StartCoroutine(FlashRedRoutine());
            }
        }

        private IEnumerator FlashRedRoutine()
        {
            if (meshRenderers != null)
            {
                foreach (var r in meshRenderers)
                {
                    if (r != null && r.material != null)
                    {
                        r.material.color = hitFlashColor;
                    }
                }
            }

            yield return new WaitForSeconds(flashDuration);

            if (meshRenderers != null && _originalColors != null)
            {
                for (int i = 0; i < meshRenderers.Length; i++)
                {
                    if (meshRenderers[i] != null && meshRenderers[i].material != null && i < _originalColors.Length)
                    {
                        meshRenderers[i].material.color = _originalColors[i];
                    }
                }
            }
        }

        protected virtual void HandleDied(IDamageable damageable)
        {
            if (_isDead) return;
            _isDead = true;

            // Dừng agent ngay lập tức
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.enabled = false;
            }

            // Tắt collider để Player không bị vướng
            if (_collider != null)
            {
                _collider.enabled = false;
            }

            // Tự động hủy sau 1.2s kèm hiệu ứng hạ xuống đất
            StartCoroutine(DeathFadeRoutine());
        }

        private IEnumerator DeathFadeRoutine()
        {
            float elapsed = 0f;
            float duration = 1.0f;
            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos - Vector3.up * 0.8f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
                transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, elapsed / duration);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
