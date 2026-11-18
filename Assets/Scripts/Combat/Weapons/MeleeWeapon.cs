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
        private ParticleSystem _aura;

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

            _aura = WeaponAura.Attach(this, weaponData, _aura);
        }

        public override void OnUnequipped()
        {
            base.OnUnequipped();
            WeaponAura.Detach(ref _aura);
        }

        public override bool ExecuteAttack(Transform attackOrigin, Vector3 aimDirection, LayerMask targetLayer, GameObject attacker)
        {
            if (!CanAttack || weaponData == null) return false;

            lastAttackTime = Time.time;
            Vector3 direction = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : attackOrigin.forward;

            // Nhát đầu đánh ngay; các nhát sau (Song Dao...) đánh tiếp theo nhịp strikeInterval.
            PerformStrike(attackOrigin, direction, targetLayer, attacker, 0);
            if (weaponData.StrikeCount > 1)
            {
                StartCoroutine(ExtraStrikesRoutine(attackOrigin, direction, targetLayer, attacker));
            }

            return true;
        }

        private IEnumerator ExtraStrikesRoutine(Transform attackOrigin, Vector3 direction, LayerMask targetLayer, GameObject attacker)
        {
            for (int i = 1; i < weaponData.StrikeCount; i++)
            {
                yield return new WaitForSeconds(weaponData.StrikeInterval);
                if (attackOrigin == null || weaponData == null) yield break;
                PerformStrike(attackOrigin, direction, targetLayer, attacker, i);
            }
        }

        private void PerformStrike(Transform attackOrigin, Vector3 aimDirection, LayerMask targetLayer, GameObject attacker, int strikeIndex)
        {
            // 1. Vệt chém (nhát lẻ lật ngược để hai nhát liên tiếp nhìn như chém chéo).
            SpawnSlashWave(attackOrigin, aimDirection, strikeIndex % 2 == 1);

            // 2. Diễn hoạt vung vũ khí
            if (_swingCoroutine != null) StopCoroutine(_swingCoroutine);
            _swingCoroutine = StartCoroutine(SwingRoutine());

            // 3. Quét sát thương hình quạt phía trước
            Vector3 center = attackOrigin.position + aimDirection * (weaponData.AttackRange * 0.5f);
            float radius = weaponData.HitRadius + (weaponData.AttackRange * 0.5f);

            WeaponVfxProfile vfx = weaponData.Vfx;
            if (vfx.groundShockwave)
            {
                Vector3 ground = new Vector3(center.x, attacker != null ? attacker.transform.position.y : center.y, center.z);
                Color ringColor = vfx.slashColor.a > 0f ? vfx.slashColor : Color.white;
                VfxFactory.Ring(ground, ringColor, radius, 0.35f, 56);
                VfxFactory.Burst(ground + Vector3.up * 0.2f, new Color(0.55f, 0.48f, 0.4f, 0.9f), 14, 3f, 0.3f, 0.5f, 1f);
            }

            Collider[] hits = Physics.OverlapSphere(center, radius, targetLayer);
            HashSet<IDamageable> damagedEntities = new HashSet<IDamageable>();

            foreach (var col in hits)
            {
                if (attacker != null && col.transform.root == attacker.transform.root) continue;

                Vector3 dirToTarget = (col.bounds.center - attackOrigin.position);
                dirToTarget.y = 0;

                // Kiểm tra góc quét nhát chém (Swing Arc)
                float angle = dirToTarget.sqrMagnitude < 0.01f ? 0f : Vector3.Angle(aimDirection, dirToTarget.normalized);
                if (angle > weaponData.SwingArcAngle * 0.5f) continue;

                IDamageable damageable = WeaponHitResolver.FindDamageable(col);
                if (damageable == null || damageable.IsDead || !damagedEntities.Add(damageable)) continue;

                WeaponHitResolver.Apply(weaponData, weaponData.Damage, attacker, damageable, col.ClosestPoint(attackOrigin.position));

                // Đẩy lùi đối thủ nếu có Rigidbody
                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    rb.AddForce(aimDirection * weaponData.KnockbackForce, ForceMode.Impulse);
                }
            }
        }

        private void SpawnSlashWave(Transform attackOrigin, Vector3 aimDirection, bool mirrored)
        {
            Vector3 spawnPos = attackOrigin.position + aimDirection.normalized * 0.9f + Vector3.up * 0.1f;
            Quaternion spawnRot = Quaternion.LookRotation(aimDirection, Vector3.up);
            if (mirrored)
            {
                // Nhát thứ hai lệch góc một chút để nhìn rõ là hai nhát riêng.
                spawnRot *= Quaternion.Euler(0f, 18f, 0f);
            }

            Color slashColor = weaponData != null ? weaponData.Vfx.slashColor : new Color(1f, 1f, 1f, 0.9f);

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
                float arc = weaponData != null ? Mathf.Clamp(weaponData.SwingArcAngle, 60f, 300f) : 110f;
                float outer = weaponData != null ? Mathf.Max(1.4f, weaponData.AttackRange * 0.9f) : 1.8f;
                mf.sharedMesh = SwordSlashWave.CreateSlashArcMesh(outer * 0.4f, outer, arc, 24);

                MeshRenderer mr = waveObj.AddComponent<MeshRenderer>();
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                Material waveMat = new Material(shader);
                waveMat.color = slashColor;
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
