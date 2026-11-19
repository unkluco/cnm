using System.Collections;
using UnityEngine;
using LightSouls.Common;
using LightSouls.Combat.Effects;

namespace LightSouls.Combat.Enemies
{
    /// <summary>
    /// Quái cận chiến (Melee Enemy): Zombie (trâu bò, chậm) hoặc Goblin (nhanh, áp sát chém dồn).
    /// Tiếp cận Player bằng NavMeshAgent và tung nhát chém/cắn khi trong tầm đánh.
    /// </summary>
    public class MeleeEnemy : EnemyBase
    {
        [Header("Chi tiết Cận chiến")]
        [SerializeField] private float hitRadius = 1.0f;
        [SerializeField] private Transform attackPoint;
        [SerializeField] private Transform visualArm;

        private bool _isAttacking = false;
        private static readonly Color WarningColor = new Color(1f, .44f, .13f, .9f);
        private ArcaneTelegraph warning;

        protected override void HandleBehavior(float distanceToPlayer)
        {
            if (_isAttacking)
            {
                LookAtPlayer();
                return;
            }

            if (distanceToPlayer > attackRange)
            {
                // Đuổi theo Người chơi
                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = false;
                    _agent.SetDestination(_playerTransform.position);
                }
            }
            else
            {
                // Trong tầm đánh: dừng lại, xoay mặt và tấn công
                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                }

                LookAtPlayer();

                if (Time.time >= _lastAttackTime + attackCooldown)
                {
                    ExecuteAttack();
                }
            }
        }

        protected override void ExecuteAttack()
        {
            _lastAttackTime = Time.time;
            StartCoroutine(AttackRoutine());
        }

        private IEnumerator AttackRoutine()
        {
            _isAttacking = true;
            _isAttackingLimbAnim = true;

            // Nhấc cánh tay / lùi nhẹ chuẩn bị chém (Wind-up 0.2s)
            Quaternion initialArmRot = visualArm != null ? visualArm.localRotation : Quaternion.identity;
            if (visualArm != null)
            {
                visualArm.localRotation = initialArmRot * Quaternion.Euler(-45f, 0f, 0f);
            }

            float resolveAt = Time.time + .2f;
            Vector3 ground = AttackCenter(); ground.y = transform.position.y;
            warning = ArcaneTelegraph.Circle("EnemyWarning_Melee", ground, hitRadius, WarningColor, .2f,
                CombatVfxElement.Steel, TelegraphStyle.Danger, transform);
            while (Time.time < resolveAt && !IsDead && isActiveAndEnabled)
            {
                ground = AttackCenter(); ground.y = transform.position.y;
                if (warning != null) warning.SetCircle(ground, hitRadius);
                yield return null;
            }
            ClearWarning();
            if (IsDead || !isActiveAndEnabled) { _isAttacking = _isAttackingLimbAnim = false; yield break; }

            // Bổ mạnh cánh tay về phía trước (Strike)
            if (visualArm != null)
            {
                visualArm.localRotation = initialArmRot * Quaternion.Euler(55f, 0f, 0f);
            }

            // Điểm kiểm tra sát thương (Attack Point)
            Vector3 center = AttackCenter();
            SwordSlashWave.Spawn(transform.position + Vector3.up * .85f, transform.forward,
                Mathf.Max(1f, attackRange * .82f), 85f, WarningColor, CombatVfxElement.Steel);

            // Layer 6: Player theo quy ước dự án
            int playerLayerMask = 1 << 6;
            Collider[] hits = Physics.OverlapSphere(center, hitRadius, playerLayerMask);

            foreach (var col in hits)
            {
                IDamageable damageable = col.GetComponent<IDamageable>();
                if (damageable == null)
                {
                    damageable = col.GetComponentInParent<IDamageable>();
                }

                if (damageable != null && !damageable.IsDead)
                {
                    DamageInfo info = new DamageInfo
                    {
                        Amount = damage,
                        Source = gameObject,
                        HitPoint = col.ClosestPoint(center)
                    };

                    damageable.TakeDamage(info);
                    VfxFactory.HitImpact(info.HitPoint, transform.forward, WarningColor, CombatVfxElement.Steel, .36f);
                    CombatImpactFeedback.Emit(info.HitPoint, .32f);
                    break;
                }
            }

            yield return new WaitForSeconds(0.2f);

            // Thu tay về vị trí cũ
            if (visualArm != null)
            {
                visualArm.localRotation = initialArmRot;
            }

            _isAttacking = false;
            _isAttackingLimbAnim = false;
        }

        private Vector3 AttackCenter() => attackPoint != null ? attackPoint.position :
            transform.position + transform.forward * (attackRange * .7f) + Vector3.up * .9f;

        private void ClearWarning()
        {
            if (warning != null) warning.Remove(); warning = null;
        }

        protected override void HandleDied(IDamageable damageable) { ClearWarning(); base.HandleDied(damageable); }
        protected override void OnDestroy() { ClearWarning(); base.OnDestroy(); }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = attackPoint != null 
                ? attackPoint.position 
                : transform.position + transform.forward * (attackRange * 0.7f) + Vector3.up * 0.9f;

            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawWireSphere(center, hitRadius);
        }
    }
}
