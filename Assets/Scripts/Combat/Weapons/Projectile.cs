using System.Collections.Generic;
using UnityEngine;
using LightSouls.Common;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Runes;

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
        private RuneImpact _runeImpact;
        private int _pierceRemaining;
        private Color _visualColor = Color.white;
        private CombatVfxElement _visualElement;
        private readonly RaycastHit[] _sweepHits = new RaycastHit[12];
        private Collider _projectileCollider;
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
        public void SetWeapon(WeaponDataSO weapon, int pierceCount,RuneImpact runeImpact=null)
        {
            _weapon = weapon;
            _runeImpact=runeImpact;
            _pierceRemaining = Mathf.Max(0, pierceCount);
        }

        public void SetVisualStyle(Color color, CombatVfxElement element, float trailWidth = .16f)
        {
            _visualColor = color;
            _visualElement = element;
            VfxFactory.TintProjectile(gameObject, color);
            VfxFactory.AttachTrail(gameObject, color, trailWidth, .23f);
            VfxFactory.ProjectileAccent(transform, color, element);
        }

        private void Start()
        {
            _spawnTime = Time.time;
            _projectileCollider = GetComponent<Collider>();
            if (_projectileCollider != null)
            {
                _projectileCollider.isTrigger = true;
            }
        }

        private void Update()
        {
            if (_finished) return;
            Advance(speed * Time.deltaTime);

            if (Time.time - _spawnTime >= maxLifetime)
            {
                Finish();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            ResolveContact(other, other.ClosestPoint(transform.position));
        }

        // Match the original collider's thickness along this frame's travel. A slow mobile frame
        // must not let a visible spell or projectile pass through a target or a wall.
        private void Advance(float step)
        {
            if (step <= 0f) return;
            Vector3 from = transform.position, forward = transform.forward;
            int environment = LayerMask.NameToLayer("Environment");
            int mask = targetLayer.value | (environment >= 0 ? 1 << environment : 0);
            float radius = .05f;
            if (_projectileCollider is SphereCollider sphere)
            {
                Vector3 scale = transform.lossyScale;
                radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            }
            else if (_projectileCollider != null)
            {
                Vector3 ext = _projectileCollider.bounds.extents;
                radius = Mathf.Max(.01f, Mathf.Min(ext.x, Mathf.Min(ext.y, ext.z)));
            }
            int hits = Physics.SphereCastNonAlloc(from, radius, forward, _sweepHits, step, mask, QueryTriggerInteraction.Ignore);
            // NonAlloc casts have no ordering guarantee. Process the closest hits first, preserving pierce and wall rules.
            for (int i = 0; i < hits; i++)
            {
                int nearest = i;
                for (int j = i + 1; j < hits; j++) if (_sweepHits[j].distance < _sweepHits[nearest].distance) nearest = j;
                (_sweepHits[i], _sweepHits[nearest]) = (_sweepHits[nearest], _sweepHits[i]);
                var hit = _sweepHits[i];
                if (hit.collider == null) continue;
                transform.position = from + forward * hit.distance;
                ResolveContact(hit.collider, hit.point);
                if (_finished) return;
            }
            transform.position = from + forward * step;
        }

        private void ResolveContact(Collider other, Vector3 contactPoint)
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
                if (damageable != null && !damageable.IsDead && !WeaponHitResolver.IsFriendly(_source,damageable) && _alreadyHit.Add(damageable))
                {
                    WeaponHitResolver.Apply(_weapon, damage, _source, damageable, contactPoint,
                        true, transform.forward, _visualElement, _visualColor,_runeImpact);

                    if (_pierceRemaining > 0)
                    {
                        _pierceRemaining--;
                        return;
                    }

                    Finish();
                    return;
                }
            }

            if (isEnvironment)
            {
                VfxFactory.HitImpact(contactPoint, -transform.forward, _visualColor, _visualElement, .18f);
                Finish();
            }
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            VfxFactory.ReleaseTrail(gameObject);
            Destroy(gameObject);
        }
    }
}
