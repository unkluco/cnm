using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using LightSouls.Common;
using LightSouls.Combat.Bosses;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Weapons;

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
    public abstract class EnemyBase : MonoBehaviour, IDamageable, ISpeedModifiable, IStunnable
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
        private float _lastFeedbackHealth;

        protected Quaternion _defaultTorsoRot = Quaternion.identity;
        protected Quaternion _defaultRightArmRot = Quaternion.identity;
        protected Quaternion _defaultLeftArmRot = Quaternion.identity;
        protected Quaternion _defaultLeftLegRot = Quaternion.identity, _defaultRightLegRot = Quaternion.identity;
        private float _controlReadyAt, charmUntil, charmAttackAt, nextCharmSearch, charmSwingUntil;
        private GameObject charmOwner;
        private EnemyBase charmTarget;
        private ParticleSystem charmAura;
        public bool IsCharmed => charmOwner!=null && Time.time<charmUntil && !IsDead;
        public float EffectiveEnemyDamage => damage;
        public float EffectiveEnemyRecovery => attackCooldown;

        public virtual void ApplyChallenge(float hpScale,float damageScale,float recoveryScale)
        {
            if(_health!=null)_health.ScaleMaximum(hpScale);
            damage*=damageScale;attackCooldown=Mathf.Max(.48f,attackCooldown*recoveryScale);
            moveSpeed*=Mathf.Clamp(1f+(hpScale-1f)*.22f,1f,1.18f);UpdateEffectiveSpeed();
        }

        // Triển khai IDamageable ủy quyền cho HealthComponent
        public virtual string DisplayName => enemyName;
        protected virtual float MaximumColliderHeight => 2f;
        protected virtual float MaximumColliderRadius => .5f;
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

            // Quái thường giữ kích cỡ humanoid. Boss khai báo kích thước collider riêng.
            _collider.height = Mathf.Min(MaximumColliderHeight, _collider.height);
            _collider.radius = Mathf.Min(MaximumColliderRadius, _collider.radius);

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
            if (leftLeg != null) _defaultLeftLegRot = leftLeg.localRotation;
            if (rightLeg != null) _defaultRightLegRot = rightLeg.localRotation;
        }

        protected virtual void Start()
        {
            if (_health != null)
            {
                _lastFeedbackHealth = _health.CurrentHealth;
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

        private float _stunnedUntil = -1f;

        public bool IsStunned => Time.time < _stunnedUntil;

        public void Stun(float duration)
        {
            TryStun(duration,out _);
        }

        // A boss may be interrupted briefly by an existing stun; normal enemies get a
        // recovery window so swapping between freeze and stun cannot permanently lock them.
        public bool TryStun(float duration,out float applied)
        {
            applied=0f;
            if(IsDead||duration<=0f||Time.time<_controlReadyAt||IsCharmed)return false;
            applied=Mathf.Min(duration,this is BossEnemy ? .32f : 1.85f);
            _stunnedUntil=Time.time+applied;_controlReadyAt=_stunnedUntil+(this is BossEnemy?5f:1.35f);
            CancelPendingAttack();
            return true;
        }

        protected virtual void CancelPendingAttack(){_isAttackingLimbAnim=false;}

        public bool TryCharm(GameObject owner,float duration,float healthThreshold)
        {
            if(owner==null||!owner.CompareTag("Player")||IsDead||IsCharmed||this is BossEnemy||_collider==null||_collider.height>2.1f||
                CurrentHealth>MaxHealth*healthThreshold) return false;
            if(this is SpecialEnemy special && (special.Kind==SpecialEnemyKind.StoneGolem||special.Kind==SpecialEnemyKind.ShieldGuard||special.Kind==SpecialEnemyKind.BombWisp))return false;
            bool otherHostile=false;
            foreach(var enemy in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            {
                if(enemy==this||enemy.IsDead)continue;
                if(enemy.IsCharmed && enemy.charmOwner==owner)return false;
                if(!enemy.IsCharmed && Vector3.Distance(enemy.transform.position,transform.position)<24f)otherHostile=true;
            }
            if(!otherHostile)return false;
            CancelPendingAttack();_stunnedUntil=0;
            GetComponent<StatusEffectReceiver>()?.Cleanse();
            charmOwner=owner;charmUntil=Time.time+Mathf.Clamp(duration,2f,8f); charmAttackAt=Time.time+.5f;nextCharmSearch=0;charmTarget=null;charmSwingUntil=0;
            if(_agent!=null&&_agent.isOnNavMesh)_agent.ResetPath();
            charmAura=VfxFactory.Aura(transform,new Color(.48f,.96f,.74f),22f,.64f,Vector3.up*.95f,.13f,.4f,"CharmedAlly");
            var ring=VfxFactory.Ring(transform.position,new Color(.48f,.96f,.74f),1.0f,.45f); // one-shot conversion
            return true;
        }

        public void EndCharm()
        {
            if(charmOwner==null && charmAura==null)return;
            charmOwner=null;charmTarget=null;charmUntil=0;charmSwingUntil=0;_lastAttackTime=Time.time;
            if(rightArm!=null)rightArm.localRotation=_defaultRightArmRot;
            if(charmAura!=null)Destroy(charmAura.gameObject);charmAura=null;
            if(_agent!=null&&_agent.isOnNavMesh)_agent.ResetPath();
        }

        private void UpdateCharmedBehavior()
        {
            if(_agent==null||!_agent.isOnNavMesh)return;
            if(Time.time>=nextCharmSearch||charmTarget==null||charmTarget.IsDead||charmTarget.IsCharmed)
            {
                nextCharmSearch=Time.time+.35f;charmTarget=null;float best=24f*24f;
                foreach(var enemy in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
                {
                    if(enemy==this||enemy.IsDead||enemy.IsCharmed)continue;
                    float sq=(enemy.transform.position-transform.position).sqrMagnitude;if(sq>=best)continue;
                    charmTarget=enemy;best=sq;
                }
            }
            if(charmTarget==null)
            {
                _agent.isStopped=Vector3.Distance(transform.position,charmOwner.transform.position)<1.75f;
                if(!_agent.isStopped)_agent.SetDestination(charmOwner.transform.position);return;
            }
            Vector3 delta=charmTarget.transform.position-transform.position;delta.y=0;
            float reach=1.75f+(charmTarget is BossEnemy boss?boss.Definition.Radius:.25f);
            bool clear=!Physics.Linecast(transform.position+Vector3.up*.9f,charmTarget.transform.position+Vector3.up*.9f,1<<8,QueryTriggerInteraction.Ignore);
            if(delta.magnitude>reach||!clear){_agent.isStopped=false;_agent.SetDestination(charmTarget.transform.position);return;}
            _agent.isStopped=true;
            if(delta.sqrMagnitude>.001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(delta),Time.deltaTime*14f);
            if(Time.time>=charmAttackAt)
            {
                charmAttackAt=Time.time+1.05f;
                charmSwingUntil=Time.time+.29f;
                float amount=Mathf.Clamp(damage*.70f,6f,17f);
                SwordSlashWave.Spawn(transform.position+Vector3.up*.75f,transform.forward,reach*.75f,80f,new Color(.48f,.96f,.74f),CombatVfxElement.Life);
                WeaponHitResolver.Apply(null,amount,charmOwner,charmTarget,charmTarget.transform.position+Vector3.up*.75f,false,delta,CombatVfxElement.Life,new Color(.48f,.96f,.74f));
            }
        }

        protected virtual void Update()
        {
            if (_isDead) return;
            if(IsCharmed)
            {
                UpdateCharmedBehavior();AnimateLimbsAndTorso();
                if(rightArm!=null&&Time.time<charmSwingUntil)
                {
                    float t=1f-Mathf.Clamp01((charmSwingUntil-Time.time)/.29f);
                    rightArm.localRotation=_defaultRightArmRot*Quaternion.Euler(65f*Mathf.Sin(t*Mathf.PI),-22f*Mathf.Sin(t*Mathf.PI),0);
                }
                return;
            }
            if(charmOwner!=null||charmAura!=null)EndCharm();

            // Bị choáng: đứng yên, không đuổi, không tấn công.
            if (IsStunned)
            {
                if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
                return;
            }

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
                if (leftLeg != null) leftLeg.localRotation = _defaultLeftLegRot*Quaternion.Euler(cycle * 30f, 0f, 0f);
                if (rightLeg != null) rightLeg.localRotation = _defaultRightLegRot*Quaternion.Euler(-cycle * 30f, 0f, 0f);

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

                if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, _defaultLeftLegRot, 8f * Time.deltaTime);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, _defaultRightLegRot, 8f * Time.deltaTime);

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
            if (leftLeg != null) _defaultLeftLegRot = leftLeg.localRotation;
            if (rightLeg != null) _defaultRightLegRot = rightLeg.localRotation;
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
            bool tookDamage = current < _lastFeedbackHealth - .001f;
            _lastFeedbackHealth = current;
            if (!_isDead && tookDamage)
            {
                // Nháy đỏ khi nhận sát thương
                if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
                _flashCoroutine = StartCoroutine(FlashRedRoutine());
            }
        }

        private IEnumerator FlashRedRoutine()
        {
            var block = new MaterialPropertyBlock();
            float snap = Mathf.Min(.045f, flashDuration * .4f);
            SetFlashColor(block, Color.Lerp(hitFlashColor, Color.white, .72f), .72f);
            yield return new WaitForSeconds(snap);
            SetFlashColor(block, hitFlashColor, .52f);
            yield return new WaitForSeconds(Mathf.Max(0f, flashDuration - snap));

            if (meshRenderers != null && _originalColors != null)
            {
                for (int i = 0; i < meshRenderers.Length; i++)
                {
                    if (meshRenderers[i] != null && meshRenderers[i].sharedMaterial != null && i < _originalColors.Length)
                    {
                        meshRenderers[i].GetPropertyBlock(block);
                        block.SetColor("_BaseColor", _originalColors[i]);
                        block.SetColor("_Color", _originalColors[i]);
                        meshRenderers[i].SetPropertyBlock(block);
                    }
                }
            }
            _flashCoroutine = null;
        }

        private void SetFlashColor(MaterialPropertyBlock block, Color tint, float strength)
        {
            if (meshRenderers != null)
            {
                for (int i = 0; i < meshRenderers.Length; i++)
                {
                    var r = meshRenderers[i];
                    if (r != null && r.sharedMaterial != null)
                    {
                        Color original = _originalColors != null && i < _originalColors.Length ? _originalColors[i] : tint;
                        Color blended = Color.Lerp(original, tint, strength);
                        r.GetPropertyBlock(block);
                        block.SetColor("_BaseColor", blended);
                        block.SetColor("_Color", blended);
                        r.SetPropertyBlock(block);
                    }
                }
            }
        }

        protected virtual void HandleDied(IDamageable damageable)
        {
            if (_isDead) return;
            _isDead = true;
            EndCharm(); CancelPendingAttack();

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
            Vector3 startScale=transform.localScale;
            Vector3 targetPos = startPos - Vector3.up * 0.8f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
