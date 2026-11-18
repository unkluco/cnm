using LightSouls.Combat.Effects;
using LightSouls.Combat.Stats;
using UnityEngine;

namespace LightSouls.Combat.Weapons
{
    /// <summary>
    /// Triển khai vũ khí tầm xa (Súng ngắn, Súng tán xạ, Nỏ, Tiểu liên, Gậy băng, Súng phóng lựu...).
    /// Bắn ra một hoặc nhiều viên đạn theo góc ngắm. Các khác biệt giữa vũ khí đều nằm trong WeaponDataSO:
    /// số tia, độ lệch, xuyên mục tiêu, đạn nổ vòng cung, hiệu ứng khi trúng, màu vệt đạn.
    /// </summary>
    public class RangedWeapon : WeaponBase
    {
        [Header("Điểm bắn nòng súng (Tùy chọn)")]
        [SerializeField] private Transform muzzlePoint;

        private ParticleSystem _aura;

        public override void OnEquipped()
        {
            base.OnEquipped();
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

            Vector3 aim = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : attackOrigin.forward;
            Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : attackOrigin.position + aim * 0.8f;
            spawnPos.y = Mathf.Max(0.5f, spawnPos.y); // Đảm bảo đạn ở ngang ngực, không dính sàn Y=0

            WeaponVfxProfile vfx = weaponData.Vfx;
            if (vfx.muzzleColor.a > 0f)
            {
                VfxFactory.Burst(spawnPos, vfx.muzzleColor, 7, 2.5f, 0.16f, 0.15f, 0f);
            }

            int count = Mathf.Max(1, weaponData.ProjectileCount);
            float startAngle = count > 1 ? -weaponData.SpreadAngle * 0.5f : 0f;
            float stepAngle = count > 1 ? weaponData.SpreadAngle / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float currentAngle = startAngle + i * stepAngle;
                if (weaponData.Inaccuracy > 0f)
                {
                    currentAngle += Random.Range(-weaponData.Inaccuracy, weaponData.Inaccuracy);
                }

                Vector3 shootDirection = Quaternion.Euler(0f, currentAngle, 0f) * aim;

                if (weaponData.IsExplosive)
                {
                    LaunchExplosive(attackOrigin, spawnPos, shootDirection, targetLayer, attacker);
                }
                else
                {
                    SpawnBullet(spawnPos, shootDirection, targetLayer, attacker);
                }
            }

            return true;
        }

        private void SpawnBullet(Vector3 spawnPos, Vector3 shootDirection, LayerMask targetLayer, GameObject attacker)
        {
            GameObject projObj;
            if (weaponData.ProjectilePrefab != null)
            {
                projObj = Instantiate(weaponData.ProjectilePrefab, spawnPos, Quaternion.LookRotation(shootDirection));
            }
            else
            {
                // Fallback tạo viên đạn hình cầu nếu chưa gắn Prefab
                projObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projObj.transform.position = spawnPos;
                projObj.transform.rotation = Quaternion.LookRotation(shootDirection);
                projObj.transform.localScale = Vector3.one * 0.25f;

                var col = projObj.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;

                projObj.AddComponent<Projectile>();
            }

            // Tô màu đạn theo màu vệt của vũ khí để mỗi súng nhìn khác nhau.
            Color trailColor = weaponData.Vfx.trailColor;
            Renderer r = projObj.GetComponentInChildren<Renderer>();
            if (r != null && trailColor.a > 0f) r.material.color = new Color(trailColor.r, trailColor.g, trailColor.b, 1f);

            // Xác định Layer của đạn dựa vào người bắn (Player -> PlayerAttack (9), Quái -> EnemyAttack (10))
            projObj.layer = attacker != null && attacker.CompareTag("Player") ? 9 : 10;

            VfxFactory.AttachTrail(projObj, weaponData.Vfx.trailColor);

            Projectile projectile = projObj.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.Initialize(weaponData.Damage, weaponData.ProjectileSpeed, weaponData.ProjectileLifetime, targetLayer, attacker);
                projectile.SetWeapon(weaponData, weaponData.PierceCount);
            }
        }

        private void LaunchExplosive(Transform attackOrigin, Vector3 spawnPos, Vector3 shootDirection, LayerMask targetLayer, GameObject attacker)
        {
            float range = weaponData.AttackRange;
            Vector3 groundOrigin = attackOrigin.position;
            groundOrigin.y = attacker != null ? attacker.transform.position.y : 0f;

            // Ngắm vào kẻ địch gần hướng bắn nhất trong tầm; không có thì rơi ở cuối tầm.
            Vector3 target = groundOrigin + shootDirection * range;
            float bestAngle = 25f;
            foreach (Collider col in Physics.OverlapSphere(groundOrigin, range, targetLayer))
            {
                Vector3 to = col.bounds.center - groundOrigin;
                to.y = 0f;
                float angle = Vector3.Angle(shootDirection, to);
                if (angle < bestAngle && WeaponHitResolver.FindDamageable(col) != null)
                {
                    bestAngle = angle;
                    target = new Vector3(col.bounds.center.x, groundOrigin.y, col.bounds.center.z);
                }
            }

            float distance = Vector3.Distance(spawnPos, target);
            float flightTime = Mathf.Clamp(distance / Mathf.Max(1f, weaponData.ProjectileSpeed), 0.25f, 1.5f);
            float damage = weaponData.Damage * PlayerStats.DamageMultiplierOf(attacker);
            ExplosiveProjectile.Launch(spawnPos, target, flightTime, weaponData.ArcHeight, damage, weaponData.ExplosionRadius,
                attacker, weaponData.ExplosionDamagesOwner, weaponData.WeaponColor, weaponData);
        }
    }
}
