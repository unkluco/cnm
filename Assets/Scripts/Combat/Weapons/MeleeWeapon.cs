using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LightSouls.Common;
using LightSouls.Combat.Effects;

namespace LightSouls.Combat.Weapons
{
    /// <summary>
    /// Triển khai vũ khí cận chiến (Kiếm, Đại kiếm, Dao găm...).
    /// Quét sát thương hình quạt, diễn hoạt nhát chém kiếm (Sword Slash Animation)
    /// và tạo vệt kiếm trắng (Sword Slash Wave) kéo dài 0.5 giây biến mất dạng sóng mờ dần.
    /// </summary>
    public class MeleeWeapon : WeaponBase
    {
        [Header("Hiệu ứng visual cận chiến")]
        [SerializeField] private TrailRenderer slashTrail;
        [SerializeField] private GameObject slashWavePrefab;
        [SerializeField] private Transform visualBlade;

        private Quaternion _defaultLocalRot = Quaternion.identity;
        private Coroutine _swingCoroutine;

        public override void OnEquipped()
        {
            base.OnEquipped();
            _defaultLocalRot = transform.localRotation;

            if (slashTrail == null)
            {
                slashTrail = GetComponentInChildren<TrailRenderer>();
            }

            if (slashTrail != null)
            {
                slashTrail.time = 0.5f; // Vệt kéo dài 0.5 giây theo yêu cầu
                slashTrail.emitting = false;
            }
        }

        public override bool ExecuteAttack(Transform attackOrigin, Vector3 aimDirection, LayerMask targetLayer, GameObject attacker)
        {
            if (!CanAttack || weaponData == null) return false;

            lastAttackTime = Time.time;

            // 1. Kích hoạt vệt kiếm trắng (Slash Wave & Trail)
            SpawnSlashWave(attackOrigin, aimDirection);

            // 2. Diễn hoạt nhát chém vung kiếm
            if (_swingCoroutine != null) StopCoroutine(_swingCoroutine);
            _swingCoroutine = StartCoroutine(SwingRoutine());

            // 3. Quét sát thương hình quạt phía trước
            Vector3 center = attackOrigin.position + aimDirection.normalized * (weaponData.AttackRange * 0.5f);
            float radius = weaponData.HitRadius + (weaponData.AttackRange * 0.5f);

            Collider[] hits = Physics.OverlapSphere(center, radius, targetLayer);
            HashSet<IDamageable> damagedEntities = new HashSet<IDamageable>();

            foreach (var col in hits)
            {
                if (col.gameObject == attacker) continue;

                Vector3 dirToTarget = (col.bounds.center - attackOrigin.position);
                dirToTarget.y = 0;

                // Kiểm tra góc quét nhát chém (Swing Arc)
                float angle = Vector3.Angle(aimDirection, dirToTarget.normalized);
                if (angle <= weaponData.SwingArcAngle * 0.5f)
                {
                    IDamageable damageable = col.GetComponent<IDamageable>();
                    if (damageable == null)
                    {
                        damageable = col.GetComponentInParent<IDamageable>();
                    }

                    if (damageable != null && !damageable.IsDead && !damagedEntities.Contains(damageable))
                    {
                        damagedEntities.Add(damageable);

                        DamageInfo info = new DamageInfo
                        {
                            Amount = weaponData.Damage,
                            Source = attacker,
                            HitPoint = col.ClosestPoint(attackOrigin.position)
                        };

                        damageable.TakeDamage(info);

                        // Đẩy lùi đối thủ nếu có Rigidbody
                        Rigidbody rb = col.attachedRigidbody;
                        if (rb != null && !rb.isKinematic)
                        {
                            rb.AddForce(aimDirection.normalized * weaponData.KnockbackForce, ForceMode.Impulse);
                        }
                    }
                }
            }

            return true;
        }

        private void SpawnSlashWave(Transform attackOrigin, Vector3 aimDirection)
        {
            Vector3 spawnPos = attackOrigin.position + aimDirection.normalized * 0.9f + Vector3.up * 0.1f;
            Quaternion spawnRot = Quaternion.LookRotation(aimDirection, Vector3.up);

            if (slashWavePrefab != null)
            {
                Instantiate(slashWavePrefab, spawnPos, spawnRot);
            }
            else
            {
                // Khởi tạo vệt sóng kiếm trắng theo chuẩn nếu chưa gắn Prefab
                GameObject waveObj = new GameObject("SwordSlashWave_Instance");
                waveObj.transform.position = spawnPos;
                waveObj.transform.rotation = spawnRot;

                MeshFilter mf = waveObj.AddComponent<MeshFilter>();
                mf.sharedMesh = SwordSlashWave.CreateSlashArcMesh(0.7f, 1.8f, 110f, 20);

                MeshRenderer mr = waveObj.AddComponent<MeshRenderer>();
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                Material waveMat = new Material(shader);
                waveMat.color = new Color(1f, 1f, 1f, 0.9f);
                mr.material = waveMat;

                waveObj.AddComponent<SwordSlashWave>();
            }

            if (slashTrail != null)
            {
                slashTrail.Clear();
                slashTrail.emitting = true;
                StartCoroutine(StopTrailRoutine(0.2f));
            }
        }

        private IEnumerator StopTrailRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (slashTrail != null)
            {
                slashTrail.emitting = false;
            }
        }

        private IEnumerator SwingRoutine()
        {
            Transform targetTransform = visualBlade != null ? visualBlade : transform;
            Quaternion startRot = _defaultLocalRot;

            // Giai đoạn 1: Vung mạnh kiếm từ phải qua trái (0.12s)
            float elapsed = 0f;
            float swingTime = 0.12f;
            Quaternion swingRot = startRot * Quaternion.Euler(60f, -75f, 20f);

            while (elapsed < swingTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / swingTime);
                targetTransform.localRotation = Quaternion.Slerp(startRot, swingRot, t);
                yield return null;
            }

            // Giai đoạn 2: Trở về tư thế sẵn sàng ban đầu (0.18s)
            elapsed = 0f;
            float returnTime = 0.18f;
            while (elapsed < returnTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnTime);
                targetTransform.localRotation = Quaternion.Slerp(swingRot, startRot, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            targetTransform.localRotation = startRot;
            _swingCoroutine = null;
        }

        private void OnDrawGizmosSelected()
        {
            if (weaponData == null) return;
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(transform.position + transform.forward * (weaponData.AttackRange * 0.5f), weaponData.HitRadius);
        }
    }
}
