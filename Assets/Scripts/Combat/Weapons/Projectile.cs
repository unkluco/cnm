using System.Collections.Generic;
using UnityEngine;
using LightSouls.Common;

namespace LightSouls.Combat.Weapons
{
    /// <summary>
    /// Điều khiển viên đạn bay thẳng và gây sát thương khi va chạm.
    /// Layer của đạn phải được gán PlayerAttack (cho Player) hoặc EnemyAttack (cho Quái).
    /// Đạn có thể xuyên qua nhiều mục tiêu (pierce) và mang hiệu ứng trúng đòn của vũ khí (qua WeaponHitResolver).
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
        private bool _finished;
        private WeaponDataSO _weapon;
        private int _pierceRemaining;
        private readonly HashSet<IDamageable> _alreadyHit = new HashSet<IDamageable>();

        public void Initialize(float damageAmount, float projectileSpeed, float lifetime, LayerMask targets, GameObject source)
        {
            damage = damageAmount;
            speed = projectileSpeed;
            maxLifetime = lifetime;
            targetLayer = targets;
            _source = source;
            _spawnTime = Time.time;
        }

        /// <summary>Gắn vũ khí bắn ra (để chạy hiệu ứng trúng đòn) và số mục tiêu được xuyên thêm.</summary>
        public void SetWeapon(WeaponDataSO weapon, int pierceCount)
        {
            _weapon = weapon;
            _pierceRemaining = Mathf.Max(0, pierceCount);
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
            if (_finished) return;

            // Kiểm tra xem đối tượng có nằm trong Layer mục tiêu hoặc là môi trường không
            int otherLayerMask = 1 << other.gameObject.layer;
            bool isTargetLayer = (targetLayer.value & otherLayerMask) != 0;

            // Môi trường (sàn, tường, cửa) dùng layer Environment; dự án không có tag "Environment".
            bool isEnvironment = other.gameObject.layer == LayerMask.NameToLayer("Environment");

            if (isTargetLayer)
            {
                IDamageable damageable = WeaponHitResolver.FindDamageable(other);
                if (damageable != null && !damageable.IsDead && _alreadyHit.Add(damageable))
                {
                    WeaponHitResolver.Apply(_weapon, damage, _source, damageable, other.ClosestPoint(transform.position));

                    if (_pierceRemaining > 0)
                    {
                        _pierceRemaining--;
                        return;
                    }

                    _finished = true;
                    Destroy(gameObject);
                    return;
                }
            }

            if (isEnvironment)
            {
                _finished = true;
                Destroy(gameObject);
            }
        }
    }
}
