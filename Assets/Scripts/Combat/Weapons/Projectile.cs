using UnityEngine;
using LightSouls.Common;

namespace LightSouls.Combat.Weapons
{
    /// <summary>
    /// Điều khiển viên đạn bay thẳng và gây sát thương khi va chạm.
    /// Layer của đạn phải được gán PlayerAttack (cho Player) hoặc EnemyAttack (cho Quái).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 18f;
        [SerializeField] private float damage = 20f;
        [SerializeField] private float maxLifetime = 3f;
        [SerializeField] private LayerMask targetLayer;

        private GameObject _source;
        private float _spawnTime;
        private bool _hasHit = false;

        public void Initialize(float damageAmount, float projectileSpeed, float lifetime, LayerMask targets, GameObject source)
        {
            damage = damageAmount;
            speed = projectileSpeed;
            maxLifetime = lifetime;
            targetLayer = targets;
            _source = source;
            _spawnTime = Time.time;
        }

        private void Start()
        {
            _spawnTime = Time.time;
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void Update()
        {
            transform.position += transform.forward * (speed * Time.deltaTime);

            if (Time.time - _spawnTime >= maxLifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasHit) return;

            // Kiểm tra xem đối tượng có nằm trong Layer mục tiêu hoặc là môi trường không
            int otherLayerMask = 1 << other.gameObject.layer;
            bool isTargetLayer = (targetLayer.value & otherLayerMask) != 0;
            
            // Môi trường (sàn, tường, cửa) dùng layer Environment; dự án không có tag "Environment".
            bool isEnvironment = other.gameObject.layer == LayerMask.NameToLayer("Environment");

            if (isTargetLayer)
            {
                IDamageable damageable = other.GetComponent<IDamageable>();
                if (damageable == null)
                {
                    damageable = other.GetComponentInParent<IDamageable>();
                }

                if (damageable != null && !damageable.IsDead)
                {
                    _hasHit = true;
                    DamageInfo info = new DamageInfo
                    {
                        Amount = damage,
                        Source = _source,
                        HitPoint = other.ClosestPoint(transform.position)
                    };

                    damageable.TakeDamage(info);
                    Destroy(gameObject);
                    return;
                }
            }

            if (isEnvironment)
            {
                _hasHit = true;
                Destroy(gameObject);
            }
        }
    }
}
