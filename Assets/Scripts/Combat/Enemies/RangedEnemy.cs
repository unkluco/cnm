using System.Collections;
using UnityEngine;
using LightSouls.Combat.Weapons;
using LightSouls.Combat.Effects;
using LightSouls.Common;

namespace LightSouls.Combat.Enemies
{
    /// <summary>
    /// Quái bắn xa (Ranged Enemy): Xạ thủ hoặc Pháp sư hắc ám (Dark Cultist).
    /// Duy trì khoảng cách lý tưởng, lùi lại nếu Player áp sát quá gần (Kiting),
    /// và bắn các quả cầu ma thuật / mũi tên trên Layer EnemyAttack (10).
    /// </summary>
    public class RangedEnemy : EnemyBase
    {
        [Header("Chi tiết Bắn xa")]
        [SerializeField] private float fleeDistance = 3.5f;
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float projectileSpeed = 12f;
        [SerializeField] private Transform visualStaff;

        private bool _isFiring = false;
        private static readonly Color ArcaneColor = new Color(.76f, .38f, 1f, .95f);
        private ArcaneTelegraph warning;
        private Coroutine fireRoutine;
        private Quaternion staffRest;

        protected override void HandleBehavior(float distanceToPlayer)
        {
            if (_isFiring)
            {
                return;
            }

            if (distanceToPlayer < fleeDistance)
            {
                // Người chơi áp sát quá gần: Lùi lại để giữ khoảng cách an toàn (Kiting)
                Vector3 fleeDir = (transform.position - _playerTransform.position).normalized;
                Vector3 fleeTarget = transform.position + fleeDir * 3.0f;

                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = false;
                    _agent.SetDestination(fleeTarget);
                }
                LookAtPlayer();
            }
            else if (distanceToPlayer > attackRange)
            {
                // Ngoài tầm bắn: tiến lại gần
                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = false;
                    _agent.SetDestination(_playerTransform.position);
                }
            }
            else
            {
                // Ở cự ly bắn lý tưởng: dừng lại, nhắm và bắn
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
            fireRoutine=StartCoroutine(FireRoutine());
        }

        private IEnumerator FireRoutine()
        {
            _isFiring = true;
            _isAttackingLimbAnim = true;

            // Nâng trượng / gậy phép nạp năng lượng (0.25s)
            Quaternion initialStaffRot = visualStaff != null ? visualStaff.localRotation : Quaternion.identity;
            staffRest=initialStaffRot;
            if (visualStaff != null)
            {
                visualStaff.localRotation = initialStaffRot * Quaternion.Euler(-30f, 0f, 0f);
            }

            GetAimLane(out Vector3 laneStart, out Vector3 laneEnd);
            const float windup=.42f;
            Vector3 lockedAim=_playerTransform.position+Vector3.up*.9f;
            warning = ArcaneTelegraph.Lane("EnemyWarning_Cultist", laneStart, laneEnd, .16f, ArcaneColor, windup, CombatVfxElement.Void, transform);
            float resolveAt = Time.time + windup;
            while (Time.time < resolveAt && !IsDead && !IsStunned && !IsCharmed && _playerTransform != null && isActiveAndEnabled)
            {
                yield return null;
            }
            ClearWarning();
            if (IsDead || IsStunned || IsCharmed || _playerTransform == null || !isActiveAndEnabled) { ResetPose(); yield break; }

            // Xác định hướng bắn về phía ngực Người chơi
            Vector3 spawnPos = muzzlePoint != null 
                ? muzzlePoint.position 
                : transform.position + transform.forward * 0.8f + Vector3.up * 1.0f;

            Vector3 targetAimPoint = lockedAim;
            Vector3 aimDir = (targetAimPoint - spawnPos).normalized;
            VfxFactory.Muzzle(spawnPos, aimDir, ArcaneColor, CombatVfxElement.Void);

            GameObject bulletObj;
            if (projectilePrefab != null)
            {
                bulletObj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(aimDir));
            }
            else
            {
                // Fallback tạo viên đạn ma thuật nếu chưa gắn prefab
                bulletObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulletObj.name = "Enemy_Bullet";
                bulletObj.transform.position = spawnPos;
                bulletObj.transform.rotation = Quaternion.LookRotation(aimDir);
                bulletObj.transform.localScale = Vector3.one * 0.35f;

                var col = bulletObj.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;

                Renderer r = bulletObj.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(0.7f, 0.1f, 0.9f, 1f); // Tím ma thuật

                bulletObj.AddComponent<Projectile>();
            }

            // Quy chuẩn Layer 10: EnemyAttack theo hợp đồng ma trận va chạm
            bulletObj.layer = 10;

            // Đảm bảo đạn có Rigidbody để kích hoạt trigger với Player
            Rigidbody rb = bulletObj.GetComponent<Rigidbody>();
            if (rb == null) rb = bulletObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // LayerMask chỉ nhắm vào Player (Layer 6)
            int playerLayerMask = 1 << 6;

            Projectile proj = bulletObj.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.Initialize(damage, projectileSpeed, 4.0f, playerLayerMask, gameObject);
                proj.SetVisualStyle(ArcaneColor, CombatVfxElement.Void, .19f);
            }

            // Hạ trượng về
            yield return new WaitForSeconds(0.15f);
            if (visualStaff != null)
            {
                visualStaff.localRotation = initialStaffRot;
            }

            _isFiring = false;
            _isAttackingLimbAnim = false;
            fireRoutine=null;
        }

        private void ResetPose(){if(visualStaff!=null&&_isFiring)visualStaff.localRotation=staffRest;_isFiring=false;_isAttackingLimbAnim=false;fireRoutine=null;}
        protected override void CancelPendingAttack(){if(fireRoutine!=null)StopCoroutine(fireRoutine);ClearWarning();ResetPose();}

        private void GetAimLane(out Vector3 start, out Vector3 end)
        {
            start = muzzlePoint != null ? muzzlePoint.position : transform.position + transform.forward * .65f;
            start.y = transform.position.y;
            Vector3 d = _playerTransform != null ? _playerTransform.position - start : transform.forward;
            d.y = 0f; if (d.sqrMagnitude < .001f) d = transform.forward;
            end = start + d.normalized * Mathf.Max(1f, Mathf.Min(d.magnitude + .6f, attackRange));
        }

        private void ClearWarning() { if (warning != null) warning.Remove(); warning = null; }
        protected override void HandleDied(IDamageable damageable) { ClearWarning(); base.HandleDied(damageable); }
        protected override void OnDestroy() { ClearWarning(); base.OnDestroy(); }
        private void OnDisable()=>CancelPendingAttack();
    }
}
