using UnityEngine;

namespace LightSouls.Combat.Weapons
{
    /// <summary>
    /// Triển khai vũ khí tầm xa (Súng ngắn, Súng tán xạ / Shotgun, Nỏ...).
    /// Bắn ra một hoặc nhiều viên đạn theo góc ngắm.
    /// </summary>
    public class RangedWeapon : WeaponBase
    {
        [Header("Điểm bắn nòng súng (Tùy chọn)")]
        [SerializeField] private Transform muzzlePoint;

        public override bool ExecuteAttack(Transform attackOrigin, Vector3 aimDirection, LayerMask targetLayer, GameObject attacker)
        {
            if (!CanAttack || weaponData == null) return false;

            lastAttackTime = Time.time;

            Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : attackOrigin.position + aimDirection.normalized * 0.8f;
            spawnPos.y = Mathf.Max(0.5f, spawnPos.y); // Đảm bảo đạn ở ngang ngực, không dính sàn Y=0

            int count = Mathf.Max(1, weaponData.ProjectileCount);
            float startAngle = count > 1 ? -weaponData.SpreadAngle * 0.5f : 0f;
            float stepAngle = count > 1 ? weaponData.SpreadAngle / (count - 1) : 0f;

            // Xác định Layer của đạn dựa vào người bắn (Player -> PlayerAttack (9), Quái -> EnemyAttack (10))
            int attackLayer = attacker.CompareTag("Player") ? 9 : 10;

            for (int i = 0; i < count; i++)
            {
                float currentAngle = startAngle + i * stepAngle;
                Quaternion rotationOffset = Quaternion.Euler(0f, currentAngle, 0f);
                Vector3 shootDirection = rotationOffset * aimDirection.normalized;

                GameObject projObj = null;
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

                    Renderer r = projObj.GetComponent<Renderer>();
                    if (r != null) r.material.color = weaponData.WeaponColor;

                    projObj.AddComponent<Projectile>();
                }

                projObj.layer = attackLayer;

                Projectile projectile = projObj.GetComponent<Projectile>();
                if (projectile != null)
                {
                    projectile.Initialize(weaponData.Damage, weaponData.ProjectileSpeed, weaponData.ProjectileLifetime, targetLayer, attacker);
                }
            }

            return true;
        }
    }
}
