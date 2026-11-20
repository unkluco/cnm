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
        private Coroutine _trailCoroutine;
        private Transform SwingTarget => visualBlade!=null?visualBlade:transform;
        private ParticleSystem _aura;
        private BladeRibbonTrail _bladeRibbon;

        public override void OnEquipped()
        {
            base.OnEquipped();
            _defaultLocalRot = SwingTarget.localRotation;

            if (slashTrail == null)
            {
                slashTrail = GetComponentInChildren<TrailRenderer>();
            }

            if (slashTrail != null)
            {
                slashTrail.time = 0.23f;
                slashTrail.emitting = false;
            }

            _bladeRibbon = GetComponent<BladeRibbonTrail>();
            if (_bladeRibbon == null) _bladeRibbon = gameObject.AddComponent<BladeRibbonTrail>();
            _bladeRibbon.Configure(visualBlade != null ? visualBlade : transform);

            _aura = WeaponAura.Attach(this, weaponData, _aura);
        }

        public override void OnUnequipped()
        {
            base.OnUnequipped();
            WeaponAura.Detach(ref _aura);
            if (_bladeRibbon != null) _bladeRibbon.Clear();
            if(slashTrail!=null){slashTrail.emitting=false;slashTrail.Clear();}
            SwingTarget.localRotation=_defaultLocalRot;_swingCoroutine=null;_trailCoroutine=null;
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
            _swingCoroutine = StartCoroutine(SwingRoutine(strikeIndex % 2 == 1));

            // 3. Quét sát thương hình quạt phía trước
            Vector3 center = attackOrigin.position + aimDirection * (weaponData.AttackRange * 0.5f);
            float radius = weaponData.HitRadius + (weaponData.AttackRange * 0.5f);

            WeaponVfxProfile vfx = weaponData.Vfx;
            if (vfx.groundShockwave)
            {
                Vector3 ground = new Vector3(center.x, attacker != null ? attacker.transform.position.y : center.y, center.z);
                Color ringColor = vfx.slashColor.a > 0f ? vfx.slashColor : Color.white;
                VfxFactory.SpellImpact(ground, radius, ringColor, vfx.element == CombatVfxElement.Steel ? CombatVfxElement.Stone : vfx.element);
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
                if (damageable == null || damageable.IsDead || WeaponHitResolver.IsFriendly(attacker,damageable) || !damagedEntities.Add(damageable)) continue;
                Vector3 contact=col.ClosestPoint(attackOrigin.position);
                if(Physics.Linecast(attackOrigin.position,contact,1<<8,QueryTriggerInteraction.Ignore))continue;

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
            Vector3 spawnPos = attackOrigin.position + Vector3.up * 0.08f;
            Quaternion spawnRot = Quaternion.LookRotation(aimDirection, Vector3.up);
            if (mirrored)
            {
                // Nhát thứ hai lệch góc một chút để nhìn rõ là hai nhát riêng.
                spawnRot *= Quaternion.Euler(0f, 18f, 0f);
            }

            Color slashColor = weaponData != null ? weaponData.Vfx.slashColor : new Color(1f, 1f, 1f, 0.9f);
            CombatVfxElement element = weaponData != null ? weaponData.Vfx.element : CombatVfxElement.Steel;
            float arc = weaponData != null ? weaponData.SwingArcAngle : 110f;
            float outer = weaponData != null ? Mathf.Max(1.0f, weaponData.AttackRange * .89f) : 1.8f;

            if (slashWavePrefab != null)
            {
                var instance = Instantiate(slashWavePrefab, spawnPos, spawnRot);
                var wave = instance.GetComponent<SwordSlashWave>();
                if (wave != null) wave.Configure(1f, slashColor, element);
            }
            else
            {
                SwordSlashWave.Spawn(spawnPos, aimDirection, outer, arc, slashColor, element, mirrored);
            }

            if (_bladeRibbon != null) _bladeRibbon.Begin(slashColor, element, .15f);
            if (element == CombatVfxElement.Ember && CombatVfxBudget.AllowSecondaryDetail)
                VfxFactory.Burst(spawnPos + aimDirection.normalized * .65f, slashColor, 6, 2.9f, .13f, .34f, .25f);

            if (slashTrail != null)
            {
                slashTrail.Clear();
                slashTrail.emitting = true;
                if(_trailCoroutine!=null)StopCoroutine(_trailCoroutine);
                _trailCoroutine=StartCoroutine(StopTrailRoutine(0.2f));
            }
        }

        private IEnumerator StopTrailRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (slashTrail != null)
            {
                slashTrail.emitting = false;
            }
            _trailCoroutine=null;
        }

        private IEnumerator SwingRoutine(bool mirrored)
        {
            Transform targetTransform = SwingTarget;
            Quaternion startRot = targetTransform.localRotation;

            // Giai đoạn 1: Vung mạnh kiếm từ phải qua trái (0.12s)
            float elapsed = 0f;
            float swingTime = 0.12f;
            Quaternion swingRot = _defaultLocalRot * Quaternion.Euler(60f, mirrored ? 68f : -75f, mirrored ? -20f : 20f);

            while (elapsed < swingTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / swingTime);
                targetTransform.localRotation = Quaternion.Slerp(startRot, swingRot, 1f - (1f - t) * (1f - t));
                yield return null;
            }

            // Giai đoạn 2: Trở về tư thế sẵn sàng ban đầu (0.18s)
            elapsed = 0f;
            float returnTime = 0.18f;
            while (elapsed < returnTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnTime);
                targetTransform.localRotation = Quaternion.Slerp(swingRot, _defaultLocalRot, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            targetTransform.localRotation = _defaultLocalRot;
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
